namespace BlazorBlueprint.Primitives.Gantt;

/// <summary>
/// Which end of a bar a dependency line touches.
/// </summary>
public enum GanttAnchor
{
    /// <summary>The left-hand end of the bar in a left-to-right chart.</summary>
    Start,

    /// <summary>The right-hand end of the bar in a left-to-right chart.</summary>
    End,
}
