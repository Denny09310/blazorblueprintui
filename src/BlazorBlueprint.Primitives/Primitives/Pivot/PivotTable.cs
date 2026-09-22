namespace BlazorBlueprint.Primitives.Pivot;

/// <summary>
/// A finished cross-tabulation: two trees of headings and the values where they cross.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <remarks>
/// This is the headless result. No markup, no colours, no widths — a renderer walks
/// <see cref="RowRoots"/> and <see cref="ColumnRoots"/> for the headings and
/// <see cref="GetValue"/> for the body. The styled <c>BbPivotDataGrid</c> renders it as a table.
/// </remarks>
public sealed class PivotTable<TItem>
{
    private readonly Dictionary<(int Row, int Column), List<TItem>> cells;
    private readonly object?[,,] values;
    private readonly bool[,,] computed;

    internal PivotTable(
        IReadOnlyList<PivotAxisNode> rowRoots,
        IReadOnlyList<PivotAxisNode> rowLeaves,
        IReadOnlyList<PivotAxisNode> columnRoots,
        IReadOnlyList<PivotAxisNode> columnLeaves,
        IReadOnlyList<PivotMeasure<TItem>> measures,
        IReadOnlyList<PivotField<TItem>> rowFields,
        IReadOnlyList<PivotField<TItem>> columnFields,
        Dictionary<(int Row, int Column), List<TItem>> cells,
        int itemCount)
    {
        RowRoots = rowRoots;
        RowLeaves = rowLeaves;
        ColumnRoots = columnRoots;
        ColumnLeaves = columnLeaves;
        Measures = measures;
        RowFields = rowFields;
        ColumnFields = columnFields;
        ItemCount = itemCount;

        this.cells = cells;
        values = new object?[Math.Max(rowLeaves.Count, 1), Math.Max(columnLeaves.Count, 1), Math.Max(measures.Count, 1)];
        computed = new bool[values.GetLength(0), values.GetLength(1), values.GetLength(2)];
    }

    /// <summary>
    /// Gets the top-level row headings.
    /// </summary>
    public IReadOnlyList<PivotAxisNode> RowRoots { get; }

    /// <summary>
    /// Gets every row heading that addresses a line of cells, in the order they are drawn.
    /// </summary>
    public IReadOnlyList<PivotAxisNode> RowLeaves { get; }

    /// <summary>
    /// Gets the top-level column headings.
    /// </summary>
    public IReadOnlyList<PivotAxisNode> ColumnRoots { get; }

    /// <summary>
    /// Gets every column heading that addresses a line of cells, in the order they are drawn.
    /// </summary>
    public IReadOnlyList<PivotAxisNode> ColumnLeaves { get; }

    /// <summary>
    /// Gets the measures, one value per measure in every cell.
    /// </summary>
    public IReadOnlyList<PivotMeasure<TItem>> Measures { get; }

    /// <summary>
    /// Gets the fields the rows were grouped by, outermost first.
    /// </summary>
    public IReadOnlyList<PivotField<TItem>> RowFields { get; }

    /// <summary>
    /// Gets the fields the columns were grouped by, outermost first.
    /// </summary>
    public IReadOnlyList<PivotField<TItem>> ColumnFields { get; }

    /// <summary>
    /// Gets how many source items went into the table.
    /// </summary>
    public int ItemCount { get; }

    /// <summary>
    /// Gets whether the table has nothing to show.
    /// </summary>
    public bool IsEmpty => RowLeaves.Count == 0 || ColumnLeaves.Count == 0 || Measures.Count == 0;

    /// <summary>
    /// Gets the value where a row and a column cross.
    /// </summary>
    /// <param name="rowLeaf">A heading from <see cref="RowLeaves"/>.</param>
    /// <param name="columnLeaf">A heading from <see cref="ColumnLeaves"/>.</param>
    /// <param name="measure">The index into <see cref="Measures"/>.</param>
    /// <returns>The value, or <see langword="null"/> where no item landed there.</returns>
    /// <remarks>
    /// Values are worked out the first time they are asked for and then kept, so a renderer can
    /// read the same cell more than once without paying for it twice.
    /// </remarks>
    public object? GetValue(PivotAxisNode rowLeaf, PivotAxisNode columnLeaf, int measure)
    {
        ArgumentNullException.ThrowIfNull(rowLeaf);
        ArgumentNullException.ThrowIfNull(columnLeaf);

        if (rowLeaf.LeafIndex < 0 || columnLeaf.LeafIndex < 0 || measure < 0 || measure >= Measures.Count)
        {
            return null;
        }

        var row = rowLeaf.LeafIndex;
        var column = columnLeaf.LeafIndex;

        if (!computed[row, column, measure])
        {
            var items = GetItems(rowLeaf, columnLeaf);
            values[row, column, measure] = items.Count == 0 ? null : Measures[measure].Evaluate(items);
            computed[row, column, measure] = true;
        }

        return values[row, column, measure];
    }

    /// <summary>
    /// Gets the source items behind a cell, which is what a drill-down shows.
    /// </summary>
    /// <param name="rowLeaf">A heading from <see cref="RowLeaves"/>.</param>
    /// <param name="columnLeaf">A heading from <see cref="ColumnLeaves"/>.</param>
    /// <returns>The items, empty where none landed there.</returns>
    /// <remarks>
    /// A total's items are every item under it, not the cells it sums. That is what lets a custom
    /// aggregate work out a ratio or a median for a total rather than averaging averages.
    /// </remarks>
    public IReadOnlyList<TItem> GetItems(PivotAxisNode rowLeaf, PivotAxisNode columnLeaf)
    {
        ArgumentNullException.ThrowIfNull(rowLeaf);
        ArgumentNullException.ThrowIfNull(columnLeaf);

        var rows = Contributors(rowLeaf, RowLeaves);
        var columns = Contributors(columnLeaf, ColumnLeaves);

        if (rows.Count == 1 && columns.Count == 1)
        {
            return cells.TryGetValue((rows[0], columns[0]), out var only) ? only : [];
        }

        var items = new List<TItem>();
        foreach (var row in rows)
        {
            foreach (var column in columns)
            {
                if (cells.TryGetValue((row, column), out var bucket))
                {
                    items.AddRange(bucket);
                }
            }
        }

        return items;
    }

    /// <summary>
    /// Gets the leaves a heading draws its items from: itself, or everything a total covers.
    /// </summary>
    private static List<int> Contributors(PivotAxisNode leaf, IReadOnlyList<PivotAxisNode> allLeaves)
    {
        if (!leaf.IsTotal)
        {
            return leaf.DataIndex >= 0 ? [leaf.DataIndex] : [];
        }

        // A total covers the real leaves that sit under the heading it totals: its parent for a
        // subtotal, and the whole axis for a grand total.
        var scope = leaf.Parent;
        var result = new List<int>();

        foreach (var candidate in allLeaves)
        {
            if (candidate.IsTotal || candidate.DataIndex < 0)
            {
                continue;
            }

            if (scope is null || IsUnder(candidate, scope))
            {
                result.Add(candidate.DataIndex);
            }
        }

        return result;
    }

    private static bool IsUnder(PivotAxisNode node, PivotAxisNode ancestor)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }
}
