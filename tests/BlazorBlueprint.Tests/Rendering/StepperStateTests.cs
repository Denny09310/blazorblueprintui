using BlazorBlueprint.Components;
using BlazorBlueprint.Tests.Performance;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorBlueprint.Tests.Rendering;

/// <summary>
/// <see cref="BbStepper"/> derives each step's state from its position against the active step,
/// and its steps register themselves as the child content renders.
/// <para>
/// Both of those are worth pinning down. A step's state is never stored, so an off-by-one in the
/// comparison would mark the wrong step complete; and because registration happens after the
/// indicator renders, an active index that sits past the end of a list that has not finished
/// registering has to be clamped rather than throw.
/// </para>
/// </summary>
public class StepperStateTests
{
    [Fact]
    public async Task StepsBehindTheActiveOneAreCompleteAndStepsAheadArePending()
    {
        await RunAsync(3, activeStep: 1, stepper =>
        {
            Assert.Equal(StepState.Completed, State(stepper, 0));
            Assert.Equal(StepState.Active, State(stepper, 1));
            Assert.Equal(StepState.Pending, State(stepper, 2));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AnExplicitStateOverridesThePositionOne()
    {
        // Step 0 sits behind the active step, so it would otherwise read as complete.
        await RunAsync(3, activeStep: 2, stepper =>
        {
            Assert.Equal(StepState.Error, State(stepper, 0));
            return Task.CompletedTask;
        },
        stateOverrides: new Dictionary<int, StepState> { [0] = StepState.Error });
    }

    [Fact]
    public async Task AnExplicitStateOverridesTheActiveStepToo()
    {
        await RunAsync(3, activeStep: 1, stepper =>
        {
            Assert.Equal(StepState.Skipped, State(stepper, 1));
            return Task.CompletedTask;
        },
        stateOverrides: new Dictionary<int, StepState> { [1] = StepState.Skipped });
    }

    [Fact]
    public async Task AnActiveStepPastTheEndIsClamped()
    {
        await RunAsync(3, activeStep: 9, stepper =>
        {
            Assert.Equal(StepState.Active, State(stepper, 2));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AStepIsOnlyNavigableWhenTheStepperIsClickable()
    {
        await RunAsync(3, activeStep: 0, stepper =>
        {
            Assert.False(CanNavigate(stepper, 2));
            return Task.CompletedTask;
        });

        await RunAsync(3, activeStep: 0, stepper =>
        {
            Assert.True(CanNavigate(stepper, 2));
            return Task.CompletedTask;
        },
        clickable: true);
    }

    [Fact]
    public async Task ClickingAStepReportsTheNewIndex()
    {
        var reported = new List<int>();

        await RunAsync(3, activeStep: 0, async stepper =>
        {
            await GoTo(stepper, 2);

            Assert.Equal([2], reported);
            Assert.Equal(StepState.Active, State(stepper, 2));
        },
        clickable: true,
        onStepChanged: reported.Add);
    }

    [Fact]
    public async Task ADisabledStepCannotBeActivated()
    {
        var reported = new List<int>();

        await RunAsync(3, activeStep: 0, async stepper =>
        {
            await GoTo(stepper, 1);

            Assert.Empty(reported);
            Assert.Equal(StepState.Active, State(stepper, 0));
        },
        clickable: true,
        disabledSteps: [1],
        onStepChanged: reported.Add);
    }

    [Fact]
    public async Task TheActiveStepCannotBeReActivated()
    {
        var reported = new List<int>();

        await RunAsync(3, activeStep: 1, async stepper =>
        {
            await GoTo(stepper, 1);

            Assert.Empty(reported);
        },
        clickable: true,
        onStepChanged: reported.Add);
    }

    // -------------------------------------------------------------------------------------

    private static StepState State(BbStepper stepper, int index) =>
        (StepState)ComponentProbe.Call(stepper, "GetStepState", index)!;

    private static bool CanNavigate(BbStepper stepper, int index) =>
        (bool)ComponentProbe.Call(stepper, "CanNavigateToStep", index)!;

    private static Task GoTo(BbStepper stepper, int index) =>
        (Task)ComponentProbe.Call(stepper, "GoToStepAsync", index)!;

    private static async Task RunAsync(
        int stepCount,
        int activeStep,
        Func<BbStepper, Task> body,
        bool clickable = false,
        int[]? disabledSteps = null,
        Dictionary<int, StepState>? stateOverrides = null,
        Action<int>? onStepChanged = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddBlazorBlueprintComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        var parameters = new Dictionary<string, object?>
        {
            [nameof(BbStepper.ActiveStep)] = activeStep,
            [nameof(BbStepper.Clickable)] = clickable,
            [nameof(BbStepper.ChildContent)] = (RenderFragment)(builder =>
                BuildSteps(builder, stepCount, disabledSteps ?? [], stateOverrides)),
        };

        if (onStepChanged is not null)
        {
            parameters[nameof(BbStepper.ActiveStepChanged)] =
                EventCallback.Factory.Create(onStepChanged.Target ?? new object(), onStepChanged);
        }

        var stepper = await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.MountAsync<BbStepper>(parameters));

        await renderer.Dispatcher.InvokeAsync(async () => await body(stepper));
    }

    private static void BuildSteps(
        RenderTreeBuilder builder,
        int stepCount,
        int[] disabledSteps,
        Dictionary<int, StepState>? stateOverrides)
    {
        var sequence = 0;

        for (var index = 0; index < stepCount; index++)
        {
            builder.OpenComponent<BbStep>(sequence++);
            builder.AddComponentParameter(sequence++, nameof(BbStep.Title), $"Step {index + 1}");

            if (disabledSteps.Contains(index))
            {
                builder.AddComponentParameter(sequence++, nameof(BbStep.Disabled), true);
            }

            if (stateOverrides is not null && stateOverrides.TryGetValue(index, out var state))
            {
                builder.AddComponentParameter(sequence++, nameof(BbStep.State), state);
            }

            builder.CloseComponent();
        }
    }
}
