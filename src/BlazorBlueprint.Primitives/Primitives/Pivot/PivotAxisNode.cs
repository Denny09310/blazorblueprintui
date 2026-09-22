namespace BlazorBlueprint.Primitives.Pivot;

/// <summary>
/// One heading on a pivot table's row or column axis.
/// </summary>
/// <remarks>
/// The headings form a tree, one level per field on that axis. A renderer walks the tree for the
/// heading cells and uses <see cref="Span"/> for the colspan or rowspan, then walks
/// <see cref="PivotTable{TItem}.RowLeaves"/> and <see cref="PivotTable{TItem}.ColumnLeaves"/> for
/// the body.
/// </remarks>
public sealed class PivotAxisNode
{
    private readonly List<PivotAxisNode> children = [];

    internal PivotAxisNode(object? key, string label, int depth, PivotAxisNode? parent, bool isTotal)
    {
        Key = key;
        Label = label;
        Depth = depth;
        Parent = parent;
        IsTotal = isTotal;
    }

    /// <summary>
    /// Gets the raw value this heading groups on, or <see langword="null"/> on a total.
    /// </summary>
    public object? Key { get; }

    /// <summary>
    /// Gets the text of the heading.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets how far down the axis this heading sits, counting from zero.
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// Gets the heading this one nests inside, or <see langword="null"/> at the top.
    /// </summary>
    public PivotAxisNode? Parent { get; }

    /// <summary>
    /// Gets whether this heading is a subtotal or a grand total rather than a group of the data.
    /// </summary>
    public bool IsTotal { get; }

    /// <summary>
    /// Gets the headings nested inside this one.
    /// </summary>
    public IReadOnlyList<PivotAxisNode> Children => children;

    /// <summary>
    /// Gets whether this heading has nothing nested inside it, so it addresses a line of cells.
    /// </summary>
    public bool IsLeaf => children.Count == 0;

    /// <summary>
    /// Gets how many leaves sit under this heading, which is its colspan or rowspan.
    /// </summary>
    public int Span { get; private set; } = 1;

    /// <summary>
    /// Gets this leaf's position along the axis as drawn, totals included, or -1 where the heading
    /// is not a leaf.
    /// </summary>
    public int LeafIndex { get; internal set; } = -1;

    /// <summary>
    /// Gets this leaf's position among the leaves that hold real data, or -1 on a total or a
    /// heading that is not a leaf.
    /// </summary>
    /// <remarks>
    /// Totals are drawn between the data, so the drawn order and the data order are not the same
    /// sequence. This one addresses the buckets the items were sorted into.
    /// </remarks>
    public int DataIndex { get; internal set; } = -1;

    /// <summary>
    /// Gets the keys from the top of the axis down to this heading.
    /// </summary>
    /// <returns>The keys, outermost first.</returns>
    public IReadOnlyList<object?> Path()
    {
        var path = new List<object?>();
        for (var node = this; node is not null; node = node.Parent)
        {
            path.Insert(0, node.Key);
        }

        return path;
    }

    internal void Add(PivotAxisNode child) => children.Add(child);

    internal void SetSpan(int span) => Span = span;
}
