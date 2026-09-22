namespace BlazorBlueprint.Components;

/// <summary>
/// Which axis of a pivot table a field was declared on.
/// </summary>
public enum PivotAxis
{
    /// <summary>The field becomes a level of row headings.</summary>
    Row,

    /// <summary>The field becomes a level of column headings.</summary>
    Column,
}
