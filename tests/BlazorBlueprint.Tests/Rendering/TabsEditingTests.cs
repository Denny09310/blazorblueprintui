using BlazorBlueprint.Components;
using BlazorBlueprint.Tests.Performance;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorBlueprint.Tests.Rendering;

/// <summary>
/// Closing, adding, renaming and reordering on <see cref="BbTabsList"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every one of the four is off unless its flag <em>and</em> its callback are both set. That pairing
/// is what these tests spend most of their time on: a flag on its own would draw an affordance that
/// does nothing when it is used, which is worse than drawing nothing.
/// </para>
/// <para>
/// Reordering by drag is not covered here. Its position comes from the DOM through a JavaScript
/// module, and the test runtime has no DOM to read — that path is verified in a browser instead.
/// </para>
/// </remarks>
public class TabsEditingTests
{
    [Fact]
    public async Task APlainTabListDrawsNoCloseIconAndNoAddButton()
    {
        var markup = await Render(_ => { });

        Assert.DoesNotContain("role=\"presentation\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-tabs-id", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClosableWithoutAHandlerDrawsNothing()
    {
        // The flag alone is not enough. An icon with nowhere to report to is a dead control.
        var markup = await Render(p => p[nameof(BbTabsList.Closable)] = true);

        Assert.DoesNotContain("role=\"presentation\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClosableWithAHandlerDrawsACloseIconOnEveryTab()
    {
        var markup = await Render(p =>
        {
            p[nameof(BbTabsList.Closable)] = true;
            p[nameof(BbTabsList.OnClose)] = EventCallback.Factory.Create<string>(new object(), _ => { });
        });

        Assert.Equal(2, Occurrences(markup, "role=\"presentation\""));
    }

    [Fact]
    public async Task ATabCanRefuseToBeClosableWhenTheListSaysOtherwise()
    {
        // The usual reason is a pinned tab: everything closes except the one that must not.
        var markup = await Render(
            p =>
            {
                p[nameof(BbTabsList.Closable)] = true;
                p[nameof(BbTabsList.OnClose)] = EventCallback.Factory.Create<string>(new object(), _ => { });
            },
            firstTabClosable: false);

        Assert.Equal(1, Occurrences(markup, "role=\"presentation\""));
    }

    [Fact]
    public async Task AddableWithoutAHandlerDrawsNothing()
    {
        var markup = await Render(p => p[nameof(BbTabsList.Addable)] = true);

        Assert.DoesNotContain("data-tabs-id", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheAddButtonSitsOutsideTheTablist()
    {
        // A tablist may only hold tabs. A button inside one either lies about being a tab or takes
        // a place in the arrow-key order that nobody expects.
        var markup = await Render(p =>
        {
            p[nameof(BbTabsList.Addable)] = true;
            p[nameof(BbTabsList.AddLabel)] = "New sheet";
            p[nameof(BbTabsList.OnAdd)] = EventCallback.Factory.Create(new object(), () => { });
        });

        var tablistEnd = markup.IndexOf("</div>", markup.IndexOf("role=\"tablist\"", StringComparison.Ordinal), StringComparison.Ordinal);
        var addButton = markup.IndexOf("New sheet", StringComparison.Ordinal);

        Assert.True(addButton > tablistEnd, "The add button must come after the tablist closes.");
    }

    [Fact]
    public async Task DeleteOnAFocusedTabAsksForItToBeClosed()
    {
        var closed = new List<string>();

        await RunAsync(async (renderer, _) =>
        {
            await Press(renderer, "Delete");
            Assert.Equal(["one"], closed);
        },
        p =>
        {
            p[nameof(BbTabsList.Closable)] = true;
            p[nameof(BbTabsList.OnClose)] = EventCallback.Factory.Create<string>(new object(), closed.Add);
        });
    }

    [Fact]
    public async Task DeleteDoesNothingWhereTheTabIsNotClosable()
    {
        var closed = new List<string>();

        await RunAsync(async (renderer, _) =>
        {
            await Press(renderer, "Delete");
            Assert.Empty(closed);
        },
        p => p[nameof(BbTabsList.OnClose)] = EventCallback.Factory.Create<string>(new object(), closed.Add));
    }

    [Fact]
    public async Task F2OpensAnEditorHoldingTheCurrentLabel()
    {
        await RunAsync(async (renderer, _) =>
        {
            await Press(renderer, "F2");

            var markup = await renderer.Dispatcher.InvokeAsync(renderer.Markup);
            Assert.Contains("value=\"One\"", markup, StringComparison.Ordinal);
        },
        Renamable);
    }

    [Fact]
    public async Task AnEditCommittedUnchangedIsNotReported()
    {
        var renames = new List<TabRenameContext>();

        await RunAsync(async (renderer, trigger) =>
        {
            await Press(renderer, "F2");
            await Commit(renderer, trigger, "One");

            Assert.Empty(renames);
        },
        p => Renamable(p, renames));
    }

    [Fact]
    public async Task AnEditCommittedEmptyIsTreatedAsACancel()
    {
        var renames = new List<TabRenameContext>();

        await RunAsync(async (renderer, trigger) =>
        {
            await Press(renderer, "F2");
            await Commit(renderer, trigger, "   ");

            Assert.Empty(renames);
            Assert.False(ComponentProbe.Field<bool>(trigger, "isRenaming"));
        },
        p => Renamable(p, renames));
    }

    [Fact]
    public async Task ARefusedRenameReopensTheEditorWithWhatWasTyped()
    {
        // Retyping a long name because one character clashed is the kind of small cruelty that
        // makes people stop renaming things.
        var renames = new List<TabRenameContext>();

        await RunAsync(async (renderer, trigger) =>
        {
            await Press(renderer, "F2");
            await Commit(renderer, trigger, "Taken");

            Assert.Single(renames);
            Assert.True(ComponentProbe.Field<bool>(trigger, "isRenaming"));
            Assert.Equal("Taken", ComponentProbe.Field<string>(trigger, "renameText"));
        },
        p => Renamable(p, renames, refuse: true));
    }

    [Fact]
    public async Task AnAcceptedRenameClosesTheEditorAndReportsBothLabels()
    {
        var renames = new List<TabRenameContext>();

        await RunAsync(async (renderer, trigger) =>
        {
            await Press(renderer, "F2");
            await Commit(renderer, trigger, "  Renamed  ");

            var reported = Assert.Single(renames);
            Assert.Equal("one", reported.Value);
            Assert.Equal("One", reported.OldLabel);

            // Trimmed, so a stray space does not become part of the name.
            Assert.Equal("Renamed", reported.NewLabel);
            Assert.False(ComponentProbe.Field<bool>(trigger, "isRenaming"));
        },
        p => Renamable(p, renames));
    }

    // -------------------------------------------------------------------------------------

    private static void Renamable(Dictionary<string, object?> parameters) =>
        Renamable(parameters, []);

    private static void Renamable(
        Dictionary<string, object?> parameters,
        List<TabRenameContext> sink,
        bool refuse = false)
    {
        parameters[nameof(BbTabsList.Renamable)] = true;
        parameters[nameof(BbTabsList.OnRename)] =
            EventCallback.Factory.Create<TabRenameContext>(sink, context =>
            {
                sink.Add(context);
                context.Cancel = refuse;
            });
    }

    /// <summary>Raises a key-down on the first handler that carries one.</summary>
    private static Task Press(ComponentTestRenderer renderer, string key) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = key }));

    /// <summary>Types into the open editor and presses Enter.</summary>
    private static async Task Commit(ComponentTestRenderer renderer, BbTabsTrigger trigger, string text)
    {
        await renderer.Dispatcher.InvokeAsync(() => ComponentProbe.SetField(trigger, "renameText", text));
        await Press(renderer, "Enter");
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        var at = haystack.IndexOf(needle, StringComparison.Ordinal);

        while (at >= 0)
        {
            count++;
            at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private static async Task<string> Render(
        Action<Dictionary<string, object?>> configure,
        bool? firstTabClosable = null)
    {
        var markup = string.Empty;

        await RunAsync(
            async (renderer, _) => markup = await renderer.Dispatcher.InvokeAsync(renderer.Markup),
            configure,
            firstTabClosable);

        return markup;
    }

    private static async Task RunAsync(
        Func<ComponentTestRenderer, BbTabsTrigger, Task> body,
        Action<Dictionary<string, object?>> configure,
        bool? firstTabClosable = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddBlazorBlueprintComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        var listParameters = new Dictionary<string, object?>();
        configure(listParameters);

        var parameters = new Dictionary<string, object?>
        {
            [nameof(BbTabs.DefaultValue)] = "one",
            [nameof(BbTabs.ChildContent)] = (RenderFragment)(builder => BuildList(builder, listParameters, firstTabClosable)),
        };

        await renderer.Dispatcher.InvokeAsync(async () => await renderer.MountAsync<BbTabs>(parameters));

        var trigger = renderer.FindComponent<BbTabsTrigger>();
        await body(renderer, trigger);
    }

    private static void BuildList(
        RenderTreeBuilder builder,
        Dictionary<string, object?> listParameters,
        bool? firstTabClosable)
    {
        builder.OpenComponent<BbTabsList>(0);

        var sequence = 1;
        foreach (var (name, value) in listParameters)
        {
            builder.AddAttribute(sequence++, name, value);
        }

        builder.AddAttribute(50, nameof(BbTabsList.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<BbTabsTrigger>(0);
            inner.AddAttribute(1, nameof(BbTabsTrigger.Value), "one");
            inner.AddAttribute(2, nameof(BbTabsTrigger.Label), "One");

            if (firstTabClosable is { } closable)
            {
                inner.AddAttribute(3, nameof(BbTabsTrigger.Closable), (bool?)closable);
            }

            inner.CloseComponent();

            inner.OpenComponent<BbTabsTrigger>(10);
            inner.AddAttribute(11, nameof(BbTabsTrigger.Value), "two");
            inner.AddAttribute(12, nameof(BbTabsTrigger.Label), "Two");
            inner.CloseComponent();
        }));

        builder.CloseComponent();
    }
}
