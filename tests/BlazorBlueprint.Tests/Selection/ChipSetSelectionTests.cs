using BlazorBlueprint.Components;
using BlazorBlueprint.Tests.Performance;
using BlazorBlueprint.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorBlueprint.Tests.Selection;

/// <summary>
/// <see cref="BbChipSet{TValue}"/> owns the selection for its chips, and a chip carries its value
/// as <see cref="object"/> so it can be used without a type argument.
/// <para>
/// That boxing is the reason these tests exist. The set unboxes back to <c>TValue</c> on every
/// operation, so a value that does not match the set's type, or a chip with no value at all, has
/// to fall out of the selection rather than throw. The controlled and uncontrolled paths both run
/// through the same toggle, and <c>Required</c> guards it — none of which is visible from the
/// rendered markup alone.
/// </para>
/// </summary>
public class ChipSetSelectionTests
{
    [Fact]
    public async Task ClickingAChipReportsItsValue()
    {
        await RunAsync(["alpha", "beta"], async (renderer, sink, chips) =>
        {
            await Activate(renderer, chips["beta"]);

            Assert.Equal("beta", sink.Value);
        });
    }

    [Fact]
    public async Task ClickingTheSelectedChipClearsTheSelection()
    {
        await RunAsync(["alpha", "beta"], async (renderer, sink, chips) =>
        {
            await Activate(renderer, chips["alpha"]);
            await Activate(renderer, chips["alpha"]);

            Assert.Null(sink.Value);
        });
    }

    [Fact]
    public async Task RequiredKeepsTheLastSelectedChip()
    {
        await RunAsync(["alpha", "beta"], async (renderer, sink, chips) =>
        {
            await Activate(renderer, chips["alpha"]);
            await Activate(renderer, chips["alpha"]);

            Assert.Equal("alpha", sink.Value);
        },
        configure: parameters => parameters[nameof(BbChipSet<string>.Required)] = true);
    }

    [Fact]
    public async Task MultipleModeAddsAndRemovesValues()
    {
        await RunAsync(["alpha", "beta", "gamma"], async (renderer, sink, chips) =>
        {
            await Activate(renderer, chips["alpha"]);
            await Activate(renderer, chips["gamma"]);

            Assert.Equal(["alpha", "gamma"], sink.Values);

            await Activate(renderer, chips["alpha"]);

            Assert.Equal(["gamma"], sink.Values);
        },
        configure: parameters =>
            parameters[nameof(BbChipSet<string>.SelectionMode)] = ChipSelectionMode.Multiple);
    }

    [Fact]
    public async Task MultipleModeReportsANewListRatherThanMutatingTheBoundOne()
    {
        var bound = new List<string> { "alpha" };

        await RunAsync(["alpha", "beta"], async (renderer, sink, chips) =>
        {
            await Activate(renderer, chips["beta"]);

            Assert.Equal(["alpha", "beta"], sink.Values);

            // The list the caller handed in is untouched, so a component that re-renders from it
            // cannot see a selection it was never told about.
            Assert.Equal(["alpha"], bound);
        },
        configure: parameters =>
        {
            parameters[nameof(BbChipSet<string>.SelectionMode)] = ChipSelectionMode.Multiple;
            parameters[nameof(BbChipSet<string>.Values)] = bound;
        });
    }

    [Fact]
    public async Task DismissingAChipDropsItsValueAndReportsIt()
    {
        var dismissed = new List<string>();

        await RunAsync(["alpha", "beta"], async (renderer, sink, chips) =>
        {
            await Activate(renderer, chips["alpha"]);
            await Dismiss(renderer, chips["alpha"]);

            Assert.Null(sink.Value);
            Assert.Equal(["alpha"], dismissed);
        },
        configure: parameters =>
        {
            parameters[nameof(BbChipSet<string>.Dismissible)] = true;
            parameters[nameof(BbChipSet<string>.OnDismiss)] =
                EventCallback.Factory.Create<string>(dismissed, dismissed.Add);
        });
    }

    [Fact]
    public async Task WithoutABindingTheSetKeepsItsOwnSelection()
    {
        await RunAsync(["alpha", "beta"], async (renderer, _, chips) =>
        {
            await Activate(renderer, chips["beta"]);

            // Nothing was bound, so the only record of the click is the set's own state, read
            // back through the chip that now reports itself as pressed.
            Assert.Contains("data-state=\"on\"", renderer.Markup());
            Assert.Contains("aria-pressed=\"true\"", renderer.Markup());
        },
        bindValue: false);
    }

    [Fact]
    public async Task AChipWithNoValueIsNotInteractive()
    {
        await RunAsync([], async (renderer, _, _) =>
        {
            var markup = renderer.Markup();

            // A chip that cannot take part in the selection renders as a span, so a screen
            // reader does not announce a button that does nothing.
            Assert.DoesNotContain("aria-pressed", markup);
            Assert.DoesNotContain("<button", markup);
        },
        valuelessChip: true);
    }

    [Fact]
    public async Task AValueOfTheWrongTypeIsIgnored()
    {
        await RunAsync([], async (renderer, sink, chips) =>
        {
            // The chip is boxed as object, so nothing stops a caller passing an int to a set of
            // strings. It must fall out rather than throw.
            await Activate(renderer, chips["wrong"]);

            Assert.Null(sink.Value);
        },
        wrongTypedChip: true);
    }

    // -------------------------------------------------------------------------------------

    private sealed class SelectionSink
    {
        public string? Value { get; private set; }
        public IReadOnlyList<string> Values { get; private set; } = [];

        public void SetValue(string? value) => Value = value;
        public void SetValues(List<string> values) => Values = values;
    }

    private static Task Activate(ComponentTestRenderer renderer, BbChip chip) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await (ComponentProbe.Call(chip, "HandleActivateAsync", new MouseEventArgs()) as Task
                   ?? Task.CompletedTask));

    private static Task Dismiss(ComponentTestRenderer renderer, BbChip chip) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await (ComponentProbe.Call(chip, "HandleDismissAsync", new MouseEventArgs()) as Task
                   ?? Task.CompletedTask));

    private static async Task RunAsync(
        string[] values,
        Func<ComponentTestRenderer, SelectionSink, Dictionary<string, BbChip>, Task> body,
        Action<Dictionary<string, object?>>? configure = null,
        bool bindValue = true,
        bool valuelessChip = false,
        bool wrongTypedChip = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddBlazorBlueprintComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        var sink = new SelectionSink();
        var chips = new Dictionary<string, BbChip>(StringComparer.Ordinal);

        var parameters = new Dictionary<string, object?>
        {
            [nameof(BbChipSet<string>.ChildContent)] =
                (RenderFragment)(builder => BuildChips(builder, values, chips, valuelessChip, wrongTypedChip)),
        };

        if (bindValue)
        {
            parameters[nameof(BbChipSet<string>.ValueChanged)] =
                EventCallback.Factory.Create<string?>(sink, sink.SetValue);
            parameters[nameof(BbChipSet<string>.ValuesChanged)] =
                EventCallback.Factory.Create<List<string>>(sink, sink.SetValues);
        }

        configure?.Invoke(parameters);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.MountAsync<BbChipSet<string>>(parameters));

        await body(renderer, sink, chips);
    }

    private static void BuildChips(
        RenderTreeBuilder builder,
        string[] values,
        Dictionary<string, BbChip> chips,
        bool valuelessChip,
        bool wrongTypedChip)
    {
        var sequence = 0;

        foreach (var value in values)
        {
            var key = value;
            builder.OpenComponent<BbChip>(sequence++);
            builder.AddComponentParameter(sequence++, nameof(BbChip.Value), value);
            builder.AddComponentReferenceCapture(sequence++, captured => chips[key] = (BbChip)captured);
            builder.CloseComponent();
        }

        if (valuelessChip)
        {
            builder.OpenComponent<BbChip>(sequence++);
            builder.AddComponentReferenceCapture(sequence++, captured => chips["none"] = (BbChip)captured);
            builder.CloseComponent();
        }

        if (wrongTypedChip)
        {
            builder.OpenComponent<BbChip>(sequence++);
            builder.AddComponentParameter(sequence++, nameof(BbChip.Value), 42);
            builder.AddComponentReferenceCapture(sequence++, captured => chips["wrong"] = (BbChip)captured);
            builder.CloseComponent();
        }
    }
}
