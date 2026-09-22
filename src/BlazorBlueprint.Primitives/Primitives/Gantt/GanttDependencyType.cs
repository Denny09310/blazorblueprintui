namespace BlazorBlueprint.Primitives.Gantt;

/// <summary>
/// Which end of each task a dependency ties together.
/// </summary>
/// <remarks>
/// The name reads as the predecessor's end first: <see cref="FinishToStart"/> runs from the
/// finish of the task it comes from to the start of the task it points at, which is the one
/// almost every plan means by "this has to happen first".
/// </remarks>
public enum GanttDependencyType
{
    /// <summary>The predecessor finishes before the successor starts.</summary>
    FinishToStart,

    /// <summary>The two start together.</summary>
    StartToStart,

    /// <summary>The two finish together.</summary>
    FinishToFinish,

    /// <summary>The predecessor starts before the successor finishes.</summary>
    StartToFinish,
}
