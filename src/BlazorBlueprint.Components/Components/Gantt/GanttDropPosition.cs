namespace BlazorBlueprint.Components;

/// <summary>
/// Where a dragged row is being asked to land.
/// </summary>
public enum GanttDropPosition
{
    /// <summary>Directly above the target, as its sibling.</summary>
    Before,

    /// <summary>Directly below the target, as its sibling.</summary>
    After,

    /// <summary>Underneath the target, as its child.</summary>
    Inside,
}
