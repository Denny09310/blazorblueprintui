namespace BlazorBlueprint.Primitives.Gantt;

/// <summary>
/// How far out the charted range is widened past the tasks it holds.
/// </summary>
public enum GanttRangeSnap
{
    /// <summary>Start and end exactly where the earliest and latest task do.</summary>
    None,

    /// <summary>Widen to whole minor slots — a whole day, week or month.</summary>
    Minor,

    /// <summary>Widen to whole major slots — a whole month, quarter or year.</summary>
    Major,
}
