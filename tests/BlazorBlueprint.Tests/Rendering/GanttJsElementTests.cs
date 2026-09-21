using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorBlueprint.Tests.Rendering;

/// <summary>
/// <see cref="BbGantt{TItem}"/> must never hand an unassigned element to its JavaScript.
/// </summary>
/// <remarks>
/// <para>
/// The chart is built in <c>OnAfterRender</c>, so on the pass that builds it the document still
/// shows whatever came before. Declaring columns makes that worse: a column registers during the
/// chart's own render and calls <c>Invalidate</c> afterwards, so a further pass can reach the
/// JavaScript setup between the build and the draw.
/// </para>
/// <para>
/// An <see cref="ElementReference"/> no render has assigned has a null <c>Id</c>. Passing one to
/// JavaScript reaches the module as <c>null</c>, which is where
/// <c>root.addEventListener is not a function</c> and
/// <c>state.containerElement.querySelector is not a function</c> came from — and on Blazor Server
/// the resulting <see cref="JSException"/> is what put a red banner over a chart that had drawn
/// perfectly well.
/// </para>
/// </remarks>
public class GanttJsElementTests
{
    private sealed record Job(string Id, string Name, DateTimeOffset Start, DateTimeOffset End);

    [Fact]
    public async System.Threading.Tasks.Task AChartWithNoColumnsNeverPassesAnUnsetElement()
    {
        var js = await RenderAsync(withColumns: false);

        Assert.Empty(js.UnsetElementCalls);
        Assert.Contains("initialize", js.GoodElementCalls);
    }

    [Fact]
    public async System.Threading.Tasks.Task AChartWithColumnsNeverPassesAnUnsetElement()
    {
        // The case that was reported: a chart with no columns mounted clean, and charts that
        // declared columns did not.
        var js = await RenderAsync(withColumns: true);

        Assert.Empty(js.UnsetElementCalls);
        Assert.Contains("initialize", js.GoodElementCalls);
    }

    [Fact]
    public async System.Threading.Tasks.Task ThreeChartsWithColumnsNeverPassAnUnsetElement()
    {
        // Reported as two and four errors across three charts, so one chart alone was not enough
        // to show it reliably.
        var js = await RenderAsync(withColumns: true, charts: 3);

        Assert.Empty(js.UnsetElementCalls);

        // One per chart, or the guard has quietly turned the gestures off instead of fixing them.
        Assert.Equal(3, js.GoodElementCalls.Count(c => c == "initialize"));
    }

    // -------------------------------------------------------------------------------------

    private static async System.Threading.Tasks.Task<RecordingJavaScript> RenderAsync(
        bool withColumns,
        int charts = 1)
    {
        var js = new RecordingJavaScript();

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime>(js);
        services.AddBlazorBlueprintComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        var parameters = new Dictionary<string, object?>
        {
            [nameof(ChartHost.Charts)] = charts,
            [nameof(ChartHost.WithColumns)] = withColumns,
        };

        await renderer.Dispatcher.InvokeAsync(async () => await renderer.MountAsync<ChartHost>(parameters));

        // Let the build, the invalidation a column causes and the redraw all settle.
        for (var i = 0; i < 6; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
            await System.Threading.Tasks.Task.Delay(10);
        }

        return js;
    }

    private sealed class ChartHost : ComponentBase
    {
        [Parameter]
        public int Charts { get; set; }

        [Parameter]
        public bool WithColumns { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var jobs = new List<Job>
            {
                new("a", "First", start, start.AddDays(4)),
                new("b", "Second", start.AddDays(3), start.AddDays(9)),
            };

            for (var i = 0; i < Charts; i++)
            {
                builder.OpenRegion(i);
                builder.OpenComponent<BbGantt<Job>>(0);
                builder.AddAttribute(1, nameof(BbGantt<Job>.Data), jobs);
                builder.AddAttribute(2, nameof(BbGantt<Job>.IdSelector), (Func<Job, string>)(j => j.Id));
                builder.AddAttribute(3, nameof(BbGantt<Job>.TextSelector), (Func<Job, string>)(j => j.Name));
                builder.AddAttribute(4, nameof(BbGantt<Job>.StartSelector), (Func<Job, DateTimeOffset>)(j => j.Start));
                builder.AddAttribute(5, nameof(BbGantt<Job>.EndSelector), (Func<Job, DateTimeOffset>)(j => j.End));

                // Interaction is what makes the component call initialize at all.
                builder.AddAttribute(6, nameof(BbGantt<Job>.AllowDrag), true);
                builder.AddAttribute(7, nameof(BbGantt<Job>.ScrollToToday), false);

                if (WithColumns)
                {
                    builder.AddAttribute(8, nameof(BbGantt<Job>.Columns), (RenderFragment)(inner =>
                    {
                        inner.OpenComponent<BbGanttColumn<Job>>(0);
                        inner.AddAttribute(1, nameof(BbGanttColumn<Job>.Title), "Task");
                        inner.AddAttribute(2, nameof(BbGanttColumn<Job>.IsTree), true);
                        inner.AddAttribute(3, nameof(BbGanttColumn<Job>.Width), 180);
                        inner.CloseComponent();

                        inner.OpenComponent<BbGanttColumn<Job>>(10);
                        inner.AddAttribute(11, nameof(BbGanttColumn<Job>.Title), "Starts");
                        inner.AddAttribute(12, nameof(BbGanttColumn<Job>.Value), (Func<Job, object?>)(j => j.Start));
                        inner.CloseComponent();
                    }));
                }

                builder.CloseComponent();
                builder.CloseRegion();
            }
        }
    }

    /// <summary>
    /// Records every call and flags the ones handed an element no render has assigned.
    /// </summary>
    private sealed class RecordingJavaScript : IJSRuntime, IJSObjectReference
    {
        public List<string> UnsetElementCalls { get; } = [];

        public List<string> GoodElementCalls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            foreach (var arg in args ?? [])
            {
                if (arg is ElementReference element)
                {
                    (string.IsNullOrEmpty(element.Id) ? UnsetElementCalls : GoodElementCalls)
                        .Add(identifier);
                }
            }

            // Every module request has to come back as something callable, or the component gives
            // up before it reaches the calls this is watching for.
            if (typeof(TValue) == typeof(IJSObjectReference))
            {
                return ValueTask.FromResult((TValue)(object)this);
            }

            return ValueTask.FromResult<TValue>(default!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
