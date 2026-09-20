namespace BlazorBlueprint.Primitives.Gantt;

/// <summary>
/// How much time one slot of the timeline covers.
/// </summary>
/// <remarks>
/// The zoom picks both tiers of the header at once: the minor tier is the unit named here, and the
/// major tier is the next natural unit up. Every minor slot is drawn the same width, so a February
/// and a March are the same size at <see cref="Month"/> zoom even though one is three days shorter.
/// </remarks>
public enum GanttZoom
{
    /// <summary>Hours under days.</summary>
    Hour,

    /// <summary>Days under months.</summary>
    Day,

    /// <summary>Weeks under months.</summary>
    Week,

    /// <summary>Months under years.</summary>
    Month,

    /// <summary>Quarters under years.</summary>
    Quarter,

    /// <summary>Years, with no tier above them.</summary>
    Year,
}
