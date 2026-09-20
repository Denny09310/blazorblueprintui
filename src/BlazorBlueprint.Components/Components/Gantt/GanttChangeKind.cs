namespace BlazorBlueprint.Components;

/// <summary>
/// Which part of a bar a drag took hold of.
/// </summary>
public enum GanttChangeKind
{
    /// <summary>The whole bar moved, keeping its length.</summary>
    Move,

    /// <summary>The starting edge moved, changing the length.</summary>
    ResizeStart,

    /// <summary>The finishing edge moved, changing the length.</summary>
    ResizeEnd,
}
