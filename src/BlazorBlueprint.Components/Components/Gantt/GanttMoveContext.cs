using BlazorBlueprint.Primitives.Gantt;

namespace BlazorBlueprint.Components;

/// <summary>
/// A row a reader has just dragged somewhere else in the task list.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <remarks>
/// Reordering and re-parenting are the same gesture: dropping between two rows makes the task their
/// sibling, dropping onto the middle of one makes it that task's child. Nothing moves until the
/// collection the chart was given comes back changed, because only the caller knows whether the
/// order it keeps is a list, a sort field or a column in a database.
/// </remarks>
public sealed class GanttMoveContext<TItem>
{
    /// <summary>Gets the task being moved.</summary>
    public required TItem Item { get; init; }

    /// <summary>Gets the row being moved.</summary>
    public required GanttRow<TItem> Row { get; init; }

    /// <summary>Gets the row it was dropped on.</summary>
    public required GanttRow<TItem> Target { get; init; }

    /// <summary>Gets where it was dropped relative to the target.</summary>
    public required GanttDropPosition Position { get; init; }

    /// <summary>
    /// Gets the identifier the task should sit under once the move is applied.
    /// </summary>
    /// <remarks>
    /// Worked out from the target and the position, so a handler that only stores a parent
    /// identifier does not have to reason about the two cases itself. Null means the top level.
    /// </remarks>
    public required string? NewParentId { get; init; }

    /// <summary>Gets or sets whether to leave the task where it was.</summary>
    public bool Cancel { get; set; }
}
