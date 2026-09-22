namespace BlazorBlueprint.Primitives.Gantt;

/// <summary>
/// One task as it is drawn: where it sits in the tree, the dates it ended up with, and where its
/// bar falls along the axis.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <remarks>
/// <see cref="Start"/> and <see cref="End"/> are the dates after roll-up, which for a summary task
/// are its children's rather than its own. Read <see cref="Item"/> for what the caller supplied.
/// </remarks>
public sealed class GanttRow<TItem>
{
    internal GanttRow(TItem item, string id, string? parentId, string text, int depth)
    {
        Item = item;
        Id = id;
        ParentId = parentId;
        Text = text;
        Depth = depth;
    }

    /// <summary>Gets the source item this row was built from.</summary>
    public TItem Item { get; }

    /// <summary>Gets the task's identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the identifier of the task this one sits under, or null at the top level.</summary>
    public string? ParentId { get; }

    /// <summary>Gets the task's name.</summary>
    public string Text { get; }

    /// <summary>Gets how deep in the tree the task sits. Zero at the top level.</summary>
    public int Depth { get; }

    /// <summary>Gets the row's position among the rows on screen, counting from zero.</summary>
    public int Index { get; internal set; }

    /// <summary>Gets the date the bar starts at.</summary>
    public DateTimeOffset Start { get; internal set; }

    /// <summary>Gets the date the bar ends at.</summary>
    public DateTimeOffset End { get; internal set; }

    /// <summary>Gets how far along the task is, from 0 to 1.</summary>
    public double Progress { get; internal set; }

    /// <summary>Gets whether the task has children.</summary>
    public bool IsSummary { get; internal set; }

    /// <summary>Gets whether the task takes no time, and is drawn as a marker rather than a bar.</summary>
    public bool IsMilestone { get; internal set; }

    /// <summary>Gets whether the task's children are shown.</summary>
    public bool IsExpanded { get; internal set; }

    /// <summary>Gets how many tasks sit directly under this one.</summary>
    public int ChildCount { get; internal set; }

    /// <summary>Gets where the bar starts, measured in minor slots from the left edge.</summary>
    public double OffsetSlots { get; internal set; }

    /// <summary>Gets how many minor slots wide the bar is. Zero for a milestone.</summary>
    public double LengthSlots { get; internal set; }
}
