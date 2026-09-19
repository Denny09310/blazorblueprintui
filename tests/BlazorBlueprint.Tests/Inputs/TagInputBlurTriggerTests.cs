using BlazorBlueprint.Components;
using BlazorBlueprint.Tests.Performance;
using BlazorBlueprint.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorBlueprint.Tests.Inputs;

/// <summary>
/// <see cref="TagInputTrigger.Blur"/> commits whatever was typed when focus leaves the input, so a
/// half-finished entry is not lost to a click elsewhere.
/// <para>
/// The commit rides the delay that already defers closing the suggestion list. That is what keeps
/// it safe, and it is what these tests are really checking: a suggestion click and a return of
/// focus both cancel the delay, so neither can also add the typed fragment.
/// </para>
/// </summary>
public class TagInputBlurTriggerTests
{
    // The component waits this long before acting on a blur.
    private static readonly TimeSpan PastBlurDelay = TimeSpan.FromMilliseconds(400);

    [Fact]
    public async Task BlurIsNotATriggerByDefault()
    {
        await RunAsync(async (renderer, input, tags) =>
        {
            await Type(renderer, input, "unfinished");
            await Blur(renderer, input);

            Assert.Empty(tags.Current);
        });
    }

    [Fact]
    public async Task WithBlurSetLeavingTheInputAddsTheTypedText()
    {
        await RunAsync(async (renderer, input, tags) =>
        {
            await Type(renderer, input, "unfinished");
            await Blur(renderer, input);

            Assert.Equal(["unfinished"], tags.Current);
        }, TagInputTrigger.Enter | TagInputTrigger.Blur);
    }

    [Fact]
    public async Task FocusReturningBeforeTheDelayCancelsTheCommit()
    {
        await RunAsync(async (renderer, input, tags) =>
        {
            await Type(renderer, input, "still typing");

            // Blur then immediately refocus — a click inside the control, not a departure.
            var blur = renderer.Dispatcher.InvokeAsync(() => ComponentProbe.Call(input, "HandleBlur") as Task ?? Task.CompletedTask);
            await renderer.Dispatcher.InvokeAsync(() => ComponentProbe.Call(input, "HandleFocus"));
            await Task.Delay(PastBlurDelay);
            await blur;

            Assert.Empty(tags.Current);
        }, TagInputTrigger.Enter | TagInputTrigger.Blur);
    }

    [Fact]
    public async Task EmptyInputAddsNothing()
    {
        await RunAsync(async (renderer, input, tags) =>
        {
            await Type(renderer, input, "   ");
            await Blur(renderer, input);

            Assert.Empty(tags.Current);
        }, TagInputTrigger.Enter | TagInputTrigger.Blur);
    }

    [Fact]
    public async Task RejectedTextIsReportedAndKeptInTheInput()
    {
        var rejected = new List<string>();

        await RunAsync(async (renderer, input, tags) =>
        {
            await Type(renderer, input, "nope");
            await Blur(renderer, input);

            Assert.Empty(tags.Current);
            Assert.Equal(["nope"], rejected);

            // Left in the box rather than silently dropped, exactly as a key commit leaves it.
            Assert.Equal("nope", ComponentProbe.Field<string>(input, "_inputText"));
        },
        TagInputTrigger.Enter | TagInputTrigger.Blur,
        parameters => parameters[nameof(BbTagInput.Validate)] = (Func<string, bool>)(_ => false),
        onRejected: rejected.Add);
    }

    [Fact]
    public async Task ADuplicateIsRejectedRatherThanAdded()
    {
        var rejected = new List<string>();

        await RunAsync(async (renderer, input, tags) =>
        {
            await Type(renderer, input, "alpha");
            await Blur(renderer, input);

            // Rejected, so the list never changed and TagsChanged never fired.
            Assert.Equal(["alpha"], rejected);
            Assert.Empty(tags.Current);
        },
        TagInputTrigger.Enter | TagInputTrigger.Blur,
        parameters => parameters[nameof(BbTagInput.Tags)] = (IReadOnlyList<string>)["alpha"],
        onRejected: rejected.Add);
    }

    [Fact]
    public async Task TabCommittingFirstDoesNotAddTheTagTwice()
    {
        // Tab commits on the keystroke and clears the text. The blur that follows must find
        // nothing left to add.
        await RunAsync(async (renderer, input, tags) =>
        {
            await Type(renderer, input, "once");
            await renderer.Dispatcher.InvokeAsync(async () => await input.JsTriggerAdd());
            await Blur(renderer, input);

            Assert.Equal(["once"], tags.Current);
        }, TagInputTrigger.Tab | TagInputTrigger.Blur);
    }

    // -------------------------------------------------------------------------------------

    private sealed class TagSink
    {
        public IReadOnlyList<string> Current { get; private set; } = [];

        public void Set(IReadOnlyList<string>? tags) => Current = tags ?? [];
    }

    private static Task Type(ComponentTestRenderer renderer, BbTagInput input, string text) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await (ComponentProbe.Call(input, "HandleInput", new ChangeEventArgs { Value = text }) as Task
                   ?? Task.CompletedTask));

    private static async Task Blur(ComponentTestRenderer renderer, BbTagInput input)
    {
        var blur = renderer.Dispatcher.InvokeAsync(() =>
            ComponentProbe.Call(input, "HandleBlur") as Task ?? Task.CompletedTask);
        await Task.Delay(PastBlurDelay);
        await blur;
    }

    private static async Task RunAsync(
        Func<ComponentTestRenderer, BbTagInput, TagSink, Task> body,
        TagInputTrigger? trigger = null,
        Action<Dictionary<string, object?>>? configure = null,
        Action<string>? onRejected = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddBlazorBlueprintComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        var sink = new TagSink();
        var parameters = new Dictionary<string, object?>
        {
            [nameof(BbTagInput.TagsChanged)] =
                EventCallback.Factory.Create<IReadOnlyList<string>?>(sink, sink.Set),
        };

        if (trigger is not null)
        {
            parameters[nameof(BbTagInput.AddTrigger)] = trigger.Value;
        }

        if (onRejected is not null)
        {
            parameters[nameof(BbTagInput.OnTagRejected)] =
                EventCallback.Factory.Create<string>(sink, onRejected);
        }

        configure?.Invoke(parameters);

        var input = await renderer.Dispatcher.InvokeAsync(async () => await renderer.MountAsync<BbTagInput>(parameters));
        await body(renderer, input, sink);
    }
}
