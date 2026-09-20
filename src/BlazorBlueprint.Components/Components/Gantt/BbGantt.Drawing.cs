using System.Globalization;
using System.Text;
using BlazorBlueprint.Primitives.Gantt;

namespace BlazorBlueprint.Components;

/// <summary>
/// Where everything lands on screen. Worked out in C# from the column widths and the slot width,
/// so nothing has to be measured in the browser and the same numbers hold on Server, on WebAssembly
/// and in print.
/// </summary>
public partial class BbGantt<TItem>
{
    private const int LinkStub = 11;
    private const int ArrowLength = 6;
    private const int ArrowHalfWidth = 4;

    /// <summary>Gets how wide the timeline is drawn, in pixels.</summary>
    private int TimelineWidth => (chart?.Axis.SlotCount ?? 0) * EffectiveSlotWidth;

    /// <summary>Gets how tall the rows are together, in pixels.</summary>
    private int BodyHeight => (chart?.Rows.Count ?? 0) * RowHeight;

    /// <summary>Gets how many tiers the header has.</summary>
    private int HeaderTiers => chart is not null && chart.Axis.Major.Count > 0 ? 2 : 1;

    /// <summary>Gets how tall the whole header is, in pixels.</summary>
    private int HeaderHeight => HeaderTiers * HeaderRowHeight;

    /// <summary>Gets how tall a bar is drawn, in pixels.</summary>
    private int BarHeight => Math.Max(10, RowHeight - 16);

    /// <summary>Gets how tall a summary's bracket is drawn, in pixels.</summary>
    private int SummaryHeight => Math.Max(5, BarHeight / 2);

    /// <summary>Gets the style for the downstroke at each end of a summary's bracket.</summary>
    private string SummaryCapStyle() =>
        $"height:{Px(SummaryHeight + 4)}px;background-color:var(--bb-gantt-bar)";

    /// <summary>Gets how wide a milestone marker is drawn, in pixels.</summary>
    private int MilestoneSize => Math.Max(10, RowHeight - 20);

    private static string Px(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Builds the repeating background that draws one faint line at every slot boundary.
    /// </summary>
    /// <remarks>
    /// A gradient rather than one element per slot: an hour zoom over a quarter is thousands of
    /// boundaries, and thousands of empty divs is a page that scrolls badly for no reason. The
    /// slots are all the same width, which is the whole reason this works.
    /// </remarks>
    private string GridBackground()
    {
        var width = EffectiveSlotWidth;
        return $"background-image:repeating-linear-gradient(to right,color-mix(in oklab,var(--border) 70%,transparent) 0 1px,transparent 1px {width}px)";
    }

    /// <summary>Gets the style that sizes the whole table.</summary>
    private string TableStyle() =>
        $"width:{Px(TreeWidth + TimelineWidth)}px;table-layout:fixed";

    /// <summary>Gets the style that sizes one task list column.</summary>
    /// <param name="column">The column.</param>
    /// <returns>The inline style.</returns>
    private static string ColumnWidthStyle(BbGanttColumn<TItem> column) =>
        $"width:{Px(column.EffectiveWidth)}px";

    /// <summary>Gets the style that sizes one timeline slot.</summary>
    private string SlotWidthStyle() => $"width:{Px(EffectiveSlotWidth)}px";

    /// <summary>Gets the style that pins a task list heading and sets its height.</summary>
    /// <param name="offset">How far the column starts from the task list's edge, in pixels.</param>
    /// <returns>The inline style.</returns>
    private string HeaderCellStyle(int offset) =>
        $"inset-inline-start:{Px(offset)}px;height:{Px(HeaderHeight)}px";

    /// <summary>Gets the style that sets the height of one tier of the timeline header.</summary>
    private string TierStyle() => $"height:{Px(HeaderRowHeight)}px";

    /// <summary>Gets the style for a timeline heading that stands in for both tiers.</summary>
    private string SingleTierStyle() => $"height:{Px(HeaderHeight)}px";

    /// <summary>Gets the style that pins the lower tier under the upper one.</summary>
    private string LowerTierStyle() =>
        $"top:{Px(HeaderRowHeight)}px;height:{Px(HeaderRowHeight)}px";

    /// <summary>
    /// Gets the style that keeps a major heading readable once its own cell has scrolled past.
    /// </summary>
    /// <remarks>
    /// A month can be wider than the window. Without this its name sits at a left edge nobody can
    /// see, and the upper tier reads as a blank strip.
    /// </remarks>
    private string MajorLabelStyle() => $"inset-inline-start:{Px(TreeWidth + 8)}px";

    /// <summary>Gets the style that sizes the box the whole chart is drawn inside.</summary>
    private string ContentStyle() => $"width:{Px(TreeWidth + TimelineWidth)}px";

    /// <summary>
    /// Gets the style for the one layer everything drawn against the axis lives in.
    /// </summary>
    /// <remarks>
    /// Left to right whatever the page's direction is: the bars are placed from the axis start and
    /// the arrows are drawn in an SVG coordinate system that does not mirror.
    /// </remarks>
    private string DrawingLayerStyle() =>
        $"inset-inline-start:{Px(TreeWidth)}px;top:{Px(HeaderHeight)}px;width:{Px(TimelineWidth)}px;" +
        $"height:{Px(BodyHeight)}px;direction:ltr;{GridBackground()}";

    /// <summary>Gets the style that places one band of non-working days.</summary>
    /// <param name="x">The band's left edge, in pixels.</param>
    /// <param name="width">The band's width, in pixels.</param>
    /// <returns>The inline style.</returns>
    private static string BandStyle(double x, double width) =>
        $"left:{Px(x)}px;width:{Px(width)}px";

    /// <summary>Gets the style that places one vertical line.</summary>
    /// <param name="x">The line's position, in pixels.</param>
    /// <returns>The inline style.</returns>
    private static string LineStyle(double x) => $"left:{Px(x)}px";

    /// <summary>Gets the style that sets a row's height.</summary>
    private string RowStyle() => $"height:{Px(RowHeight)}px";

    /// <summary>Gets the style that pins a task list cell and sets its height.</summary>
    /// <param name="offset">How far the column starts from the task list's edge, in pixels.</param>
    /// <returns>The inline style.</returns>
    private string BodyCellStyle(int offset) =>
        $"inset-inline-start:{Px(offset)}px;height:{Px(RowHeight)}px";

    /// <summary>Gets the style that indents a task by how deep in the tree it sits.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The inline style.</returns>
    private static string IndentStyle(GanttRow<TItem> row) =>
        $"padding-inline-start:{Px(row.Depth * 16)}px";

    /// <summary>Gets the style that sizes and paints the done part of a bar.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The inline style.</returns>
    private static string ProgressStyle(GanttRow<TItem> row) =>
        $"width:{Percent(row.Progress)}%;background-color:var(--bb-gantt-bar)";

    /// <summary>
    /// Merges the non-working slots into as few bands as possible.
    /// </summary>
    /// <returns>The left edge and the width of each band, in pixels.</returns>
    private IEnumerable<(double X, double Width)> NonWorkingBands()
    {
        if (chart is null || !ShowNonWorkingDays)
        {
            yield break;
        }

        var slots = chart.Axis.Minor;
        var width = EffectiveSlotWidth;
        var index = 0;

        while (index < slots.Count)
        {
            if (!slots[index].IsNonWorking)
            {
                index++;
                continue;
            }

            // A whole weekend is one band, not two: fewer elements, and no hairline between the
            // Saturday and the Sunday where two translucent fills would overlap.
            var run = 0;
            while (index + run < slots.Count && slots[index + run].IsNonWorking)
            {
                run++;
            }

            yield return (index * width, run * width);
            index += run;
        }
    }

    /// <summary>
    /// Finds the boundaries between the slots of the upper tier.
    /// </summary>
    /// <returns>The left edge of each boundary past the first, in pixels.</returns>
    private IEnumerable<double> MajorLines()
    {
        if (chart is null)
        {
            yield break;
        }

        var width = EffectiveSlotWidth;
        var covered = 0;

        foreach (var slot in chart.Axis.Major)
        {
            covered += slot.Span;
            if (covered < chart.Axis.SlotCount)
            {
                yield return covered * width;
            }
        }
    }

    /// <summary>
    /// Finds where the current instant falls.
    /// </summary>
    /// <returns>The position in pixels, or null where now is outside the charted range.</returns>
    private double? TodayLine()
    {
        if (chart is null || !ShowToday)
        {
            return null;
        }

        var now = DateTimeOffset.Now;
        if (now < chart.Axis.Start || now > chart.Axis.End)
        {
            return null;
        }

        return chart.Axis.Position(now) * EffectiveSlotWidth;
    }

    /// <summary>
    /// Works out where something of a given height sits in a row, measured from the top of the
    /// drawing layer.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="height">How tall the thing is, in pixels.</param>
    /// <returns>The offset from the top of the layer, in pixels.</returns>
    private double Top(GanttRow<TItem> row, int height) =>
        (row.Index * RowHeight) + ((RowHeight - height) / 2.0);

    /// <summary>Gets the style that places and sizes a task's bar.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The inline style.</returns>
    private string BarStyle(GanttRow<TItem> row)
    {
        var width = EffectiveSlotWidth;
        var height = row.IsSummary ? SummaryHeight : BarHeight;
        var left = row.OffsetSlots * width;

        // Never narrower than two pixels: a task that runs for an hour at month zoom still has to
        // be something a reader can see and take hold of.
        var length = Math.Max(row.LengthSlots * width, 2);

        return $"left:{Px(left)}px;width:{Px(length)}px;top:{Px(Top(row, height))}px;height:{height}px";
    }

    /// <summary>Gets the style that places a milestone marker.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The inline style.</returns>
    private string MilestoneStyle(GanttRow<TItem> row)
    {
        var size = MilestoneSize;
        var left = (row.OffsetSlots * EffectiveSlotWidth) - (size / 2.0);

        return $"left:{Px(left)}px;top:{Px(Top(row, size))}px;width:{size}px;height:{size}px";
    }

    /// <summary>Gets the style that places the name written beside a bar.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The inline style.</returns>
    private string LabelStyle(GanttRow<TItem> row)
    {
        var width = EffectiveSlotWidth;
        var end = row.IsMilestone
            ? (row.OffsetSlots * width) + (MilestoneSize / 2.0)
            : (row.OffsetSlots + row.LengthSlots) * width;

        return $"left:{Px(end + 8)}px;top:{Px(row.Index * RowHeight)}px;height:{Px(RowHeight)}px";
    }

    /// <summary>
    /// Routes one dependency arrow.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <returns>The path's <c>d</c> attribute.</returns>
    /// <remarks>
    /// Three segments where the two ends face each other, and five where they do not — a successor
    /// that starts before its predecessor finishes has to be reached by going around the row rather
    /// than straight back through the bar.
    /// </remarks>
    private string LinkPath(GanttLink link)
    {
        var width = EffectiveSlotWidth;
        var x1 = link.FromSlots * width;
        var x2 = link.ToSlots * width;
        var y1 = (link.FromRow * RowHeight) + (RowHeight / 2.0);
        var y2 = (link.ToRow * RowHeight) + (RowHeight / 2.0);

        var leaving = link.FromAnchor == GanttAnchor.End ? 1 : -1;
        var arriving = link.ToAnchor == GanttAnchor.Start ? 1 : -1;
        var out1 = x1 + (leaving * LinkStub);
        var approach = x2 - (arriving * ArrowLength);
        var turn = approach - (arriving * LinkStub);

        var path = new StringBuilder();
        path.Append(CultureInfo.InvariantCulture, $"M {Px(x1)} {Px(y1)}");

        var direct = leaving > 0 ? turn >= out1 : turn <= out1;
        if (direct)
        {
            path.Append(CultureInfo.InvariantCulture, $" L {Px(turn)} {Px(y1)}");
            path.Append(CultureInfo.InvariantCulture, $" L {Px(turn)} {Px(y2)}");
        }
        else
        {
            // Clear of the source bar but short of the row's own border line: routing along the
            // border would draw the long horizontal run straight on top of it, and the arrow would
            // read as going nowhere.
            var between = y1 + ((y2 > y1 ? 1 : -1) * ((BarHeight / 2.0) + 4));
            path.Append(CultureInfo.InvariantCulture, $" L {Px(out1)} {Px(y1)}");
            path.Append(CultureInfo.InvariantCulture, $" L {Px(out1)} {Px(between)}");
            path.Append(CultureInfo.InvariantCulture, $" L {Px(turn)} {Px(between)}");
            path.Append(CultureInfo.InvariantCulture, $" L {Px(turn)} {Px(y2)}");
        }

        path.Append(CultureInfo.InvariantCulture, $" L {Px(approach)} {Px(y2)}");
        return path.ToString();
    }

    /// <summary>
    /// Builds the arrowhead at the end of one dependency.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <returns>The polygon's <c>points</c> attribute.</returns>
    private string LinkArrow(GanttLink link)
    {
        var x2 = link.ToSlots * EffectiveSlotWidth;
        var y2 = (link.ToRow * RowHeight) + (RowHeight / 2.0);
        var arriving = link.ToAnchor == GanttAnchor.Start ? 1 : -1;
        var back = x2 - (arriving * ArrowLength);

        return $"{Px(x2)},{Px(y2)} {Px(back)},{Px(y2 - ArrowHalfWidth)} {Px(back)},{Px(y2 + ArrowHalfWidth)}";
    }

    /// <summary>Gets the classes on the root element.</summary>
    private string RootClass => ClassNames.cn("bb:flex bb:w-full bb:flex-col bb:gap-3", Class);

    /// <summary>Gets the class that puts a cell's content on the right side of its column.</summary>
    /// <param name="column">The column.</param>
    /// <returns>The class.</returns>
    private static string AlignClass(BbGanttColumn<TItem> column) => column.Align switch
    {
        GanttColumnAlign.Center => "bb:text-center",
        GanttColumnAlign.End => "bb:text-end",
        _ => "bb:text-start",
    };

    /// <summary>
    /// Gets the paint for a bar: one custom property every part of it reads.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The style fragment.</returns>
    /// <remarks>
    /// The colour is declared once and the track, the done part and a summary's end strokes all
    /// derive from it. Painting only the track would leave a fully finished task showing none of
    /// its colour, because the done part covers the whole bar.
    /// </remarks>
    private string BarPaint(GanttRow<TItem> row)
    {
        var color = BarColorSelector?.Invoke(row.Item) is { Length: > 0 } custom
            ? custom
            : row.IsSummary || row.IsMilestone ? "var(--foreground)" : "var(--primary)";

        return $";--bb-gantt-bar:{color};background-color:color-mix(in oklab,var(--bb-gantt-bar) 28%,transparent)";
    }

    /// <summary>Gets the paint for a milestone marker, which is solid rather than a track.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The style fragment.</returns>
    private string MilestonePaint(GanttRow<TItem> row)
    {
        var color = BarColorSelector?.Invoke(row.Item) is { Length: > 0 } custom ? custom : "var(--foreground)";

        return $";background-color:{color}";
    }

    /// <summary>Gets the outline that keeps an unstarted bar visible against the grid.</summary>
    private static string BarOutline() =>
        "box-shadow:inset 0 0 0 1px color-mix(in oklab,var(--bb-gantt-bar) 55%,transparent)";

    /// <summary>Turns a share into a percentage for a width.</summary>
    /// <param name="value">The share, from 0 to 1.</param>
    /// <returns>The percentage.</returns>
    private static string Percent(double value) =>
        (Math.Clamp(value, 0, 1) * 100).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Says what a dependency is, for a reader who cannot see the arrow.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <returns>The sentence.</returns>
    private string LinkLabel(GanttLink link)
    {
        var key = link.Dependency.Type switch
        {
            GanttDependencyType.StartToStart => "Gantt.StartToStart",
            GanttDependencyType.FinishToFinish => "Gantt.FinishToFinish",
            GanttDependencyType.StartToFinish => "Gantt.StartToFinish",
            _ => "Gantt.FinishToStart",
        };

        var from = chart?.Rows.ElementAtOrDefault(link.FromRow)?.Text ?? link.Dependency.FromId;
        var to = chart?.Rows.ElementAtOrDefault(link.ToRow)?.Text ?? link.Dependency.ToId;

        return string.Format(CultureInfo.CurrentCulture, Localizer[key], from, to);
    }

    /// <summary>
    /// Says what a bar covers, for a reader who cannot see it.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The sentence.</returns>
    private string BarLabel(GanttRow<TItem> row)
    {
        var culture = CultureInfo.CurrentCulture;
        var pattern = Zoom == GanttZoom.Hour ? "g" : "d";

        if (row.IsMilestone)
        {
            return string.Format(culture, Localizer["Gantt.MilestoneLabel"], row.Text, row.Start.ToString(pattern, culture));
        }

        return string.Format(
            culture,
            Localizer["Gantt.BarLabel"],
            row.Text,
            row.Start.ToString(pattern, culture),
            row.End.ToString(pattern, culture),
            (row.Progress * 100).ToString("0", culture));
    }
}
