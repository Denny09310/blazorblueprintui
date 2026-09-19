using System.Globalization;

namespace BlazorBlueprint.Components;

/// <summary>
/// The type-free half of drawing day-spanning event bars: packing runs into non-overlapping lanes,
/// and placing one run across an N-column day grid.
/// </summary>
/// <remarks>
/// <para>
/// Shared by <see cref="BbEventCalendar{TEvent}"/> and <c>BbScheduler</c>. Only the parts that do
/// not depend on the event type live here. Each component keeps its own run-ordering rule and its
/// own bar record, because those need the event: <c>BbEventCalendar</c> reads the consumer's
/// <c>EventStart</c> / <c>EventEnd</c> delegates, while the scheduler works from a materialised
/// <c>SchedulerOccurrence</c>. Generalising over that would drag one component's accessors into
/// the other for no gain.
/// </para>
/// </remarks>
internal static class DayBandLayout
{
    /// <summary>Lane pitch in pixels — one bar plus the gap below it.</summary>
    internal const int LaneHeightPx = 22;

    /// <summary>Bar height in pixels.</summary>
    internal const int BarHeightPx = 20;

    /// <summary>
    /// Pixels trimmed from whichever end of a bar is a real start or finish rather than a join
    /// into an adjacent row.
    /// </summary>
    internal const int BarInsetPx = 3;

    /// <summary>
    /// Takes the topmost lane whose columns are all free, adding one if none is, and marks the
    /// claimed columns.
    /// </summary>
    /// <param name="occupied">
    /// Lane occupancy, one <c>bool[columns]</c> per lane. Grows as lanes are claimed. Pass the
    /// same list for every run in a row, and a fresh list per row.
    /// </param>
    /// <param name="columns">Columns in the row. Seven for a week; a day count elsewhere.</param>
    /// <param name="startColumn">Zero-based column the run starts in.</param>
    /// <param name="span">How many columns the run covers.</param>
    /// <returns>The zero-based lane the run was placed in.</returns>
    internal static int Claim(List<bool[]> occupied, int columns, int startColumn, int span)
    {
        ArgumentNullException.ThrowIfNull(occupied);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        ArgumentOutOfRangeException.ThrowIfNegative(startColumn);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(span);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startColumn + span, columns);

        for (var lane = 0; ; lane++)
        {
            if (lane == occupied.Count)
            {
                occupied.Add(new bool[columns]);
            }

            var taken = occupied[lane];
            var free = true;
            for (var column = startColumn; column < startColumn + span; column++)
            {
                if (taken[column])
                {
                    free = false;
                    break;
                }
            }

            if (!free)
            {
                continue;
            }

            for (var column = startColumn; column < startColumn + span; column++)
            {
                taken[column] = true;
            }

            return lane;
        }
    }

    /// <summary>
    /// Places one bar across the row it belongs to.
    /// </summary>
    /// <remarks>
    /// The row is an N-column grid with a 1px gap, so a column is <c>(100% - (N-1)px) / N</c> wide
    /// and column <c>s</c> starts <c>s</c> gaps in. Expressing that as a <c>calc()</c> keeps the
    /// bar exactly on the column boundaries at any width, with no measurement and no JavaScript.
    /// </remarks>
    /// <param name="startColumn">Zero-based column the run starts in.</param>
    /// <param name="span">How many columns the run covers.</param>
    /// <param name="columns">Columns in the row.</param>
    /// <param name="lane">Zero-based lane, from <see cref="Claim"/>.</param>
    /// <param name="topBasePx">Offset of the first lane from the top of the row.</param>
    /// <param name="continuesBefore">The run began in an earlier row; do not inset its start.</param>
    /// <param name="continuesAfter">The run carries on into a later row; do not inset its end.</param>
    internal static string BarStyle(
        int startColumn,
        int span,
        int columns,
        int lane,
        int topBasePx,
        bool continuesBefore,
        bool continuesAfter)
    {
        var startInset = continuesBefore ? 0 : BarInsetPx;
        var endInset = continuesAfter ? 0 : BarInsetPx;
        var top = topBasePx + (lane * LaneHeightPx);
        var gaps = columns - 1;

        // The gaps: a bar starting in column s clears s of them, and one spanning n columns swallows
        // n - 1. The insets then trim whichever end is a real start or finish rather than a join.
        var left = SignedPixels(startColumn + startInset);
        var width = SignedPixels(span - 1 - startInset - endInset);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"left:calc((100% - {gaps}px) * {startColumn} / {columns} {left});width:calc((100% - {gaps}px) * {span} / {columns} {width});top:{top}px;height:{BarHeightPx}px;");
    }

    /// <summary>
    /// Reserves the height the bars float above, so content below them is never overlapped. Every
    /// cell in a row reserves the same height, which keeps that content on one baseline.
    /// </summary>
    internal static string LaneSpacerStyle(int laneCount) =>
        string.Create(CultureInfo.InvariantCulture, $"height:{laneCount * LaneHeightPx}px;");

    /// <summary>
    /// Squares off whichever end of a bar runs on into an adjacent row, so the two halves read as
    /// one bar rather than two.
    /// </summary>
    internal static string? JoinRounding(bool continuesBefore, bool continuesAfter) =>
        (continuesBefore, continuesAfter) switch
        {
            (true, true) => "bb:rounded-none",
            (true, false) => "bb:rounded-l-none",
            (false, true) => "bb:rounded-r-none",
            _ => null,
        };

    /// <summary>
    /// A signed pixel term for a <c>calc()</c>. Written as <c>- 4px</c> rather than <c>+ -4px</c>,
    /// which is legal but reads like a mistake in devtools.
    /// </summary>
    private static string SignedPixels(int value) =>
        string.Create(CultureInfo.InvariantCulture, $"{(value < 0 ? '-' : '+')} {Math.Abs(value)}px");
}
