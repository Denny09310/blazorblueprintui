using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Gantt;
using BlazorBlueprint.Primitives.Pivot;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorBlueprint.Tests.Rendering;

/// <summary>
/// <c>OnBuilt</c> on <see cref="BbGantt{TItem}"/> and <see cref="BbPivotDataGrid{TItem}"/> must not
/// fire again for a build that produced the same thing.
/// </summary>
/// <remarks>
/// <para>
/// This is a hang, not a wrong number. <c>EventCallback.InvokeAsync</c> calls
/// <c>StateHasChanged</c> on whoever handles it; if that component owns the markup the chart is
/// declared in, its re-render sets the chart's parameters again, which marks it stale, which
/// rebuilds, which fires <c>OnBuilt</c>. Nothing in that circle stops it.
/// </para>
/// <para>
/// Both components guarded on <c>ReferenceEquals</c> against the previous build. Both also build a
/// brand new object every time, so that guard was never once true.
/// </para>
/// </remarks>
public class BuiltCallbackLoopTests
{
    private sealed record Task(string Id, string? ParentId, DateTimeOffset Start, DateTimeOffset End);

    private sealed record Sale(string Region, string Quarter, double Amount);

    /// <summary>How many builds a runaway loop has to exceed to be called one.</summary>
    /// <remarks>
    /// A handful of builds is normal — the first draw, and a settling pass or two after it. A loop
    /// does not settle at all, so any small ceiling separates the two; this one is generous.
    /// </remarks>
    private const int RunawayThreshold = 20;

    [Fact]
    public async System.Threading.Tasks.Task AGanttDoesNotRebuildForeverWhenAParentHandlesOnBuilt()
    {
        var builds = 0;

        await RunAsync<BbGantt<Task>>(
            (builder, host) => BuildGantt(builder, host, () => builds++),
            () => builds);

        Assert.InRange(builds, 1, RunawayThreshold);
    }

    [Fact]
    public async System.Threading.Tasks.Task APivotDoesNotRebuildForeverWhenAParentHandlesOnBuilt()
    {
        var builds = 0;

        await RunAsync<BbPivotDataGrid<Sale>>(
            (builder, host) => BuildPivot(builder, host, () => builds++),
            () => builds);

        Assert.InRange(builds, 1, RunawayThreshold);
    }

    [Fact]
    public async System.Threading.Tasks.Task AGanttStillReportsItsFirstBuild()
    {
        // The fix must not silence the callback altogether. A handler that never hears anything is
        // as broken as one that hears forever, and much easier to ship by accident.
        GanttChart<Task>? reported = null;
        var builds = 0;

        await RunAsync<BbGantt<Task>>(
            (builder, host) => BuildGantt(builder, host, () => builds++, chart => reported = chart),
            () => builds);

        Assert.NotNull(reported);
        Assert.Equal(2, reported.Rows.Count);
    }

    [Fact]
    public async System.Threading.Tasks.Task APivotStillReportsItsFirstBuild()
    {
        PivotTable<Sale>? reported = null;
        var builds = 0;

        await RunAsync<BbPivotDataGrid<Sale>>(
            (builder, host) => BuildPivot(builder, host, () => builds++, table => reported = table),
            () => builds);

        Assert.NotNull(reported);
        Assert.Equal(4, reported.ItemCount);
    }

    // -------------------------------------------------------------------------------------

    private static void BuildGantt(
        RenderTreeBuilder builder,
        LoopHost host,
        Action onBuilt,
        Action<GanttChart<Task>?>? capture = null)
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var tasks = new List<Task>
        {
            new("a", null, start, start.AddDays(5)),
            new("b", null, start.AddDays(2), start.AddDays(9)),
        };

        builder.OpenComponent<BbGantt<Task>>(0);
        builder.AddAttribute(1, nameof(BbGantt<Task>.Data), tasks);
        builder.AddAttribute(2, nameof(BbGantt<Task>.IdSelector), (Func<Task, string>)(t => t.Id));
        builder.AddAttribute(3, nameof(BbGantt<Task>.TextSelector), (Func<Task, string>)(t => t.Id));
        builder.AddAttribute(4, nameof(BbGantt<Task>.StartSelector), (Func<Task, DateTimeOffset>)(t => t.Start));
        builder.AddAttribute(5, nameof(BbGantt<Task>.EndSelector), (Func<Task, DateTimeOffset>)(t => t.End));
        builder.AddAttribute(6, nameof(BbGantt<Task>.ScrollToToday), false);
        // The receiver is the host, so InvokeAsync re-renders it exactly as a page would be
        // re-rendered. A plain delegate here would give the callback no component to notify, and
        // the loop would never start.
        builder.AddAttribute(7, nameof(BbGantt<Task>.OnBuilt),
            EventCallback.Factory.Create<GanttChart<Task>?>(host, chart =>
            {
                onBuilt();
                capture?.Invoke(chart);
            }));
        builder.CloseComponent();
    }

    private static void BuildPivot(
        RenderTreeBuilder builder,
        LoopHost host,
        Action onBuilt,
        Action<PivotTable<Sale>?>? capture = null)
    {
        var sales = new List<Sale>
        {
            new("North", "Q1", 10),
            new("North", "Q2", 20),
            new("South", "Q1", 30),
            new("South", "Q2", 40),
        };

        builder.OpenComponent<BbPivotDataGrid<Sale>>(0);
        builder.AddAttribute(1, nameof(BbPivotDataGrid<Sale>.Data), sales);
        builder.AddAttribute(2, nameof(BbPivotDataGrid<Sale>.Rows), (RenderFragment)(inner =>
        {
            inner.OpenComponent<BbPivotField<Sale>>(0);
            inner.AddAttribute(1, nameof(BbPivotField<Sale>.Title), "Region");
            inner.AddAttribute(2, nameof(BbPivotField<Sale>.Value), (Func<Sale, object?>)(s => s.Region));
            inner.CloseComponent();
        }));
        builder.AddAttribute(3, nameof(BbPivotDataGrid<Sale>.Columns), (RenderFragment)(inner =>
        {
            inner.OpenComponent<BbPivotField<Sale>>(0);
            inner.AddAttribute(1, nameof(BbPivotField<Sale>.Title), "Quarter");
            inner.AddAttribute(2, nameof(BbPivotField<Sale>.Value), (Func<Sale, object?>)(s => s.Quarter));
            inner.CloseComponent();
        }));
        builder.AddAttribute(4, nameof(BbPivotDataGrid<Sale>.Values), (RenderFragment)(inner =>
        {
            inner.OpenComponent<BbPivotValue<Sale>>(0);
            inner.AddAttribute(1, nameof(BbPivotValue<Sale>.Title), "Amount");
            inner.AddAttribute(2, nameof(BbPivotValue<Sale>.Value), (Func<Sale, object?>)(s => s.Amount));
            inner.CloseComponent();
        }));
        builder.AddAttribute(5, nameof(BbPivotDataGrid<Sale>.OnBuilt),
            EventCallback.Factory.Create<PivotTable<Sale>?>(host, table =>
            {
                onBuilt();
                capture?.Invoke(table);
            }));
        builder.CloseComponent();
    }

    /// <summary>
    /// Renders a host whose own markup declares the component, which is the arrangement that loops.
    /// </summary>
    /// <remarks>
    /// The host re-renders on every callback, exactly as a page would. If the component rebuilds
    /// and reports on every one of those renders, the count climbs without ever settling — so the
    /// test watches the count rather than waiting for a hang it would never come back from.
    /// </remarks>
    private static async System.Threading.Tasks.Task RunAsync<T>(
        Action<RenderTreeBuilder, LoopHost> build,
        Func<int> builds) where T : IComponent
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
            [nameof(LoopHost.Build)] = build,
            [nameof(LoopHost.Ceiling)] = RunawayThreshold,
            [nameof(LoopHost.Builds)] = builds,
        };

        await renderer.Dispatcher.InvokeAsync(async () => await renderer.MountAsync<LoopHost>(parameters));

        // Settling passes. A fixed component reaches a steady state well inside this; a looping one
        // keeps climbing, and the host stops re-rendering once it passes the ceiling so the test
        // finishes rather than hanging with it.
        for (var i = 0; i < 10; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
            await System.Threading.Tasks.Task.Delay(10);
        }
    }

    /// <summary>A parent that owns the markup the component is declared in.</summary>
    private sealed class LoopHost : ComponentBase
    {
        [Parameter]
        public Action<RenderTreeBuilder, LoopHost>? Build { get; set; }

        [Parameter]
        public int Ceiling { get; set; }

        [Parameter]
        public Func<int>? Builds { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            // Stops drawing once the count runs away, so a regression fails the assertion instead
            // of taking the test run with it.
            if (Builds is not null && Builds() > Ceiling)
            {
                return;
            }

            Build?.Invoke(builder, this);
        }
    }
}
