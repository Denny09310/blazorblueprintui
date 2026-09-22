namespace BlazorBlueprint.Primitives.Gantt;

/// <summary>
/// A tie between two tasks, drawn as an arrow from one bar to the other.
/// </summary>
/// <param name="FromId">The identifier of the task the arrow leaves.</param>
/// <param name="ToId">The identifier of the task the arrow points at.</param>
/// <param name="Type">Which end of each task the arrow touches.</param>
/// <remarks>
/// A dependency is drawn, not enforced. Moving a task does not move what depends on it, because
/// rescheduling a plan is a decision about float, calendars and who is free — not something a
/// chart should do behind the reader's back.
/// </remarks>
public sealed record GanttDependency(
    string FromId,
    string ToId,
    GanttDependencyType Type = GanttDependencyType.FinishToStart);
