namespace BlazorBlueprint.Primitives.Gantt;

/// <summary>
/// Everything <see cref="GanttBuilder"/> needs: where the tasks come from, how to read one, and how
/// the timeline is cut up.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
public sealed class GanttSource<TItem>
{
    /// <summary>Gets the tasks, in any order. The tree is rebuilt from the parent identifiers.</summary>
    public required IEnumerable<TItem> Items { get; init; }

    /// <summary>Gets the reader for a task's identifier. Must be unique across the whole set.</summary>
    public required Func<TItem, string> Id { get; init; }

    /// <summary>Gets the reader for a task's name.</summary>
    public required Func<TItem, string> Text { get; init; }

    /// <summary>Gets the reader for a task's start.</summary>
    public required Func<TItem, DateTimeOffset> Start { get; init; }

    /// <summary>Gets the reader for a task's end.</summary>
    public required Func<TItem, DateTimeOffset> End { get; init; }

    /// <summary>
    /// Gets the reader for the identifier of the task this one sits under. Null, or a reader that
    /// returns null, puts every task at the top level.
    /// </summary>
    /// <remarks>
    /// A parent identifier naming a task that is not in the set is treated as no parent, so a
    /// filtered-down slice of a plan still draws rather than throwing.
    /// </remarks>
    public Func<TItem, string?>? ParentId { get; init; }

    /// <summary>Gets the reader for how far along a task is, from 0 to 1. Values outside are clamped.</summary>
    public Func<TItem, double>? Progress { get; init; }

    /// <summary>
    /// Gets the order tasks under the same parent are drawn in. Null keeps the order they arrived in.
    /// </summary>
    public IComparer<TItem>? Order { get; init; }

    /// <summary>Gets the arrows drawn between tasks.</summary>
    public IEnumerable<GanttDependency>? Dependencies { get; init; }

    /// <summary>Gets how much time one minor slot of the timeline holds.</summary>
    public GanttZoom Zoom { get; init; } = GanttZoom.Day;

    /// <summary>Gets how far past the tasks the timeline is widened.</summary>
    public GanttRangeSnap Snap { get; init; } = GanttRangeSnap.Major;

    /// <summary>Gets an explicit left edge for the timeline. Null takes it from the earliest task.</summary>
    public DateTimeOffset? RangeStart { get; init; }

    /// <summary>Gets an explicit right edge for the timeline. Null takes it from the latest task.</summary>
    public DateTimeOffset? RangeEnd { get; init; }

    /// <summary>Gets the days shaded as non-working. Null shades Saturday and Sunday.</summary>
    public IReadOnlyCollection<DayOfWeek>? NonWorkingDays { get; init; }

    /// <summary>Gets the wording and culture the headings are built with.</summary>
    public GanttLabels? Labels { get; init; }

    /// <summary>
    /// Gets the identifiers of the tasks whose children are hidden.
    /// </summary>
    /// <remarks>
    /// The closed set is named rather than the open one because a plan is read open: an empty set
    /// shows every task, which is the state a reader wants first.
    /// </remarks>
    public IReadOnlySet<string>? Collapsed { get; init; }

    /// <summary>
    /// Gets whether a task with children takes its dates and its progress from them.
    /// </summary>
    /// <remarks>
    /// On by default, because a summary that disagrees with the work under it is a reporting bug
    /// rather than a plan. Turn it off when the summary carries a separately agreed baseline.
    /// </remarks>
    public bool RollUpSummaries { get; init; } = true;
}
