namespace BlazorBlueprint.Primitives.Gantt;

/// <summary>
/// A dependency resolved against the rows on screen: which rows it joins, where along the axis, and
/// which end of each bar it touches.
/// </summary>
/// <param name="Dependency">The dependency this came from.</param>
/// <param name="FromRow">The index of the row the arrow leaves.</param>
/// <param name="ToRow">The index of the row the arrow points at.</param>
/// <param name="FromSlots">The position the arrow leaves from, in minor slots.</param>
/// <param name="ToSlots">The position the arrow points at, in minor slots.</param>
/// <param name="FromAnchor">Which end of the leaving bar the arrow is tied to.</param>
/// <param name="ToAnchor">Which end of the arriving bar the arrow is tied to.</param>
/// <remarks>
/// A dependency on a task whose row is collapsed away is re-tied to the nearest ancestor still on
/// screen, so folding a branch up hides its detail without dropping the arrows that crossed it.
/// </remarks>
public sealed record GanttLink(
    GanttDependency Dependency,
    int FromRow,
    int ToRow,
    double FromSlots,
    double ToSlots,
    GanttAnchor FromAnchor,
    GanttAnchor ToAnchor);
