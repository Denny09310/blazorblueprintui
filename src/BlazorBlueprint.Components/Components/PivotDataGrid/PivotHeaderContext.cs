using BlazorBlueprint.Primitives.Pivot;

namespace BlazorBlueprint.Components;

/// <summary>
/// One heading of a pivot table, handed to a header template.
/// </summary>
/// <param name="Node">The heading itself, with its label, its key and what nests inside it.</param>
/// <param name="Axis">Which axis the heading sits on.</param>
public readonly record struct PivotHeaderContext(PivotAxisNode Node, PivotAxis Axis);
