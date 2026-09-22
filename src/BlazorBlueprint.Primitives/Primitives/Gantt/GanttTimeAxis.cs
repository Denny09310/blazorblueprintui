using System.Globalization;

namespace BlazorBlueprint.Primitives.Gantt;

/// <summary>
/// The two tiers of headings across the top of a chart, and the mapping from a date to a position
/// along them.
/// </summary>
/// <remarks>
/// <para>
/// Positions are measured in slots, not in days. Every minor slot is one unit wide whatever it
/// holds, so a renderer multiplies by one slot width and is done — and a month zoom does not have
/// to draw February narrower than March to stay honest.
/// </para>
/// <para>
/// The whole axis sits at one UTC offset, taken from the range start. A daylight saving change
/// inside the range shifts nothing, which is what keeps a day column exactly one day wide across
/// the boundary.
/// </para>
/// </remarks>
public sealed class GanttTimeAxis
{
    private readonly double[] positions;

    private GanttTimeAxis(
        DateTimeOffset start,
        DateTimeOffset end,
        GanttZoom zoom,
        IReadOnlyList<GanttSlot> minor,
        IReadOnlyList<GanttSlot> major)
    {
        Start = start;
        End = end;
        Zoom = zoom;
        Minor = minor;
        Major = major;

        // Slot boundaries as ticks, so Position can binary-search instead of walking.
        positions = new double[minor.Count + 1];
        for (var i = 0; i < minor.Count; i++)
        {
            positions[i] = minor[i].Start.UtcTicks;
        }

        positions[minor.Count] = minor.Count > 0 ? minor[^1].End.UtcTicks : start.UtcTicks;
    }

    /// <summary>Gets the instant the charted range opens at.</summary>
    public DateTimeOffset Start { get; }

    /// <summary>Gets the instant the charted range closes at.</summary>
    public DateTimeOffset End { get; }

    /// <summary>Gets the zoom the tiers were built for.</summary>
    public GanttZoom Zoom { get; }

    /// <summary>Gets the lower tier — the slots a position is measured in.</summary>
    public IReadOnlyList<GanttSlot> Minor { get; }

    /// <summary>
    /// Gets the upper tier. Empty at <see cref="GanttZoom.Year"/>, which has no unit above it.
    /// </summary>
    public IReadOnlyList<GanttSlot> Major { get; }

    /// <summary>Gets how many minor slots the axis is wide.</summary>
    public int SlotCount => Minor.Count;

    /// <summary>
    /// Turns an instant into a position along the axis, measured in minor slots.
    /// </summary>
    /// <param name="value">The instant.</param>
    /// <returns>
    /// A fractional slot index, clamped to the charted range. Zero is the left edge and
    /// <see cref="SlotCount"/> is the right edge.
    /// </returns>
    public double Position(DateTimeOffset value)
    {
        if (Minor.Count == 0)
        {
            return 0;
        }

        double ticks = value.UtcTicks;
        if (ticks <= positions[0])
        {
            return 0;
        }

        if (ticks >= positions[^1])
        {
            return Minor.Count;
        }

        // Upper bound: the first boundary past the value, so index - 1 is the slot holding it.
        var low = 0;
        var high = positions.Length - 1;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (positions[mid] <= ticks)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        var slot = low - 1;
        var span = positions[slot + 1] - positions[slot];
        return span <= 0 ? slot : slot + ((ticks - positions[slot]) / span);
    }

    /// <summary>
    /// Turns a position along the axis back into an instant.
    /// </summary>
    /// <param name="slots">A fractional slot index.</param>
    /// <returns>The instant at that position, clamped to the charted range.</returns>
    /// <remarks>
    /// The inverse of <see cref="Position"/>. A drag reports how many slots a bar moved, and this
    /// is what turns that back into dates the caller can store.
    /// </remarks>
    public DateTimeOffset At(double slots)
    {
        if (Minor.Count == 0 || slots <= 0)
        {
            return Start;
        }

        if (slots >= Minor.Count)
        {
            return End;
        }

        var index = (int)Math.Floor(slots);
        var slot = Minor[index];
        var within = slots - index;

        return slot.Start + ((slot.End - slot.Start) * within);
    }

    /// <summary>
    /// Builds the tiers covering a range.
    /// </summary>
    /// <param name="start">The earliest instant to cover.</param>
    /// <param name="end">The latest instant to cover.</param>
    /// <param name="zoom">How much time one minor slot holds.</param>
    /// <param name="snap">How far past the range the tiers are widened.</param>
    /// <param name="nonWorkingDays">The days shaded as non-working. Null shades Saturday and Sunday.</param>
    /// <param name="labels">The wording and culture the headings are built with.</param>
    /// <returns>The axis.</returns>
    public static GanttTimeAxis Build(
        DateTimeOffset start,
        DateTimeOffset end,
        GanttZoom zoom,
        GanttRangeSnap snap = GanttRangeSnap.Major,
        IReadOnlyCollection<DayOfWeek>? nonWorkingDays = null,
        GanttLabels? labels = null)
    {
        labels ??= GanttLabels.Default;
        var culture = labels.Resolved;
        var offset = start.Offset;
        var minorUnit = MinorUnit(zoom);
        var majorUnit = MajorUnit(zoom);

        if (end < start)
        {
            end = start;
        }

        var snapUnit = snap switch
        {
            // Widening to the major unit is what gives a day zoom its whole month. A year is too
            // much: a seven-week plan snapped to one becomes two slots out of twelve, and the work
            // disappears into a sliver. Above a month, widen by the minor unit instead.
            GanttRangeSnap.Major => majorUnit is null or GanttUnit.Year ? minorUnit : majorUnit.Value,
            GanttRangeSnap.Minor => minorUnit,
            _ => (GanttUnit?)null,
        };

        var from = start.DateTime;
        var to = end.DateTime;

        if (snapUnit is { } unit)
        {
            from = Floor(from, unit, culture);
            to = Ceiling(to, unit, culture);
        }

        // Whole minor slots regardless, or the last slot would be clipped and every position in it
        // would read short.
        from = Floor(from, minorUnit, culture);
        to = Ceiling(to, minorUnit, culture);

        if (to <= from)
        {
            to = Add(from, minorUnit, 1);
        }

        var working = nonWorkingDays ?? [DayOfWeek.Saturday, DayOfWeek.Sunday];
        var shadeable = minorUnit is GanttUnit.Hour or GanttUnit.Day;

        var minor = new List<GanttSlot>();
        for (var cursor = from; cursor < to; cursor = Add(cursor, minorUnit, 1))
        {
            var next = Add(cursor, minorUnit, 1);
            minor.Add(new GanttSlot(
                new DateTimeOffset(cursor, offset),
                new DateTimeOffset(next, offset),
                Label(cursor, minorUnit, culture, labels),
                minor.Count,
                1,
                shadeable && working.Contains(cursor.DayOfWeek)));
        }

        var major = majorUnit is { } up ? Group(minor, up, offset, culture, labels) : [];

        return new GanttTimeAxis(new DateTimeOffset(from, offset), new DateTimeOffset(to, offset), zoom, minor, major);
    }

    private static List<GanttSlot> Group(
        List<GanttSlot> minor,
        GanttUnit unit,
        TimeSpan offset,
        CultureInfo culture,
        GanttLabels labels)
    {
        var major = new List<GanttSlot>();
        var index = 0;

        while (index < minor.Count)
        {
            // A major slot covers whole minor slots by construction: it is named after the unit
            // its first minor slot falls in, and runs until a minor slot falls in the next one.
            var head = minor[index].Start.DateTime;
            var bucket = Floor(head, unit, culture);
            var boundary = Add(bucket, unit, 1);
            var span = 0;

            while (index + span < minor.Count && minor[index + span].Start.DateTime < boundary)
            {
                span++;
            }

            major.Add(new GanttSlot(
                minor[index].Start,
                minor[index + span - 1].End,
                Label(bucket, unit, culture, labels),
                major.Count,
                span,
                IsNonWorking: false));

            index += span;
        }

        return major;
    }

    private static string Label(DateTime value, GanttUnit unit, CultureInfo culture, GanttLabels labels) => unit switch
    {
        GanttUnit.Hour => value.ToString("HH", culture),
        GanttUnit.Day => value.Day.ToString(culture),
        // Numbered from three days in, not from the slot's first day. A week that opens on a
        // Sunday opens inside the ISO week before it, and labelling it from that day names the
        // week the slot mostly is not.
        GanttUnit.Week => string.Format(culture, labels.WeekFormat, ISOWeek.GetWeekOfYear(value.AddDays(3))),
        GanttUnit.Month => value.ToString("MMM", culture),
        GanttUnit.Quarter => string.Format(culture, labels.QuarterFormat, ((value.Month - 1) / 3) + 1),
        _ => value.Year.ToString(culture),
    };

    private static GanttUnit MinorUnit(GanttZoom zoom) => zoom switch
    {
        GanttZoom.Hour => GanttUnit.Hour,
        GanttZoom.Day => GanttUnit.Day,
        GanttZoom.Week => GanttUnit.Week,
        GanttZoom.Month => GanttUnit.Month,
        GanttZoom.Quarter => GanttUnit.Quarter,
        _ => GanttUnit.Year,
    };

    private static GanttUnit? MajorUnit(GanttZoom zoom) => zoom switch
    {
        GanttZoom.Hour => GanttUnit.Day,
        GanttZoom.Day => GanttUnit.Month,
        GanttZoom.Week => GanttUnit.Month,
        GanttZoom.Month => GanttUnit.Year,
        GanttZoom.Quarter => GanttUnit.Year,
        _ => null,
    };

    private static DateTime Floor(DateTime value, GanttUnit unit, CultureInfo culture) => unit switch
    {
        GanttUnit.Hour => new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, value.Kind),
        GanttUnit.Day => value.Date,
        GanttUnit.Week => FloorWeek(value, culture),
        GanttUnit.Month => new DateTime(value.Year, value.Month, 1, 0, 0, 0, value.Kind),
        GanttUnit.Quarter => new DateTime(value.Year, ((value.Month - 1) / 3 * 3) + 1, 1, 0, 0, 0, value.Kind),
        _ => new DateTime(value.Year, 1, 1, 0, 0, 0, value.Kind),
    };

    private static DateTime FloorWeek(DateTime value, CultureInfo culture)
    {
        var first = culture.DateTimeFormat.FirstDayOfWeek;
        var back = ((int)value.DayOfWeek - (int)first + 7) % 7;
        return value.Date.AddDays(-back);
    }

    private static DateTime Ceiling(DateTime value, GanttUnit unit, CultureInfo culture)
    {
        var floor = Floor(value, unit, culture);
        return floor == value ? value : Add(floor, unit, 1);
    }

    private static DateTime Add(DateTime value, GanttUnit unit, int count) => unit switch
    {
        GanttUnit.Hour => value.AddHours(count),
        GanttUnit.Day => value.AddDays(count),
        GanttUnit.Week => value.AddDays(7 * count),
        GanttUnit.Month => value.AddMonths(count),
        GanttUnit.Quarter => value.AddMonths(3 * count),
        _ => value.AddYears(count),
    };

    private enum GanttUnit
    {
        Hour,
        Day,
        Week,
        Month,
        Quarter,
        Year,
    }
}
