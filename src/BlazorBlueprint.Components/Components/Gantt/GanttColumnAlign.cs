namespace BlazorBlueprint.Components;

/// <summary>
/// Which side of its column a cell's content sits on.
/// </summary>
public enum GanttColumnAlign
{
    /// <summary>The reading side — left in a left-to-right page.</summary>
    Start,

    /// <summary>The middle.</summary>
    Center,

    /// <summary>The far side — right in a left-to-right page.</summary>
    End,
}
