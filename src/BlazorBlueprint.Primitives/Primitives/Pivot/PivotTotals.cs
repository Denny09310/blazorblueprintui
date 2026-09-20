namespace BlazorBlueprint.Primitives.Pivot;

/// <summary>
/// Which total rows and columns a pivot table carries.
/// </summary>
/// <remarks>
/// Read the names as totals <em>of</em> the thing named: <see cref="RowTotals"/> adds a column at
/// the end of every row holding that row's total, and <see cref="ColumnTotals"/> adds a row at the
/// bottom holding each column's total. The subtotal flags do the same one level in, once per parent
/// group, and only do anything where there is more than one field on that axis.
/// </remarks>
[Flags]
public enum PivotTotals
{
    /// <summary>No totals at all.</summary>
    None = 0,

    /// <summary>A total column at the end of every row.</summary>
    RowTotals = 1,

    /// <summary>A total row under every column.</summary>
    ColumnTotals = 2,

    /// <summary>A subtotal column after each group of columns that has children.</summary>
    ColumnSubtotals = 4,

    /// <summary>A subtotal row after each group of rows that has children.</summary>
    RowSubtotals = 8,

    /// <summary>The grand totals only: a column at the end and a row at the bottom.</summary>
    Grand = RowTotals | ColumnTotals,

    /// <summary>Every total and subtotal.</summary>
    All = RowTotals | ColumnTotals | ColumnSubtotals | RowSubtotals,
}
