namespace BlazorBlueprint.Primitives.Gantt;

/// <summary>
/// A built chart: the rows on screen, the axis they are measured against, and the arrows between
/// them. No markup — this is the whole of a Gantt except the drawing.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
public sealed class GanttChart<TItem>
{
    internal GanttChart(
        IReadOnlyList<GanttRow<TItem>> rows,
        GanttTimeAxis axis,
        IReadOnlyList<GanttLink> links,
        int taskCount)
    {
        Rows = rows;
        Axis = axis;
        Links = links;
        TaskCount = taskCount;
    }

    /// <summary>Gets the rows on screen, in the order they are drawn.</summary>
    public IReadOnlyList<GanttRow<TItem>> Rows { get; }

    /// <summary>Gets the timeline the rows are measured against.</summary>
    public GanttTimeAxis Axis { get; }

    /// <summary>Gets the dependencies resolved against the rows on screen.</summary>
    public IReadOnlyList<GanttLink> Links { get; }

    /// <summary>
    /// Gets how many tasks the chart was built from, including those a collapsed branch hides.
    /// </summary>
    public int TaskCount { get; }

    /// <summary>Gets whether there is nothing to draw.</summary>
    public bool IsEmpty => Rows.Count == 0;
}
