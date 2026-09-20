using BlazorBlueprint.Primitives.Pivot;

namespace BlazorBlueprint.Components;

/// <summary>
/// One cell of a pivot table, handed to a template or to a click handler.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <param name="Row">The row heading the cell sits under.</param>
/// <param name="Column">The column heading the cell sits under.</param>
/// <param name="Measure">The measure this cell holds.</param>
/// <param name="MeasureIndex">The measure's position, for reading straight from the table.</param>
/// <param name="Value">The worked-out value, or <see langword="null"/> where nothing landed here.</param>
/// <param name="Text">The value as it is printed, with the measure's format applied.</param>
/// <param name="Table">The whole table, for reading a neighbouring cell or the grand total.</param>
public readonly record struct PivotCellContext<TItem>(
    PivotAxisNode Row,
    PivotAxisNode Column,
    PivotMeasure<TItem> Measure,
    int MeasureIndex,
    object? Value,
    string Text,
    PivotTable<TItem> Table)
{
    /// <summary>
    /// Gets whether this cell sits under a total or a subtotal on either axis.
    /// </summary>
    public bool IsTotal => Row.IsTotal || Column.IsTotal;

    /// <summary>
    /// Gets the source items behind this cell, which is what a drill-down shows.
    /// </summary>
    /// <remarks>
    /// Worked out when asked for rather than held, so a template that never reads it costs nothing.
    /// </remarks>
    public IReadOnlyList<TItem> Items => Table.GetItems(Row, Column);
}
