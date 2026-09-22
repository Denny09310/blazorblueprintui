namespace BlazorBlueprint.Primitives.Pivot;

/// <summary>
/// One grouping dimension of a pivot table: what to group by, and how to label each group.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <remarks>
/// A field on the row axis becomes a level of row headings, and a field on the column axis becomes
/// a level of column headings. Stack two on an axis and the second nests inside the first.
/// </remarks>
public sealed class PivotField<TItem>
{
    /// <summary>
    /// Creates a field.
    /// </summary>
    /// <param name="key">A stable identifier, unique among the fields of one table.</param>
    /// <param name="title">The heading shown for the level.</param>
    /// <param name="selector">Reads the value to group by from an item.</param>
    public PivotField(string key, string title, Func<TItem, object?> selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(selector);

        Key = key;
        Title = title;
        Selector = selector;
    }

    /// <summary>
    /// Gets the identifier, unique among the fields of one table.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the heading shown for this level.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the function that reads the value to group by.
    /// </summary>
    public Func<TItem, object?> Selector { get; }

    /// <summary>
    /// Gets or sets how a group's value is turned into its heading. Defaults to <c>ToString()</c>.
    /// </summary>
    /// <remarks>
    /// Use this to bucket or to format — a date into a quarter, a number into a band — rather than
    /// grouping on the formatted string, so the groups still sort by their real value.
    /// </remarks>
    public Func<object?, string>? Label { get; set; }

    /// <summary>
    /// Gets or sets how the groups of this level are ordered. Defaults to the value's own order.
    /// </summary>
    public IComparer<object?>? Comparer { get; set; }

    /// <summary>
    /// Gets or sets whether the groups of this level run in reverse order.
    /// </summary>
    public bool Descending { get; set; }
}
