namespace BlazorBlueprint.Primitives.Gantt;

/// <summary>
/// One cell of one tier of the timeline header.
/// </summary>
/// <param name="Start">The instant the slot opens at.</param>
/// <param name="End">The instant the next slot opens at.</param>
/// <param name="Label">The heading shown in the cell.</param>
/// <param name="Index">The slot's position in its own tier, counting from zero.</param>
/// <param name="Span">
/// How many minor slots this one covers. Always one on the minor tier, which makes it the
/// <c>colspan</c> a renderer needs for a major slot without measuring anything.
/// </param>
/// <param name="IsNonWorking">
/// Whether the whole slot falls on a non-working day. Only ever true where a slot is a day or
/// shorter, because no week, month or year is entirely non-working.
/// </param>
public sealed record GanttSlot(
    DateTimeOffset Start,
    DateTimeOffset End,
    string Label,
    int Index,
    int Span,
    bool IsNonWorking);
