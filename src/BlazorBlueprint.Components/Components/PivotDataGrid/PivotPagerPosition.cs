namespace BlazorBlueprint.Components;

/// <summary>
/// Where the pager sits on a pivot table.
/// </summary>
public enum PivotPagerPosition
{
    /// <summary>Under the table.</summary>
    Bottom,

    /// <summary>Above the table.</summary>
    Top,

    /// <summary>Above and under it, which suits a table too tall to see the ends of at once.</summary>
    Both,
}
