namespace BlazorBlueprint.Components;

/// <summary>
/// Which rules a pivot table draws between its cells.
/// </summary>
/// <remarks>
/// A cross-tabulation is read in two directions at once, so it needs more rules than a list does.
/// <see cref="Both"/> is the default for that reason; drop to <see cref="Horizontal"/> on a table
/// with few columns, where the vertical rules only add noise.
/// </remarks>
public enum PivotGridLines
{
    /// <summary>Rules both ways. The default, and what a cross-tabulation usually needs.</summary>
    Both,

    /// <summary>Rules between rows only.</summary>
    Horizontal,

    /// <summary>Rules between columns only.</summary>
    Vertical,

    /// <summary>No rules between cells.</summary>
    None,
}
