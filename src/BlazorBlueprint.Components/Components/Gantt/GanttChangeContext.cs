using BlazorBlueprint.Primitives.Gantt;

namespace BlazorBlueprint.Components;

/// <summary>
/// What a drag on a bar is asking for.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <remarks>
/// The chart does not change the task itself. Store the new dates and hand back a changed
/// collection, or set <see cref="Cancel"/> and the bar snaps back where it was.
/// </remarks>
public sealed class GanttChangeContext<TItem>
{
    /// <summary>Gets which end of the bar was dragged.</summary>
    public required GanttChangeKind Kind { get; init; }

    /// <summary>Gets the task the bar stands for.</summary>
    public required TItem Item { get; init; }

    /// <summary>Gets the row that was dragged.</summary>
    public required GanttRow<TItem> Row { get; init; }

    /// <summary>Gets the start the drag is asking for.</summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>Gets the end the drag is asking for.</summary>
    public required DateTimeOffset End { get; init; }

    /// <summary>Gets or sets whether to leave the task where it was.</summary>
    public bool Cancel { get; set; }
}
