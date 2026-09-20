using BlazorBlueprint.Primitives.Gantt;
using BlazorBlueprint.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Components;

/// <summary>
/// A plan drawn against a timeline: a task list down the side, a bar for every task across from it,
/// and arrows for what has to happen first.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <remarks>
/// <para>
/// The task list and the timeline are one table, not two panes kept in step. A Gantt is read across
/// the row — this name, that bar — and two scrolling panes can only ever agree about where a row is
/// by measuring each other. Sharing the row means they cannot disagree.
/// </para>
/// <para>
/// Every position is worked out in C# from the column widths and the slot width, so the chart draws
/// the same on the server, in WebAssembly and in a print stylesheet. Nothing is measured in the
/// browser.
/// </para>
/// <para>
/// A task with children is a summary: it takes its dates and its progress from the work underneath
/// rather than carrying its own. A task with no length is a milestone and is drawn as a marker.
/// </para>
/// </remarks>
public partial class BbGantt<TItem> : ComponentBase, IAsyncDisposable
{
    private readonly List<BbGanttColumn<TItem>> columns = [];

    /// <summary>
    /// The widths a drag has overridden, by column key.
    /// </summary>
    /// <remarks>
    /// Kept here rather than on the declarations: a child's parameter belongs to whoever wrote the
    /// markup, so setting it from out here would be undone the next time the parent renders.
    /// </remarks>
    private readonly Dictionary<string, int> widths = new(StringComparer.Ordinal);

    private readonly string chartId = $"bb-gantt-{Guid.NewGuid():N}";

    private HashSet<string> collapsed = new(StringComparer.Ordinal);
    private GanttChart<TItem>? chart;
    private ElementReference rootElement;
    private DotNetObjectReference<BbGantt<TItem>>? selfRef;
    private IJSObjectReference? columnsModule;
    private IJSObjectReference? ganttModule;
    private bool jsReady;
    private bool seeded;
    private bool stale = true;
    private bool scrolled;
    private string? buildError;
    private string? sortKey;
    private bool sortDescending;

    /// <summary>Gets or sets the tasks, in any order.</summary>
    [Parameter]
    public IEnumerable<TItem>? Data { get; set; }

    /// <summary>Gets or sets the reader for a task's identifier. Must be unique across the set.</summary>
    [Parameter]
    [EditorRequired]
    public Func<TItem, string> IdSelector { get; set; } = default!;

    /// <summary>Gets or sets the reader for a task's name.</summary>
    [Parameter]
    [EditorRequired]
    public Func<TItem, string> TextSelector { get; set; } = default!;

    /// <summary>Gets or sets the reader for a task's start.</summary>
    [Parameter]
    [EditorRequired]
    public Func<TItem, DateTimeOffset> StartSelector { get; set; } = default!;

    /// <summary>Gets or sets the reader for a task's end.</summary>
    [Parameter]
    [EditorRequired]
    public Func<TItem, DateTimeOffset> EndSelector { get; set; } = default!;

    /// <summary>
    /// Gets or sets the reader for the identifier of the task this one sits under. Leave it unset
    /// for a flat plan.
    /// </summary>
    [Parameter]
    public Func<TItem, string?>? ParentIdSelector { get; set; }

    /// <summary>Gets or sets the reader for how far along a task is, from 0 to 1.</summary>
    [Parameter]
    public Func<TItem, double>? ProgressSelector { get; set; }

    /// <summary>
    /// Gets or sets the reader for a bar's colour, as any CSS colour. Null uses the theme's.
    /// </summary>
    [Parameter]
    public Func<TItem, string?>? BarColorSelector { get; set; }

    /// <summary>
    /// Gets or sets the columns of the task list. Leave it unset and the chart shows one column of
    /// task names.
    /// </summary>
    [Parameter]
    public RenderFragment? Columns { get; set; }

    /// <summary>Gets or sets the arrows drawn between tasks.</summary>
    [Parameter]
    public IEnumerable<GanttDependency>? Dependencies { get; set; }

    /// <summary>Gets or sets whether the dependency arrows are drawn.</summary>
    [Parameter]
    public bool ShowDependencies { get; set; } = true;

    /// <summary>Gets or sets how much time one slot of the timeline holds.</summary>
    [Parameter]
    public GanttZoom Zoom { get; set; } = GanttZoom.Day;

    /// <summary>Gets or sets the callback fired when the zoom changes.</summary>
    [Parameter]
    public EventCallback<GanttZoom> ZoomChanged { get; set; }

    /// <summary>Gets or sets whether the toolbar is shown.</summary>
    [Parameter]
    public bool ShowToolbar { get; set; } = true;

    /// <summary>Gets or sets whether the toolbar offers the zoom levels.</summary>
    [Parameter]
    public bool ShowZoomControls { get; set; } = true;

    /// <summary>
    /// Gets or sets how wide one slot is drawn, in pixels. Null takes a width from the zoom.
    /// </summary>
    [Parameter]
    public int? SlotWidth { get; set; }

    /// <summary>Gets or sets an explicit left edge for the timeline.</summary>
    [Parameter]
    public DateTimeOffset? RangeStart { get; set; }

    /// <summary>Gets or sets an explicit right edge for the timeline.</summary>
    [Parameter]
    public DateTimeOffset? RangeEnd { get; set; }

    /// <summary>Gets or sets how far past the tasks the timeline is widened.</summary>
    [Parameter]
    public GanttRangeSnap RangeSnap { get; set; } = GanttRangeSnap.Major;

    /// <summary>Gets or sets the days shaded as non-working. Null shades Saturday and Sunday.</summary>
    [Parameter]
    public IReadOnlyCollection<DayOfWeek>? NonWorkingDays { get; set; }

    /// <summary>Gets or sets whether non-working days are shaded.</summary>
    [Parameter]
    public bool ShowNonWorkingDays { get; set; } = true;

    /// <summary>Gets or sets whether a line marks the current instant.</summary>
    [Parameter]
    public bool ShowToday { get; set; } = true;

    /// <summary>Gets or sets whether the chart scrolls to today the first time it is drawn.</summary>
    [Parameter]
    public bool ScrollToToday { get; set; } = true;

    /// <summary>Gets or sets how tall one row is drawn, in pixels.</summary>
    [Parameter]
    public int RowHeight { get; set; } = 36;

    /// <summary>Gets or sets how tall one tier of the header is drawn, in pixels.</summary>
    [Parameter]
    public int HeaderRowHeight { get; set; } = 30;

    /// <summary>
    /// Gets or sets the identifiers of the tasks whose children are hidden.
    /// </summary>
    /// <remarks>
    /// The closed set is named rather than the open one, unlike <c>BbDataGrid</c>'s
    /// <c>ExpandedNodes</c>: a plan is read open, so an empty set is the state a reader wants first.
    /// </remarks>
    [Parameter]
    public HashSet<string>? CollapsedIds { get; set; }

    /// <summary>Gets or sets the callback fired when a branch is opened or closed.</summary>
    [Parameter]
    public EventCallback<HashSet<string>> CollapsedIdsChanged { get; set; }

    /// <summary>Gets or sets whether every branch starts open. Set it false to start folded up.</summary>
    [Parameter]
    public bool DefaultExpandAll { get; set; } = true;

    /// <summary>Gets or sets whether a summary takes its dates and progress from its children.</summary>
    [Parameter]
    public bool RollUpSummaries { get; set; } = true;

    /// <summary>Gets or sets whether a bar can be dragged along the timeline.</summary>
    [Parameter]
    public bool AllowDrag { get; set; }

    /// <summary>Gets or sets whether a bar's edges can be dragged to change its length.</summary>
    [Parameter]
    public bool AllowResize { get; set; }

    /// <summary>
    /// Gets or sets whether a drag lands on whole slots.
    /// </summary>
    /// <remarks>
    /// On by default. A plan agreed in days should not come back with a start at 09:47 because
    /// that is where the pointer was let go.
    /// </remarks>
    [Parameter]
    public bool SnapToSlot { get; set; } = true;

    /// <summary>
    /// Gets or sets the callback fired when a bar is dragged. Set <c>Cancel</c> to refuse it.
    /// </summary>
    /// <remarks>
    /// The chart does not write to the task. Store the new dates and hand back a changed
    /// collection, or the bar snaps straight back.
    /// </remarks>
    [Parameter]
    public EventCallback<GanttChangeContext<TItem>> OnTaskChange { get; set; }

    /// <summary>Gets or sets the callback fired when a task is clicked, in the list or on the bar.</summary>
    [Parameter]
    public EventCallback<TItem> OnTaskClick { get; set; }

    /// <summary>Gets or sets what is drawn inside a bar, replacing the default fill.</summary>
    [Parameter]
    public RenderFragment<GanttRow<TItem>>? BarTemplate { get; set; }

    /// <summary>
    /// Gets or sets whether a task's name is written beside its bar.
    /// </summary>
    /// <remarks>
    /// Off by default. The task list is pinned to the side, so the name is already on the row and a
    /// second copy of it is the first thing to get in the way of the bars.
    /// </remarks>
    [Parameter]
    public bool ShowTaskLabels { get; set; }

    /// <summary>Gets or sets whether a column's edge can be dragged to change its width.</summary>
    [Parameter]
    public bool Resizable { get; set; } = true;

    /// <summary>Gets or sets how narrow a dragged column may get, in pixels.</summary>
    [Parameter]
    public int MinColumnWidth { get; set; } = 60;

    /// <summary>Gets or sets the height of the scrolling area, as a CSS length. Null lets it grow.</summary>
    [Parameter]
    public string? Height { get; set; }

    /// <summary>Gets or sets whether a loading indicator replaces the chart.</summary>
    [Parameter]
    public bool Loading { get; set; }

    /// <summary>Gets or sets the message shown when there is nothing to draw.</summary>
    [Parameter]
    public string? EmptyMessage { get; set; }

    /// <summary>Gets or sets the label a screen reader announces for the chart.</summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>Gets or sets extra classes for the root element.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets the callback fired after the chart is built.
    /// </summary>
    /// <remarks>
    /// Reading <see cref="Chart"/> through <c>@ref</c> in the parent's markup is always one render
    /// behind, because the parent's markup is written before this component has run. Use this.
    /// </remarks>
    [Parameter]
    public EventCallback<GanttChart<TItem>?> OnBuilt { get; set; }

    /// <summary>Gets or sets attributes splatted onto the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject]
    private IJSRuntime Js { get; set; } = default!;

    /// <summary>
    /// Gets the chart as it was last built, or null where there was nothing to build.
    /// </summary>
    public GanttChart<TItem>? Chart => chart;

    /// <summary>Gets the columns of the task list, in the order they were declared.</summary>
    internal IReadOnlyList<BbGanttColumn<TItem>> DeclaredColumns => columns;

    /// <summary>Gets the column that carries the expander and the indent.</summary>
    internal BbGanttColumn<TItem>? TreeColumn =>
        columns.FirstOrDefault(c => c.IsTree) ?? columns.FirstOrDefault();

    /// <summary>Gets how wide the task list is, in pixels.</summary>
    internal int TreeWidth => columns.Sum(c => c.EffectiveWidth);

    /// <summary>Gets how wide one slot is drawn, in pixels.</summary>
    internal int EffectiveSlotWidth => SlotWidth ?? Zoom switch
    {
        GanttZoom.Hour => 36,
        GanttZoom.Day => 34,
        GanttZoom.Week => 58,
        GanttZoom.Month => 74,
        GanttZoom.Quarter => 82,
        _ => 88,
    };

    /// <summary>Gets the width a column is drawn at, which a drag on its edge overrides.</summary>
    /// <param name="column">The column.</param>
    /// <returns>The width in pixels.</returns>
    internal int WidthOf(BbGanttColumn<TItem> column) =>
        widths.TryGetValue(column.EffectiveKey, out var width) ? width : column.Width;

    /// <summary>Adds a column declared in this chart's <c>Columns</c>.</summary>
    /// <param name="column">The column.</param>
    internal void Register(BbGanttColumn<TItem> column)
    {
        columns.Add(column);
        Invalidate();
    }

    /// <summary>Takes a column back out.</summary>
    /// <param name="column">The column.</param>
    internal void Unregister(BbGanttColumn<TItem> column)
    {
        columns.Remove(column);
        Invalidate();
    }

    /// <summary>
    /// Marks the chart as needing to be built again.
    /// </summary>
    /// <remarks>
    /// Building is not free, so it happens once before the next render rather than every time a
    /// child reports a changed parameter.
    /// </remarks>
    internal void Invalidate()
    {
        stale = true;
        StateHasChanged();
    }

    /// <summary>Opens or closes a branch.</summary>
    /// <param name="id">The identifier of the task to open or close.</param>
    internal async Task ToggleAsync(string id)
    {
        if (!collapsed.Remove(id))
        {
            collapsed.Add(id);
        }

        await NotifyCollapsedAsync();
    }

    /// <summary>Opens every branch.</summary>
    public async Task ExpandAllAsync()
    {
        collapsed.Clear();
        await NotifyCollapsedAsync();
    }

    /// <summary>Closes every branch.</summary>
    public async Task CollapseAllAsync()
    {
        collapsed = Summaries();
        await NotifyCollapsedAsync();
    }

    /// <summary>Changes how much time one slot holds.</summary>
    /// <param name="zoom">The new zoom.</param>
    public async Task SetZoomAsync(GanttZoom zoom)
    {
        if (Zoom == zoom)
        {
            return;
        }

        Zoom = zoom;
        scrolled = false;
        Invalidate();

        if (ZoomChanged.HasDelegate)
        {
            await ZoomChanged.InvokeAsync(zoom);
        }
    }

    /// <summary>Scrolls the timeline so that the current instant is in view.</summary>
    public async Task GoToTodayAsync() => await ScrollToAsync(DateTimeOffset.Now);

    /// <summary>Scrolls the timeline so that an instant is in view.</summary>
    /// <param name="value">The instant to scroll to.</param>
    public async Task ScrollToAsync(DateTimeOffset value)
    {
        if (chart is null || ganttModule is null)
        {
            return;
        }

        var x = TreeWidth + (chart.Axis.Position(value) * EffectiveSlotWidth);
        await ganttModule.InvokeVoidAsync("scrollTo", rootElement, x, TreeWidth);
    }

    /// <summary>
    /// Records a column width a drag settled on. Called from JavaScript.
    /// </summary>
    /// <param name="columnId">The key of the column that was dragged.</param>
    /// <param name="dragged">Every managed column's width at the end of the drag.</param>
    [JSInvokable]
    public void OnResizeCompleted(string columnId, Dictionary<string, double> dragged)
    {
        ArgumentNullException.ThrowIfNull(dragged);

        foreach (var (key, width) in dragged)
        {
            widths[key] = (int)Math.Round(width);
        }

        // The table has to be re-measured against the new task list width, or the bars stay where
        // the old one put them.
        Invalidate();
    }

    /// <summary>
    /// Reports a finished drag on a bar. Called from JavaScript.
    /// </summary>
    /// <param name="id">The identifier of the task that was dragged.</param>
    /// <param name="action">Which part of the bar was taken hold of.</param>
    /// <param name="slots">How far it moved, in slots.</param>
    [JSInvokable]
    public async Task OnBarDragged(string id, string action, double slots)
    {
        if (chart is null || slots == 0)
        {
            return;
        }

        var row = chart.Rows.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        if (row is null)
        {
            return;
        }

        var kind = action switch
        {
            "start" => GanttChangeKind.ResizeStart,
            "end" => GanttChangeKind.ResizeEnd,
            _ => GanttChangeKind.Move,
        };

        var from = row.OffsetSlots;
        var to = row.OffsetSlots + row.LengthSlots;

        switch (kind)
        {
            case GanttChangeKind.Move:
                from += slots;
                to += slots;
                break;
            case GanttChangeKind.ResizeStart:
                from = Math.Min(from + slots, to);
                break;
            default:
                to = Math.Max(to + slots, from);
                break;
        }

        var context = new GanttChangeContext<TItem>
        {
            Kind = kind,
            Item = row.Item,
            Row = row,
            Start = chart.Axis.At(from),
            End = chart.Axis.At(to),
        };

        if (OnTaskChange.HasDelegate)
        {
            await OnTaskChange.InvokeAsync(context);
        }

        // Redraw either way: accepted, the caller's new dates are already in Data; refused, the
        // bar has to go back to where the drag started from.
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (CollapsedIds is not null && !ReferenceEquals(CollapsedIds, collapsed))
        {
            collapsed = new HashSet<string>(CollapsedIds, StringComparer.Ordinal);
        }

        stale = true;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var rebuilt = false;

        if (stale)
        {
            var previous = chart;
            Rebuild();
            rebuilt = !ReferenceEquals(previous, chart);

            if (rebuilt)
            {
                if (OnBuilt.HasDelegate)
                {
                    await OnBuilt.InvokeAsync(chart);
                }

                StateHasChanged();
            }
        }

        await SetUpJsAsync(rebuilt || firstRender);
    }

    private async Task SetUpJsAsync(bool redrawn)
    {
        if (chart is null)
        {
            return;
        }

        try
        {
            if (!jsReady)
            {
                selfRef = DotNetObjectReference.Create(this);
                ganttModule = await JsModules.GetAsync(Js, "./_content/BlazorBlueprint.Components/js/gantt.js");
                jsReady = true;
            }

            if (AllowDrag || AllowResize)
            {
                await ganttModule!.InvokeVoidAsync("initialize", rootElement, selfRef);
            }

            // Only after a redraw: on Blazor Server every call here is a circuit round trip, and
            // the handles a render did not replace are already wired up.
            if (redrawn && Resizable && columns.Count > 0)
            {
                columnsModule ??= await JsModules.GetAsync(Js, "./_content/BlazorBlueprint.Components/js/table-columns.js");
                await columnsModule.InvokeVoidAsync("initColumnResize", rootElement, selfRef, chartId, MinColumnWidth);
                await columnsModule.InvokeVoidAsync("setupResizeHandles", chartId);
            }

            if (ScrollToToday && !scrolled)
            {
                scrolled = true;
                await GoToTodayAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The circuit went away mid-render; there is nothing left to wire up.
        }
    }

    private void Rebuild()
    {
        stale = false;
        buildError = null;

        if (Data is null || IdSelector is null || TextSelector is null || StartSelector is null || EndSelector is null)
        {
            chart = null;
            return;
        }

        if (!seeded && !DefaultExpandAll && CollapsedIds is null)
        {
            // Worked out from the parent identifiers rather than from a built chart, so the first
            // render is already folded up instead of opening and then snapping shut.
            collapsed = Summaries();
            seeded = true;
        }

        try
        {
            chart = GanttBuilder.Build(new GanttSource<TItem>
            {
                Items = Data,
                Id = IdSelector,
                ParentId = ParentIdSelector,
                Text = TextSelector,
                Start = StartSelector,
                End = EndSelector,
                Progress = ProgressSelector,
                Order = SortComparer(),
                Dependencies = ShowDependencies ? Dependencies : null,
                Zoom = Zoom,
                Snap = RangeSnap,
                RangeStart = RangeStart,
                RangeEnd = RangeEnd,
                NonWorkingDays = ShowNonWorkingDays ? NonWorkingDays : [],
                Labels = new GanttLabels
                {
                    WeekFormat = Localizer["Gantt.Week"],
                    QuarterFormat = Localizer["Gantt.Quarter"],
                },
                Collapsed = collapsed,
                RollUpSummaries = RollUpSummaries,
            });
        }
        catch (ArgumentException error)
        {
            // A plan that cannot be drawn is a declaration problem, not a bug. Say so in the markup
            // rather than throwing, which on Blazor Server would take the circuit down.
            chart = null;
            buildError = error.Message;
        }
    }

    private HashSet<string> Summaries()
    {
        var parents = new HashSet<string>(StringComparer.Ordinal);
        if (Data is null || ParentIdSelector is null)
        {
            return parents;
        }

        foreach (var item in Data)
        {
            if (ParentIdSelector(item) is { Length: > 0 } parent)
            {
                parents.Add(parent);
            }
        }

        return parents;
    }

    private Comparer<TItem>? SortComparer()
    {
        if (sortKey is null)
        {
            return null;
        }

        var column = columns.FirstOrDefault(c => string.Equals(c.EffectiveKey, sortKey, StringComparison.Ordinal));
        if (column is null)
        {
            return null;
        }

        var read = column.Value ?? (item => TextSelector(item));
        var inner = column.Comparer ?? Comparer<object?>.Default;
        var sign = sortDescending ? -1 : 1;

        return Comparer<TItem>.Create((a, b) => sign * inner.Compare(read(a), read(b)));
    }

    private async Task SortByAsync(BbGanttColumn<TItem> column)
    {
        if (string.Equals(sortKey, column.EffectiveKey, StringComparison.Ordinal))
        {
            // Ascending, then descending, then back to the order the tasks arrived in.
            if (!sortDescending)
            {
                sortDescending = true;
            }
            else
            {
                sortKey = null;
                sortDescending = false;
            }
        }
        else
        {
            sortKey = column.EffectiveKey;
            sortDescending = false;
        }

        Invalidate();
        await Task.CompletedTask;
    }

    private async Task NotifyCollapsedAsync()
    {
        Invalidate();

        if (CollapsedIdsChanged.HasDelegate)
        {
            await CollapsedIdsChanged.InvokeAsync(new HashSet<string>(collapsed, StringComparer.Ordinal));
        }
    }

    private async Task ClickAsync(GanttRow<TItem> row)
    {
        if (OnTaskClick.HasDelegate)
        {
            await OnTaskClick.InvokeAsync(row.Item);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the JavaScript handlers this chart set up.</summary>
    /// <returns>A task that completes when the handlers are gone.</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        try
        {
            if (ganttModule is not null && jsReady)
            {
                await ganttModule.InvokeVoidAsync("dispose", rootElement);
            }
        }
        catch (JSDisconnectedException)
        {
            // The circuit went away first, which already took the handlers with it.
        }
        catch (ObjectDisposedException)
        {
            // Same again, on a runtime that reports it differently.
        }

        selfRef?.Dispose();
        selfRef = null;
    }
}
