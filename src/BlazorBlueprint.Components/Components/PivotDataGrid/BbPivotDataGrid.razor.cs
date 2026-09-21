using System.Globalization;
using BlazorBlueprint.Primitives.Pivot;
using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// A cross-tabulation: one field down the side, another across the top, and a worked-out value
/// where they cross.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <remarks>
/// <para>
/// This is the one grid whose columns come from the data rather than from a declaration. That is
/// the whole difference between it and grouping in <c>BbDataGrid</c>: a grouped grid still has the
/// columns you wrote down, where a pivot grows a column for every value it finds.
/// </para>
/// <para>
/// Declare fields inside <c>Rows</c> and <c>Columns</c> and values inside <c>Values</c>. Which axis
/// a field belongs to comes from the fragment it is written in, so moving a field between rows and
/// columns is moving it in the markup.
/// </para>
/// <para>
/// Totals are worked out from every item under them rather than from the cells they cover. An
/// average of averages is not an average, and that difference is invisible until it is wrong.
/// </para>
/// </remarks>
public partial class BbPivotDataGrid<TItem> : ComponentBase
{
    private readonly List<BbPivotField<TItem>> fields = [];
    private readonly List<BbPivotValue<TItem>> values = [];

    /// <summary>
    /// The keys the field picker has switched off.
    /// </summary>
    /// <remarks>
    /// Kept here rather than on the declarations, because a child's parameter belongs to whoever
    /// wrote the markup: setting it from out here would be undone the next time the parent renders.
    /// </remarks>
    private readonly HashSet<string> hidden = new(StringComparer.Ordinal);

    private PivotTable<TItem>? table;
    private bool stale = true;
    private string? buildError;
    private int page;

    /// <summary>
    /// Gets or sets the items to cross-tabulate.
    /// </summary>
    [Parameter]
    public IEnumerable<TItem>? Data { get; set; }

    /// <summary>
    /// Gets or sets the fields that become levels of row headings, outermost first.
    /// </summary>
    [Parameter]
    public RenderFragment? Rows { get; set; }

    /// <summary>
    /// Gets or sets the fields that become levels of column headings, outermost first.
    /// </summary>
    [Parameter]
    public RenderFragment? Columns { get; set; }

    /// <summary>
    /// Gets or sets the values worked out in each cell. At least one is needed.
    /// </summary>
    [Parameter]
    public RenderFragment? Values { get; set; }

    /// <summary>
    /// Gets or sets which totals and subtotals the table carries.
    /// </summary>
    /// <remarks>
    /// Read the names as totals <em>of</em> the thing named: <c>RowTotals</c> adds a column at the
    /// end of every row, <c>ColumnTotals</c> adds a row at the bottom.
    /// </remarks>
    [Parameter]
    public PivotTotals Totals { get; set; } = PivotTotals.Grand;

    /// <summary>
    /// Gets or sets whether the field picker is shown.
    /// </summary>
    /// <remarks>
    /// The picker turns declared fields and values on and off. A field switched off here is still
    /// declared, so it comes back without touching the markup.
    /// </remarks>
    [Parameter]
    public bool ShowFieldPicker { get; set; }

    /// <summary>
    /// Gets or sets how many top-level row groups a page holds. Null shows them all.
    /// </summary>
    /// <remarks>
    /// Paging works on the outermost row groups rather than on rows, so a group is never split
    /// across a page boundary and its subtotal always lands with it.
    /// </remarks>
    [Parameter]
    public int? PageSize { get; set; }

    /// <summary>
    /// Gets or sets where the pager sits.
    /// </summary>
    [Parameter]
    public PivotPagerPosition PagerPosition { get; set; } = PivotPagerPosition.Bottom;

    /// <summary>
    /// Gets or sets the height of the scrolling area, as a CSS length. Null lets the table grow.
    /// </summary>
    /// <remarks>
    /// Set this and the headings stay put while the body scrolls, which is what makes a wide
    /// cross-tabulation readable.
    /// </remarks>
    [Parameter]
    public string? Height { get; set; }

    /// <summary>
    /// Gets or sets whether every other row is tinted.
    /// </summary>
    [Parameter]
    public bool AlternatingRows { get; set; } = true;

    /// <summary>
    /// Gets or sets which rules are drawn between cells.
    /// </summary>
    [Parameter]
    public PivotGridLines GridLines { get; set; } = PivotGridLines.Both;

    /// <summary>
    /// Gets or sets whether the table is waiting for its data.
    /// </summary>
    [Parameter]
    public bool Loading { get; set; }

    /// <summary>
    /// Gets or sets the message shown when there is nothing to cross-tabulate.
    /// </summary>
    [Parameter]
    public string? EmptyMessage { get; set; }

    /// <summary>
    /// Gets or sets the heading a total carries.
    /// </summary>
    [Parameter]
    public string? TotalLabel { get; set; }

    /// <summary>
    /// Gets or sets the heading a group carries when the value it groups on is empty.
    /// </summary>
    [Parameter]
    public string? BlankLabel { get; set; }

    /// <summary>
    /// Gets or sets the template for the empty box above the row headings.
    /// </summary>
    /// <remarks>Defaults to the row fields' titles, which is what names the rows.</remarks>
    [Parameter]
    public RenderFragment? CornerTemplate { get; set; }

    /// <summary>
    /// Gets or sets the template for a row or column heading.
    /// </summary>
    [Parameter]
    public RenderFragment<PivotHeaderContext>? HeaderTemplate { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when a cell is clicked.
    /// </summary>
    /// <remarks>
    /// Set it and the cells become buttons; the context carries the items behind the cell, which is
    /// what a drill-down shows. Leave it unset and the cells stay plain text.
    /// </remarks>
    [Parameter]
    public EventCallback<PivotCellContext<TItem>> OnCellClick { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked whenever the table is rebuilt.
    /// </summary>
    [Parameter]
    public EventCallback<PivotTable<TItem>?> OnBuilt { get; set; }

    /// <summary>
    /// Gets or sets an accessible name for the table.
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes for the root element.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets additional attributes splatted onto the root element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// Gets the cross-tabulation as last built, or <see langword="null"/> before there is one.
    /// </summary>
    /// <remarks>
    /// Read this from an event handler or from <c>OnAfterRender</c>. Use <see cref="OnBuilt"/> to
    /// show anything from it in a parent's markup, because a parent builds its markup before its
    /// children re-render.
    /// </remarks>
    public PivotTable<TItem>? Table => table;

    /// <summary>
    /// Gets the fields declared on the row axis, in the order they were declared.
    /// </summary>
    internal IReadOnlyList<BbPivotField<TItem>> RowFields =>
        [.. fields.Where(f => f.Axis == PivotAxis.Row)];

    /// <summary>
    /// Gets whether a field takes part, which is its declaration unless the picker says otherwise.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns><see langword="true"/> when the field is shown.</returns>
    internal bool IsShown(BbPivotField<TItem> field) =>
        field.Visible && !hidden.Contains(FieldKey(field));

    /// <summary>
    /// Gets whether a value takes part, which is its declaration unless the picker says otherwise.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the value is shown.</returns>
    internal bool IsShown(BbPivotValue<TItem> value) =>
        value.Visible && !hidden.Contains(ValueKey(value));

    /// <summary>The picker's key for a field, kept apart from a value that shares its title.</summary>
    private static string FieldKey(BbPivotField<TItem> field) =>
        $"{field.Axis}:{field.EffectiveKey}";

    /// <summary>The picker's key for a value.</summary>
    private static string ValueKey(BbPivotValue<TItem> value) => $"value:{value.EffectiveKey}";

    /// <summary>
    /// Gets the fields declared on the column axis, in the order they were declared.
    /// </summary>
    internal IReadOnlyList<BbPivotField<TItem>> ColumnFields =>
        [.. fields.Where(f => f.Axis == PivotAxis.Column)];

    /// <summary>
    /// Gets the values declared for the cells.
    /// </summary>
    internal IReadOnlyList<BbPivotValue<TItem>> ValueColumns => values;

    /// <summary>
    /// Gets the values that are actually shown, which is what the columns are built from.
    /// </summary>
    internal IReadOnlyList<BbPivotValue<TItem>> VisibleValues =>
        [.. values.Where(IsShown)];

    /// <summary>
    /// Adds a field declared in this table's <c>Rows</c> or <c>Columns</c>.
    /// </summary>
    /// <param name="field">The field.</param>
    internal void Register(BbPivotField<TItem> field)
    {
        fields.Add(field);
        Invalidate();
    }

    /// <summary>
    /// Adds a value declared in this table's <c>Values</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    internal void Register(BbPivotValue<TItem> value)
    {
        values.Add(value);
        Invalidate();
    }

    /// <summary>
    /// Takes a field back out.
    /// </summary>
    /// <param name="field">The field.</param>
    internal void Unregister(BbPivotField<TItem> field)
    {
        fields.Remove(field);
        Invalidate();
    }

    /// <summary>
    /// Takes a value back out.
    /// </summary>
    /// <param name="value">The value.</param>
    internal void Unregister(BbPivotValue<TItem> value)
    {
        values.Remove(value);
        Invalidate();
    }

    /// <summary>
    /// Marks the cross-tabulation as needing to be built again.
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

    /// <summary>
    /// Turns a field on or off and rebuilds.
    /// </summary>
    /// <param name="field">The field to toggle.</param>
    internal void ToggleField(BbPivotField<TItem> field)
    {
        var key = FieldKey(field);
        if (!hidden.Remove(key))
        {
            hidden.Add(key);
        }

        Invalidate();
    }

    /// <summary>
    /// Turns a value on or off and rebuilds.
    /// </summary>
    /// <param name="value">The value to toggle.</param>
    internal void ToggleValue(BbPivotValue<TItem> value)
    {
        var key = ValueKey(value);

        // The last one standing stays on: a pivot with no values has nothing in its cells.
        if (!hidden.Contains(key) && VisibleValues.Count == 1)
        {
            return;
        }

        if (!hidden.Remove(key))
        {
            hidden.Add(key);
        }

        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => stale = true;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!stale)
        {
            return;
        }

        var previous = table;
        var previousError = buildError;
        Rebuild();

        // Compared by value, not by reference. Rebuild always makes a new table, so a reference
        // test is always "changed" — and OnBuilt calls StateHasChanged on whoever handles it,
        // whose re-render sets this component's parameters again, which marks it stale again,
        // which rebuilds again. That circle has nothing to stop it, and it hangs the circuit on
        // Server and the tab on WebAssembly.
        var tableChanged = !SameTable(previous, table);
        if (tableChanged || !string.Equals(previousError, buildError, StringComparison.Ordinal))
        {
            if (tableChanged && OnBuilt.HasDelegate)
            {
                await OnBuilt.InvokeAsync(table);
            }

            StateHasChanged();
        }
    }

    /// <summary>
    /// Gets whether two builds draw the same cross-tabulation.
    /// </summary>
    /// <param name="left">The table built last time, which may be null.</param>
    /// <param name="right">The table just built, which may be null.</param>
    /// <returns><see langword="true"/> when nothing a reader or a handler could notice differs.</returns>
    /// <remarks>
    /// <para>
    /// The headings are compared first and the cells only if they match, so the usual case — a
    /// parent re-rendering with nothing changed — costs a walk of the headings rather than of the
    /// grid. Where it does reach the cells it is one more pass over values the rebuild has just
    /// worked out, which is the same order of work as drawing them.
    /// </para>
    /// <para>
    /// The cells have to be included. A pivot whose shape is unchanged but whose numbers moved is
    /// exactly the case a handler is watching for, and a comparison that stopped at the headings
    /// would report nothing had happened.
    /// </para>
    /// </remarks>
    private static bool SameTable(PivotTable<TItem>? left, PivotTable<TItem>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left.ItemCount != right.ItemCount
            || left.RowLeaves.Count != right.RowLeaves.Count
            || left.ColumnLeaves.Count != right.ColumnLeaves.Count
            || left.Measures.Count != right.Measures.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Measures.Count; i++)
        {
            if (!string.Equals(left.Measures[i].Key, right.Measures[i].Key, StringComparison.Ordinal)
                || !string.Equals(left.Measures[i].Title, right.Measures[i].Title, StringComparison.Ordinal)
                || !string.Equals(left.Measures[i].Format, right.Measures[i].Format, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (!SameHeadings(left.RowRoots, right.RowRoots)
            || !SameHeadings(left.ColumnRoots, right.ColumnRoots))
        {
            return false;
        }

        for (var row = 0; row < left.RowLeaves.Count; row++)
        {
            for (var column = 0; column < left.ColumnLeaves.Count; column++)
            {
                for (var measure = 0; measure < left.Measures.Count; measure++)
                {
                    var before = left.GetValue(left.RowLeaves[row], left.ColumnLeaves[column], measure);
                    var after = right.GetValue(right.RowLeaves[row], right.ColumnLeaves[column], measure);

                    if (!Equals(before, after))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static bool SameHeadings(IReadOnlyList<PivotAxisNode> left, IReadOnlyList<PivotAxisNode> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }
        for (var i = 0; i < left.Count; i++)
        {
            if (!Equals(left[i].Key, right[i].Key)
                || !string.Equals(left[i].Label, right[i].Label, StringComparison.Ordinal)
                || left[i].Depth != right[i].Depth
                || left[i].IsTotal != right[i].IsTotal
                || left[i].Span != right[i].Span
                || !SameHeadings(left[i].Children, right[i].Children))
            {
                return false;
            }
        }

        return true;
    }

    private void Rebuild()
    {
        stale = false;
        buildError = null;

        var rowFields = RowFields.Where(IsShown).Select(f => f.ToField()).ToList();
        var columnFields = ColumnFields.Where(IsShown).Select(f => f.ToField()).ToList();
        var measures = VisibleValues.Select(v => v.ToMeasure()).ToList();

        if (Data is null || measures.Count == 0 || (rowFields.Count == 0 && columnFields.Count == 0))
        {
            table = null;
            return;
        }

        try
        {
            table = PivotBuilder.Build(
                Data,
                rowFields,
                columnFields,
                measures,
                Totals,
                TotalLabel ?? Localizer["PivotDataGrid.Total"],
                BlankLabel ?? Localizer["PivotDataGrid.Blank"]);
        }
        catch (ArgumentException error)
        {
            // A table that cannot be built is a declaration problem, not a bug. Say so in the
            // markup rather than throwing, which on Blazor Server would take the circuit down.
            table = null;
            buildError = error.Message;
        }

        page = table is null ? 0 : Math.Clamp(page, 0, Math.Max(0, PageCount - 1));
    }

    /// <summary>
    /// Gets how many pages of top-level row groups there are.
    /// </summary>
    private int PageCount
    {
        get
        {
            if (table is null || PageSize is not > 0)
            {
                return 1;
            }

            // The grand total is not a group and never counts towards a page.
            var groups = table.RowRoots.Count(r => !r.IsTotal);
            return Math.Max(1, (int)Math.Ceiling(groups / (double)PageSize.Value));
        }
    }

    /// <summary>
    /// Gets the row leaves this page shows, with the grand total always kept on the last page.
    /// </summary>
    private IReadOnlyList<PivotAxisNode> PagedRowLeaves
    {
        get
        {
            if (table is null)
            {
                return [];
            }

            if (PageSize is not > 0)
            {
                return table.RowLeaves;
            }

            var groups = table.RowRoots.Where(r => !r.IsTotal).ToList();
            var take = groups.Skip(page * PageSize.Value).Take(PageSize.Value).ToHashSet();

            var leaves = table.RowLeaves.Where(leaf => take.Contains(RootOf(leaf))).ToList();

            if (page == PageCount - 1)
            {
                leaves.AddRange(table.RowLeaves.Where(leaf => leaf.IsTotal && leaf.Parent is null));
            }

            return leaves;
        }
    }

    private static PivotAxisNode RootOf(PivotAxisNode node)
    {
        var current = node;
        while (current.Parent is not null)
        {
            current = current.Parent;
        }

        return current;
    }

    private void GoToPage(int target)
    {
        page = Math.Clamp(target, 0, Math.Max(0, PageCount - 1));
        StateHasChanged();
    }

    /// <summary>
    /// Gets how deep the row headings run, which is how many columns they take.
    /// </summary>
    private int RowDepth => Math.Max(1, RowFields.Count(IsShown));

    /// <summary>
    /// Gets how deep the column headings run, which is how many heading rows they take.
    /// </summary>
    private int ColumnDepth => ColumnFields.Count(IsShown);

    /// <summary>
    /// Gets whether the headings need a last row naming each value.
    /// </summary>
    /// <remarks>
    /// With one value its name belongs in the corner rather than repeated over every column.
    /// </remarks>
    private bool ShowValueHeaderRow => VisibleValues.Count > 1;

    /// <summary>
    /// Gets the column headings at one level, left to right.
    /// </summary>
    private List<PivotAxisNode> ColumnLevel(int depth)
    {
        if (table is null)
        {
            return [];
        }

        var level = new List<PivotAxisNode>();
        Collect(table.ColumnRoots);
        return level;

        void Collect(IEnumerable<PivotAxisNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Depth == depth)
                {
                    level.Add(node);
                }
                else if (node.Depth < depth)
                {
                    Collect(node.Children);
                }
            }
        }
    }

    /// <summary>
    /// Gets the row heading cells to draw for one row, having tracked which ones the rows above
    /// already span.
    /// </summary>
    /// <remarks>
    /// A heading is drawn once and spans every row beneath it, so each row only draws the ancestors
    /// that start on it.
    /// </remarks>
    private List<(PivotAxisNode Node, int RowSpan, int ColumnSpan)> RowHeaderCells(
        PivotAxisNode leaf,
        Dictionary<int, int> remaining)
    {
        var cells = new List<(PivotAxisNode, int, int)>();

        var chain = new List<PivotAxisNode>();
        for (var node = leaf; node is not null; node = node.Parent)
        {
            chain.Insert(0, node);
        }

        foreach (var node in chain)
        {
            var depth = node.Depth;

            if (remaining.TryGetValue(depth, out var left) && left > 0)
            {
                remaining[depth] = left - 1;
                continue;
            }

            // A total sits shallower than the data leaves, so its label takes the columns the
            // levels below it would have used.
            var columnSpan = node.IsLeaf ? Math.Max(1, RowDepth - depth) : 1;

            cells.Add((node, node.Span, columnSpan));
            remaining[depth] = node.Span - 1;

            if (node.IsLeaf)
            {
                for (var deeper = depth + 1; deeper < RowDepth; deeper++)
                {
                    remaining[deeper] = 0;
                }
            }
        }

        return cells;
    }

    /// <summary>
    /// Formats a cell's value with its measure's format string.
    /// </summary>
    private static string Format(object? value, PivotMeasure<TItem> measure)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(measure.Format) && value is IFormattable formattable)
        {
            return formattable.ToString(measure.Format, CultureInfo.CurrentCulture);
        }

        return value.ToString() ?? string.Empty;
    }

    private PivotCellContext<TItem> CellContext(PivotAxisNode row, PivotAxisNode column, int measureIndex)
    {
        var measure = table!.Measures[measureIndex];
        var value = table.GetValue(row, column, measureIndex);

        return new PivotCellContext<TItem>(row, column, measure, measureIndex, value, Format(value, measure), table);
    }

    private async Task OnCellActivatedAsync(PivotCellContext<TItem> context)
    {
        if (OnCellClick.HasDelegate)
        {
            await OnCellClick.InvokeAsync(context);
        }
    }

    private string RootClass => ClassNames.cn("bb:w-full bb:space-y-2", Class);

    private string TableClass => ClassNames.cn(
        "bb:w-full bb:border-collapse bb:text-sm",
        GridLines is PivotGridLines.Both or PivotGridLines.Vertical ? "bb:[&_td]:border-e bb:[&_th]:border-e" : null,
        GridLines is PivotGridLines.Both or PivotGridLines.Horizontal ? "bb:[&_tr]:border-b" : null,
        "bb:[&_td]:border-border bb:[&_th]:border-border bb:[&_tr]:border-border");

    private static string HeaderCellClass(bool isTotal) => ClassNames.cn(
        "bb:bg-muted bb:px-3 bb:py-2 bb:text-start bb:font-medium bb:whitespace-nowrap",
        isTotal ? "bb:font-semibold" : null);

    private string BodyCellClass(PivotCellContext<TItem> context) => ClassNames.cn(
        "bb:px-3 bb:py-1.5 bb:text-end bb:tabular-nums bb:whitespace-nowrap",
        context.IsTotal ? "bb:bg-muted/50 bb:font-semibold" : null,
        OnCellClick.HasDelegate ? "bb:cursor-pointer bb:hover:bg-accent" : null);

    private string RowClass(int index) => ClassNames.cn(
        AlternatingRows && index % 2 == 1 ? "bb:bg-muted/30" : null);
}
