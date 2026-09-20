using BlazorBlueprint.Primitives.Gantt;

namespace BlazorBlueprint.Components;

/// <summary>
/// A dependency a reader has just drawn between two bars.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <remarks>
/// Nothing is drawn until the new dependency is in the collection the chart was given. Add it, or
/// set <see cref="Cancel"/> and the arrow never appears — which is the hook for refusing a link
/// that would make a loop, or one the plan's own rules do not allow.
/// </remarks>
public sealed class GanttDependencyContext<TItem>
{
    /// <summary>Gets the dependency the drag is asking for.</summary>
    public required GanttDependency Dependency { get; init; }

    /// <summary>Gets the row the arrow would leave.</summary>
    public required GanttRow<TItem> From { get; init; }

    /// <summary>Gets the row the arrow would point at.</summary>
    public required GanttRow<TItem> To { get; init; }

    /// <summary>Gets or sets whether to drop it.</summary>
    public bool Cancel { get; set; }
}
