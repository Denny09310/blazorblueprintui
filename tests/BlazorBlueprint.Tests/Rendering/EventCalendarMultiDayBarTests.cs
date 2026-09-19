using System.Globalization;
using System.Text.RegularExpressions;
using BlazorBlueprint.Components;
using BlazorBlueprint.Tests.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorBlueprint.Tests.Rendering;

/// <summary>
/// A multi-day event is one bar per week row, not a chip in every day it covers (#544).
/// <para>
/// The bars are positioned against the week row with a <c>calc()</c> on the seven-column grid, so
/// the assertions read the emitted <c>left</c>/<c>width</c> rather than measuring anything: the
/// column index and span are exactly the numbers in that expression.
/// </para>
/// </summary>
public class EventCalendarMultiDayBarTests
{
    // September 2026 starts on a Tuesday, so with a Sunday-first grid the first cell is Aug 30.
    private static readonly DateTime GridStart = new(2026, 8, 30);

    private sealed record Ev(string Title, DateTime Start, DateTime? End);

    private static readonly string[] OverlappingTitles = ["One", "Two", "Three", "Four"];

    [Fact]
    public async Task AMultiDayEventInsideOneWeekIsASingleSpanningBar()
    {
        // Mon 31 Aug to Wed 2 Sep: columns 1..3 of the first row.
        var markup = await RenderAsync([new("Conference", GridStart.AddDays(1), GridStart.AddDays(3))]);
        var bars = Bars(markup, "Conference");

        var bar = Assert.Single(bars);
        Assert.Equal(1, bar.StartColumn);
        Assert.Equal(3, bar.Span);
    }

    [Fact]
    public async Task AnEventCrossingAWeekBoundaryBecomesOneBarPerRow()
    {
        // Fri 4 Sep to Mon 7 Sep: columns 5..6 of row 1, then column 0..1 of row 2.
        var markup = await RenderAsync([new("Conference", GridStart.AddDays(5), GridStart.AddDays(8))]);
        var bars = Bars(markup, "Conference");

        Assert.Equal(2, bars.Count);
        Assert.Equal((5, 2), (bars[0].StartColumn, bars[0].Span));
        Assert.Equal((0, 2), (bars[1].StartColumn, bars[1].Span));
    }

    [Fact]
    public async Task TheJoinBetweenTwoRowsIsSquaredOff()
    {
        var markup = await RenderAsync([new("Conference", GridStart.AddDays(5), GridStart.AddDays(8))]);
        var bars = Bars(markup, "Conference");

        // The first half keeps its left corners and loses its right; the second half is the mirror.
        Assert.Contains("bb:rounded-e-none", bars[0].Classes);
        Assert.DoesNotContain("bb:rounded-s-none", bars[0].Classes);
        Assert.Contains("bb:rounded-s-none", bars[1].Classes);
        Assert.DoesNotContain("bb:rounded-e-none", bars[1].Classes);
    }

    [Fact]
    public async Task BothHalvesNameTheWholeEventNotTheirOwnSegment()
    {
        var markup = await RenderAsync([new("Conference", GridStart.AddDays(5), GridStart.AddDays(8))]);
        var labels = Regex.Matches(markup, @"aria-label=""(Conference[^""]*)""")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Equal(2, labels.Count);
        Assert.Equal(labels[0], labels[1]);
        Assert.Contains("Conference", labels[0]);
    }

    [Fact]
    public async Task OverlappingEventsStackIntoSeparateLanes()
    {
        // Two runs that share Wednesday cannot sit in the same lane.
        var markup = await RenderAsync(
        [
            new("Long", GridStart, GridStart.AddDays(4)),
            new("Short", GridStart.AddDays(2), GridStart.AddDays(3)),
        ]);

        var longBar = Assert.Single(Bars(markup, "Long"));
        var shortBar = Assert.Single(Bars(markup, "Short"));

        // Longest first, so the long run takes the top lane.
        Assert.True(longBar.Top < shortBar.Top, $"long top {longBar.Top} should be above short top {shortBar.Top}");
    }

    [Fact]
    public async Task NonOverlappingEventsShareOneLane()
    {
        // Mon–Tue and Thu–Fri never touch, so they belong on the same line.
        var markup = await RenderAsync(
        [
            new("First", GridStart.AddDays(1), GridStart.AddDays(2)),
            new("Second", GridStart.AddDays(4), GridStart.AddDays(5)),
        ]);

        var first = Assert.Single(Bars(markup, "First"));
        var second = Assert.Single(Bars(markup, "Second"));

        Assert.Equal(first.Top, second.Top);
    }

    [Fact]
    public async Task ASingleDayEventStaysAChipAndIsNotDrawnTwice()
    {
        var markup = await RenderAsync([new("Standup", GridStart.AddDays(1).AddHours(9), GridStart.AddDays(1).AddHours(10))]);

        Assert.Empty(Bars(markup, "Standup"));
        Assert.Single(Regex.Matches(markup, ">Standup<"));
    }

    [Fact]
    public async Task ABarSpendsTheDayBudgetInEveryDayItCovers()
    {
        // MaxEventsPerDay=2, one bar across Mon–Tue, and two single-day events on Monday.
        // The bar takes one of Monday's two slots, so one chip shows and one is counted hidden.
        var monday = GridStart.AddDays(1);
        var markup = await RenderAsync(
            [
                new("Spanning", monday, GridStart.AddDays(2)),
                new("Chip A", monday.AddHours(9), monday.AddHours(10)),
                new("Chip B", monday.AddHours(11), monday.AddHours(12)),
            ],
            maxEventsPerDay: 2);

        Assert.Single(Bars(markup, "Spanning"));
        Assert.Single(Regex.Matches(markup, ">Chip A<"));
        Assert.Empty(Regex.Matches(markup, ">Chip B<"));
    }

    [Fact]
    public async Task ABarPastTheDayBudgetIsDroppedRatherThanOverflowingTheRow()
    {
        // Three overlapping runs need three lanes, but only one is allowed.
        var markup = await RenderAsync(
            [
                new("One", GridStart, GridStart.AddDays(4)),
                new("Two", GridStart, GridStart.AddDays(4)),
                new("Three", GridStart, GridStart.AddDays(4)),
            ],
            maxEventsPerDay: 1);

        Assert.Equal(1, Bars(markup, "One").Count + Bars(markup, "Two").Count + Bars(markup, "Three").Count);
    }

    [Fact]
    public async Task TheWeekViewAlsoDrawsOneBarInsteadOfAChipPerDay()
    {
        // Tue 15 to Thu 17 September, in the week view: columns 2..4 of the single row.
        var markup = await RenderAsync(
            [new("Conference", new DateTime(2026, 9, 15), new DateTime(2026, 9, 17))],
            view: EventCalendarView.Week);

        var bar = Assert.Single(Bars(markup, "Conference"));
        Assert.Equal(2, bar.StartColumn);
        Assert.Equal(3, bar.Span);

        // One button, not one per day.
        Assert.Single(Regex.Matches(markup, ">Conference<"));
    }

    [Fact]
    public async Task TheWeekViewHasNoLaneLimit()
    {
        // MaxEventsPerDay caps the month grid, where a cell has a fixed height. The week view is a
        // single tall row, so four overlapping runs all get drawn.
        var monday = new DateTime(2026, 9, 14);
        var markup = await RenderAsync(
            [
                new("One", monday, monday.AddDays(4)),
                new("Two", monday, monday.AddDays(4)),
                new("Three", monday, monday.AddDays(4)),
                new("Four", monday, monday.AddDays(4)),
            ],
            maxEventsPerDay: 1,
            view: EventCalendarView.Week);

        Assert.Equal(4, OverlappingTitles.Sum(t => Bars(markup, t).Count));
    }

    [Fact]
    public async Task WeekViewBarsSitAboveTheTimedEvents()
    {
        // The week view puts its day numbers in a header row, so a bar starts at the cell padding
        // rather than below a day-number button.
        var tuesday = new DateTime(2026, 9, 15);
        var markup = await RenderAsync(
            [
                new("Spanning", tuesday, tuesday.AddDays(2)),
                new("Standup", tuesday.AddHours(9), tuesday.AddHours(10)),
            ],
            view: EventCalendarView.Week);

        var bar = Assert.Single(Bars(markup, "Spanning"));
        Assert.Equal(6, bar.Top);
        Assert.Single(Regex.Matches(markup, ">Standup<"));
    }

    // -------------------------------------------------------------------------------------

    private sealed record Bar(int StartColumn, int Span, int Top, string Classes);

    /// <summary>
    /// Reads the bars for one title out of the markup. A bar is the only element carrying the
    /// absolute <c>inset-inline-start:calc(...)</c> the month layout emits, so the pattern cannot
    /// match a chip.
    /// </summary>
    private static List<Bar> Bars(string markup, string title)
    {
        var pattern = new Regex(
            @"class=""(?<cls>[^""]*)"" style=""inset-inline-start:calc\(\(100% - 6px\) \* (?<col>\d+) / 7 [-+] \d+px\);"
            + @"width:calc\(\(100% - 6px\) \* (?<span>\d+) / 7 [-+] \d+px\);top:(?<top>\d+)px;[^""]*""",
            RegexOptions.Compiled);

        return pattern.Matches(markup)
            .Where(m => BarBody(markup, m.Index).Contains(title, StringComparison.Ordinal))
            .Select(m => new Bar(
                int.Parse(m.Groups["col"].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups["span"].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups["top"].Value, CultureInfo.InvariantCulture),
                m.Groups["cls"].Value))
            .ToList();
    }

    /// <summary>The rendered content of the button whose style attribute starts at <paramref name="index"/>.</summary>
    private static string BarBody(string markup, int index)
    {
        var close = markup.IndexOf("</button>", index, StringComparison.Ordinal);
        return close < 0 ? string.Empty : markup[index..close];
    }

    private static async Task<string> RenderAsync(
        Ev[] events,
        int maxEventsPerDay = 3,
        EventCalendarView view = EventCalendarView.Month)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddBlazorBlueprintComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<BbEventCalendar<Ev>>(new()
            {
                [nameof(BbEventCalendar<Ev>.Items)] = events,
                [nameof(BbEventCalendar<Ev>.EventStart)] = (Func<Ev, DateTime>)(e => e.Start),
                [nameof(BbEventCalendar<Ev>.EventEnd)] = (Func<Ev, DateTime?>)(e => e.End),
                [nameof(BbEventCalendar<Ev>.EventTitle)] = (Func<Ev, string>)(e => e.Title),
                [nameof(BbEventCalendar<Ev>.CurrentDate)] = new DateTime(2026, 9, 15),
                [nameof(BbEventCalendar<Ev>.FirstDayOfWeek)] = DayOfWeek.Sunday,
                [nameof(BbEventCalendar<Ev>.MaxEventsPerDay)] = maxEventsPerDay,
                [nameof(BbEventCalendar<Ev>.View)] = view,
            });

            return renderer.Markup();
        });
    }
}
