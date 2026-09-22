using System.Globalization;

namespace BlazorBlueprint.Primitives.Gantt;

/// <summary>
/// The wording and the culture the timeline header is built with.
/// </summary>
/// <remarks>
/// Month and day names come from <see cref="Culture"/>. Only the two headings with no date pattern
/// of their own — a week number and a quarter number — are spelled out here.
/// </remarks>
public sealed class GanttLabels
{
    /// <summary>
    /// Gets the shared instance that uses the current culture and English abbreviations.
    /// </summary>
    public static GanttLabels Default { get; } = new();

    /// <summary>
    /// Gets the format a week heading is built with. <c>{0}</c> is the ISO 8601 week number.
    /// </summary>
    public string WeekFormat { get; init; } = "W{0}";

    /// <summary>
    /// Gets the format a quarter heading is built with. <c>{0}</c> is the quarter, 1 to 4.
    /// </summary>
    public string QuarterFormat { get; init; } = "Q{0}";

    /// <summary>
    /// Gets the culture month and day names are taken from. Null uses the current culture.
    /// </summary>
    public CultureInfo? Culture { get; init; }

    /// <summary>
    /// Gets the culture to format with.
    /// </summary>
    internal CultureInfo Resolved => Culture ?? CultureInfo.CurrentCulture;
}
