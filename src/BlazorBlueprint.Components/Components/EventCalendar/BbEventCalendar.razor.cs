using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// An event calendar with month, week, and agenda views, generic over the consumer's event type.
/// </summary>
/// <typeparam name="TEvent">The consumer's event type. Accessor delegates map it to start/end dates and a title.</typeparam>
public partial class BbEventCalendar<TEvent> : ComponentBase
{
    private CultureInfo culture = CultureInfo.CurrentCulture;
    private Dictionary<DateTime, List<TEvent>> eventsByDay = new();
    private EventCalendarRange? lastNotifiedRange;
    private string[]? cachedDayNames;
    private DayOfWeek cachedFirstDayOfWeek;

    /// <summary>
    /// The events to display.
    /// </summary>
    [Parameter]
    public IEnumerable<TEvent>? Items { get; set; }

    /// <summary>
    /// Returns the start date/time of an event.
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TEvent, DateTime> EventStart { get; set; } = default!;

    /// <summary>
    /// Returns the end date/time of an event, or null for a point-in-time event.
    /// When the delegate itself is null, all events are treated as point-in-time.
    /// An event whose end date falls on a later day than its start is drawn as one bar across the
    /// days it covers, not a separate chip in each of them. An event crossing a week boundary is
    /// one bar per week row, squared off at the join.
    /// </summary>
    [Parameter]
    public Func<TEvent, DateTime?>? EventEnd { get; set; }

    /// <summary>
    /// Returns the display title of an event.
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TEvent, string> EventTitle { get; set; } = default!;

    /// <summary>
    /// Returns additional CSS classes for a specific event's chip, merged with the built-in chip classes.
    /// Use this for per-event coloring (e.g. by category).
    /// </summary>
    [Parameter]
    public Func<TEvent, string?>? EventClass { get; set; }

    /// <summary>
    /// Optional template that fully replaces the built-in event chip content.
    /// When set, only minimal layout/focus classes are applied to the chip button;
    /// combine with <see cref="EventClass"/> for chip-level styling.
    /// </summary>
    [Parameter]
    public RenderFragment<TEvent>? EventTemplate { get; set; }

    /// <summary>
    /// The active view. Use @bind-View for two-way binding.
    /// </summary>
    [Parameter]
    public EventCalendarView View { get; set; } = EventCalendarView.Month;

    /// <summary>
    /// Callback when the active view changes.
    /// </summary>
    [Parameter]
    public EventCallback<EventCalendarView> ViewChanged { get; set; }

    /// <summary>
    /// The date that controls the visible period (the month or week containing it).
    /// Use @bind-CurrentDate for two-way binding. Defaults to today.
    /// </summary>
    [Parameter]
    public DateTime CurrentDate { get; set; } = DateTime.Today;

    /// <summary>
    /// Callback when the current date changes through navigation.
    /// </summary>
    [Parameter]
    public EventCallback<DateTime> CurrentDateChanged { get; set; }

    /// <summary>
    /// The first day of the week. Defaults to the current culture's first day of the week.
    /// </summary>
    [Parameter]
    public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary>
    /// The maximum number of event chips shown per day in the month view before
    /// the overflow "+x more" button appears. Defaults to 3.
    /// </summary>
    [Parameter]
    public int MaxEventsPerDay { get; set; } = 3;

    /// <summary>
    /// Callback when an event chip is clicked.
    /// </summary>
    [Parameter]
    public EventCallback<TEvent> OnEventClick { get; set; }

    /// <summary>
    /// Callback when a day number (month view) or day header (week view) is clicked.
    /// </summary>
    [Parameter]
    public EventCallback<DateTime> OnDateClick { get; set; }

    /// <summary>
    /// Callback when the visible date range changes (including the initial range).
    /// Useful for loading events on demand.
    /// </summary>
    [Parameter]
    public EventCallback<EventCalendarRange> OnViewRangeChanged { get; set; }

    /// <summary>
    /// Additional CSS classes to apply to the root element.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Additional CSS classes to apply to the view container — the month grid, the week grid or
    /// the agenda list — rather than to the root element.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The root element wraps the toolbar as well as the view, so sizing it does not size the view
    /// (#544). This targets the view itself, which is what a calendar that has to fill the page
    /// needs. The same classes apply whichever <see cref="View"/> is showing, so a height set here
    /// survives a switch between Month, Week and Agenda.
    /// </para>
    /// <para>
    /// To make the calendar fill the space left by the toolbar, give the root a column flex box and
    /// let the view grow into it:
    /// <code>
    /// &lt;BbEventCalendar Class="bb:flex bb:h-full bb:flex-col" ContainerClass="grow overflow-auto" ... /&gt;
    /// </code>
    /// </para>
    /// </remarks>
    [Parameter]
    public string? ContainerClass { get; set; }

    /// <summary>
    /// Additional attributes to apply to the root element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private DayOfWeek EffectiveFirstDayOfWeek => FirstDayOfWeek ?? culture.DateTimeFormat.FirstDayOfWeek;

    private string CssClass => ClassNames.cn("bb:w-full", Class);

    /// <summary>The month and week grids: same shell, so the container classes merge once.</summary>
    private string GridCssClass => ClassNames.cn(
        "bb:grid bb:gap-px bb:overflow-hidden bb:rounded-lg bb:border bb:border-border bb:bg-border",
        ContainerClass);

    /// <summary>The agenda list, which is a divided card rather than a grid.</summary>
    private string AgendaCssClass => ClassNames.cn(
        "bb:divide-y bb:divide-border bb:overflow-hidden bb:rounded-lg bb:border bb:border-border bb:bg-card bb:text-card-foreground",
        ContainerClass);

    protected override async Task OnParametersSetAsync()
    {
        var currentCulture = CultureInfo.CurrentCulture;
        if (!ReferenceEquals(culture, currentCulture) && culture.Name != currentCulture.Name)
        {
            culture = currentCulture;
            cachedDayNames = null;
        }

        if (cachedFirstDayOfWeek != EffectiveFirstDayOfWeek)
        {
            cachedFirstDayOfWeek = EffectiveFirstDayOfWeek;
            cachedDayNames = null;
        }

        await RefreshAsync();
    }

    #region Range & lookup

    private EventCalendarRange GetVisibleRange()
    {
        switch (View)
        {
            case EventCalendarView.Week:
                var weekStart = StartOfWeek(CurrentDate);
                return new EventCalendarRange(weekStart, weekStart.AddDays(6));
            case EventCalendarView.Agenda:
                var agendaStart = new DateTime(CurrentDate.Year, CurrentDate.Month, 1);
                return new EventCalendarRange(agendaStart, agendaStart.AddMonths(1).AddDays(-1));
            default:
                var firstOfMonth = new DateTime(CurrentDate.Year, CurrentDate.Month, 1);
                var gridStart = StartOfWeek(firstOfMonth);
                return new EventCalendarRange(gridStart, gridStart.AddDays(41));
        }
    }

    private DateTime StartOfWeek(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)EffectiveFirstDayOfWeek + 7) % 7;
        return date.Date.AddDays(-diff);
    }

    /// <summary>
    /// Rebuilds the per-day event lookup for the visible range and raises
    /// <see cref="OnViewRangeChanged"/> when the range has changed.
    /// </summary>
    private async Task RefreshAsync()
    {
        RebuildEventLookup();

        var range = GetVisibleRange();
        if (lastNotifiedRange == range)
        {
            return;
        }

        lastNotifiedRange = range;
        if (OnViewRangeChanged.HasDelegate)
        {
            await OnViewRangeChanged.InvokeAsync(range);
        }
    }

    private void RebuildEventLookup()
    {
        eventsByDay = new Dictionary<DateTime, List<TEvent>>();
        if (Items is null || EventStart is null)
        {
            return;
        }

        var range = GetVisibleRange();
        foreach (var item in Items)
        {
            var startDay = EventStart(item).Date;
            var endDay = (EventEnd?.Invoke(item) ?? EventStart(item)).Date;
            if (endDay < startDay)
            {
                endDay = startDay;
            }

            var from = startDay < range.Start ? range.Start : startDay;
            var to = endDay > range.End ? range.End : endDay;
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                if (!eventsByDay.TryGetValue(day, out var list))
                {
                    list = new List<TEvent>();
                    eventsByDay[day] = list;
                }
                list.Add(item);
            }
        }

        foreach (var list in eventsByDay.Values)
        {
            list.Sort(CompareEvents);
        }
    }

    private int CompareEvents(TEvent a, TEvent b)
    {
        // All-day events first, then by start, then by title for a stable order.
        var allDayCompare = IsAllDay(b).CompareTo(IsAllDay(a));
        if (allDayCompare != 0)
        {
            return allDayCompare;
        }

        var startCompare = EventStart(a).CompareTo(EventStart(b));
        if (startCompare != 0)
        {
            return startCompare;
        }

        return string.Compare(EventTitle(a), EventTitle(b), StringComparison.Ordinal);
    }

    private IReadOnlyList<TEvent> GetEventsForDay(DateTime day) =>
        eventsByDay.TryGetValue(day.Date, out var list) ? list : Array.Empty<TEvent>();

    /// <summary>Whether the event covers more than one calendar day.</summary>
    private bool IsMultiDay(TEvent item)
    {
        var start = EventStart(item).Date;
        var end = (EventEnd?.Invoke(item) ?? EventStart(item)).Date;
        return end > start;
    }

    /// <summary>
    /// The single-day events for a month cell. Multi-day events are drawn as bars across the week
    /// row instead, so they must not also appear here or they would be shown twice.
    /// </summary>
    private List<TEvent> GetSingleDayEvents(DateTime day)
    {
        var chips = new List<TEvent>();
        foreach (var item in GetEventsForDay(day))
        {
            if (!IsMultiDay(item))
            {
                chips.Add(item);
            }
        }

        return chips;
    }

    #endregion

    #region Week-row event bars

    /// <summary>
    /// One horizontal run of a multi-day event within a single week row.
    /// </summary>
    /// <param name="Event">The consumer's event.</param>
    /// <param name="StartColumn">Zero-based column the run starts in.</param>
    /// <param name="Span">How many columns the run covers.</param>
    /// <param name="Lane">Zero-based stacking row within the week, assigned so runs never overlap.</param>
    /// <param name="ContinuesBefore">The event began in an earlier week row.</param>
    /// <param name="ContinuesAfter">The event carries on into a later week row.</param>
    private sealed record EventBar(
        TEvent Event,
        int StartColumn,
        int Span,
        int Lane,
        bool ContinuesBefore,
        bool ContinuesAfter);

    /// <summary>The bars for one week row, and what they cost the day cells underneath them.</summary>
    private sealed class WeekRowLayout
    {
        /// <summary>Runs to draw, already packed into lanes.</summary>
        public List<EventBar> Bars { get; } = new();

        /// <summary>Lanes actually drawn, so every cell in the row reserves the same height.</summary>
        public int VisibleLaneCount { get; set; }

        /// <summary>Drawn bars covering each column — they spend that day's event budget.</summary>
        public int[] BarsPerColumn { get; } = new int[7];

        /// <summary>Bars dropped for exceeding the budget, counted into that day's "+x more".</summary>
        public int[] HiddenPerColumn { get; } = new int[7];
    }

    /// <summary>
    /// Packs the multi-day events overlapping a week row into non-overlapping lanes.
    /// </summary>
    /// <remarks>
    /// An event that crosses a week boundary produces one run per row; the run carries
    /// <see cref="EventBar.ContinuesBefore"/> / <see cref="EventBar.ContinuesAfter"/> so the joint
    /// can be drawn square rather than rounded. Longest events are placed first, which keeps the
    /// long bars at the top of the stack where they read as a continuous line across the month.
    /// </remarks>
    /// <param name="week">The seven days of the row, in display order.</param>
    /// <param name="maxLanes">
    /// How many lanes a day will give up to bars. The month view spends the day's
    /// <see cref="MaxEventsPerDay"/> budget; the week view has no such limit and passes
    /// <see cref="int.MaxValue"/>.
    /// </param>
    private WeekRowLayout BuildWeekLayout(DateTime[] week, int maxLanes)
    {
        var layout = new WeekRowLayout();
        if (Items is null || EventStart is null || maxLanes <= 0)
        {
            return layout;
        }

        var weekStart = week[0];
        var weekEnd = week[6];

        var spanning = new List<(TEvent Item, DateTime Start, DateTime End)>();
        foreach (var item in Items)
        {
            var start = EventStart(item).Date;
            var end = (EventEnd?.Invoke(item) ?? EventStart(item)).Date;
            if (end < start)
            {
                end = start;
            }

            // Single-day events stay as chips inside their cell.
            if (end == start || end < weekStart || start > weekEnd)
            {
                continue;
            }

            spanning.Add((item, start, end));
        }

        spanning.Sort(CompareSpanning);

        // occupied[lane][column]: which cells of the row each lane has already taken.
        var occupied = new List<bool[]>();

        foreach (var (item, start, end) in spanning)
        {
            var from = start < weekStart ? weekStart : start;
            var to = end > weekEnd ? weekEnd : end;
            var startColumn = (from - weekStart).Days;
            var span = (to - from).Days + 1;
            var lane = ClaimLane(occupied, startColumn, span);

            if (lane >= maxLanes)
            {
                // Past the day's budget: drop the bar rather than let the row grow without limit,
                // and report it through the same "+x more" the chips use.
                for (var column = startColumn; column < startColumn + span; column++)
                {
                    layout.HiddenPerColumn[column]++;
                }

                continue;
            }

            layout.Bars.Add(new EventBar(item, startColumn, span, lane, start < from, end > to));
            for (var column = startColumn; column < startColumn + span; column++)
            {
                layout.BarsPerColumn[column]++;
            }

            layout.VisibleLaneCount = Math.Max(layout.VisibleLaneCount, lane + 1);
        }

        return layout;
    }

    /// <summary>Longest first, then earliest, then the view's normal event order.</summary>
    private int CompareSpanning(
        (TEvent Item, DateTime Start, DateTime End) a,
        (TEvent Item, DateTime Start, DateTime End) b)
    {
        var lengthCompare = (b.End - b.Start).CompareTo(a.End - a.Start);
        if (lengthCompare != 0)
        {
            return lengthCompare;
        }

        var startCompare = a.Start.CompareTo(b.Start);
        return startCompare != 0 ? startCompare : CompareEvents(a.Item, b.Item);
    }

    /// <summary>Takes the topmost lane whose columns are all free, adding one if none is.</summary>
    private static int ClaimLane(List<bool[]> occupied, int startColumn, int span)
    {
        for (var lane = 0; ; lane++)
        {
            if (lane == occupied.Count)
            {
                occupied.Add(new bool[7]);
            }

            var columns = occupied[lane];
            var free = true;
            for (var column = startColumn; column < startColumn + span; column++)
            {
                if (columns[column])
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
                columns[column] = true;
            }

            return lane;
        }
    }

    #endregion

    #region View data

    private string[] DayNames => cachedDayNames ??= BuildDayNames();

    private string[] BuildDayNames()
    {
        var cultureNames = culture.DateTimeFormat.AbbreviatedDayNames;
        var start = (int)EffectiveFirstDayOfWeek;
        var names = new string[7];
        for (var i = 0; i < 7; i++)
        {
            names[i] = cultureNames[(start + i) % 7];
        }
        return names;
    }

    private IEnumerable<DateTime[]> MonthWeeks
    {
        get
        {
            var start = GetVisibleRange().Start;
            for (var week = 0; week < 6; week++)
            {
                var days = new DateTime[7];
                for (var i = 0; i < 7; i++)
                {
                    days[i] = start.AddDays((week * 7) + i);
                }
                yield return days;
            }
        }
    }

    private DateTime[] WeekDays
    {
        get
        {
            var start = StartOfWeek(CurrentDate);
            var days = new DateTime[7];
            for (var i = 0; i < 7; i++)
            {
                days[i] = start.AddDays(i);
            }
            return days;
        }
    }

    private IEnumerable<DateTime> AgendaDays => eventsByDay.Keys.OrderBy(d => d);

    private string HeaderTitle
    {
        get
        {
            if (View == EventCalendarView.Week)
            {
                var range = GetVisibleRange();
                return string.Format(
                    culture,
                    "{0} – {1}, {2}",
                    range.Start.ToString("M", culture),
                    range.End.ToString("M", culture),
                    range.End.Year);
            }

            return CurrentDate.ToString("Y", culture);
        }
    }

    private bool IsAllDay(TEvent item)
    {
        var start = EventStart(item);
        if (start.TimeOfDay != TimeSpan.Zero)
        {
            return false;
        }

        var end = EventEnd?.Invoke(item);
        if (end is null)
        {
            return true;
        }

        return end.Value.TimeOfDay == TimeSpan.Zero;
    }

    private string GetEventTimeLabel(TEvent item)
    {
        if (IsAllDay(item))
        {
            return Localizer["EventCalendar.AllDay"];
        }

        var start = EventStart(item);
        var end = EventEnd?.Invoke(item);
        if (end is null || end.Value == start)
        {
            return start.ToString("t", culture);
        }

        return string.Format(
            culture,
            "{0} – {1}",
            start.ToString("t", culture),
            end.Value.ToString("t", culture));
    }

    #endregion

    #region Navigation & interaction

    private async Task SetViewAsync(EventCalendarView view)
    {
        if (View == view)
        {
            return;
        }

        View = view;
        await ViewChanged.InvokeAsync(view);
        await RefreshAsync();
    }

    private async Task SetCurrentDateAsync(DateTime date)
    {
        CurrentDate = date.Date;
        await CurrentDateChanged.InvokeAsync(CurrentDate);
        await RefreshAsync();
    }

    private Task NavigatePreviousAsync() =>
        SetCurrentDateAsync(View == EventCalendarView.Week ? CurrentDate.AddDays(-7) : CurrentDate.AddMonths(-1));

    private Task NavigateNextAsync() =>
        SetCurrentDateAsync(View == EventCalendarView.Week ? CurrentDate.AddDays(7) : CurrentDate.AddMonths(1));

    private Task NavigateTodayAsync() => SetCurrentDateAsync(DateTime.Today);

    private Task HandleEventClick(TEvent item) => OnEventClick.InvokeAsync(item);

    private Task HandleDateClick(DateTime day) => OnDateClick.InvokeAsync(day.Date);

    /// <summary>
    /// The "+x more" overflow: navigates to the clicked day in the agenda view.
    /// </summary>
    private async Task ShowMoreAsync(DateTime day)
    {
        await SetViewAsync(EventCalendarView.Agenda);
        await SetCurrentDateAsync(day);
    }

    #endregion

    #region Styling

    private const string ChipBaseClasses = "bb:block bb:w-full bb:rounded bb:px-1.5 bb:py-0.5 bb:text-left bb:text-xs bb:font-medium bb:bg-primary/10 bb:text-primary bb:hover:bg-primary/20 bb:focus-visible:outline-none bb:focus-visible:ring-2 bb:focus-visible:ring-ring";
    private const string ChipTemplateClasses = "bb:block bb:w-full bb:rounded bb:text-left bb:text-xs bb:focus-visible:outline-none bb:focus-visible:ring-2 bb:focus-visible:ring-ring";
    private const string DayNumberBaseClasses = "bb:flex bb:h-6 bb:w-6 bb:items-center bb:justify-center bb:rounded-full bb:text-xs bb:font-medium bb:hover:bg-accent bb:hover:text-accent-foreground bb:focus-visible:outline-none bb:focus-visible:ring-2 bb:focus-visible:ring-ring";
    private const string DayNumberTodayClasses = "bb:flex bb:h-6 bb:w-6 bb:items-center bb:justify-center bb:rounded-full bb:text-xs bb:font-medium bb:bg-primary bb:text-primary-foreground bb:hover:bg-primary bb:hover:text-primary-foreground bb:focus-visible:outline-none bb:focus-visible:ring-2 bb:focus-visible:ring-ring";

    private string GetEventChipClasses(TEvent item)
    {
        var baseClasses = EventTemplate is not null ? ChipTemplateClasses : ChipBaseClasses;
        var customClass = EventClass?.Invoke(item);
        return string.IsNullOrEmpty(customClass) ? baseClasses : ClassNames.cn(baseClasses, customClass);
    }

    // Month bar geometry. A bar is positioned against the week row rather than its cell, so these
    // have to reproduce the cell's own box: `p-1.5` top padding, the `h-6` day-number button, and
    // the `mt-1` above the event stack. A lane is one chip (text-xs, py-0.5) plus the `space-y-0.5`
    // that separates them, so bars and chips sit on the same rhythm.
    private const int MonthCellPaddingPx = 6;
    private const int MonthDayNumberHeightPx = 24;
    private const int MonthDayNumberGapPx = 4;
    private const int LaneHeightPx = 22;
    private const int EventBarHeightPx = 20;
    private const int EventBarInsetPx = 3;
    private const int MonthBarsTopPx = MonthCellPaddingPx + MonthDayNumberHeightPx + MonthDayNumberGapPx;

    // The week view puts its day numbers in a separate header row, so its bars start at the top of
    // the cell's padding box rather than below a day-number button.
    private const int WeekBarsTopPx = MonthCellPaddingPx;

    private const string EventBarBaseClasses = "bb:absolute bb:z-10 bb:flex bb:items-center bb:overflow-hidden bb:rounded bb:px-1.5 bb:text-left bb:text-xs bb:font-medium bb:bg-primary/10 bb:text-primary bb:hover:bg-primary/20 bb:focus-visible:outline-none bb:focus-visible:ring-2 bb:focus-visible:ring-ring";
    private const string EventBarTemplateClasses = "bb:absolute bb:z-10 bb:flex bb:items-center bb:overflow-hidden bb:rounded bb:text-left bb:text-xs bb:focus-visible:outline-none bb:focus-visible:ring-2 bb:focus-visible:ring-ring";

    private string GetEventBarClasses(EventBar bar)
    {
        var baseClasses = EventTemplate is not null ? EventBarTemplateClasses : EventBarBaseClasses;

        // Square off whichever end runs on into another week row, so the two halves read as one bar.
        var rounding = (bar.ContinuesBefore, bar.ContinuesAfter) switch
        {
            (true, true) => "bb:rounded-none",
            (true, false) => "bb:rounded-l-none",
            (false, true) => "bb:rounded-r-none",
            _ => null,
        };

        return ClassNames.cn(baseClasses, rounding, EventClass?.Invoke(bar.Event));
    }

    /// <summary>
    /// Places a bar across the week row it belongs to.
    /// </summary>
    /// <remarks>
    /// The row is a seven-column grid with a 1px gap, so a column is <c>(100% - 6px) / 7</c> wide
    /// and column <c>s</c> starts <c>s</c> gaps in. Expressing that as a <c>calc()</c> keeps the
    /// bar exactly on the column boundaries at any width, with no measurement and no JavaScript.
    /// The bar sits in its starting day's cell, which keeps it inside a <c>gridcell</c> for
    /// assistive technology and puts it at the right point in the tab order; the cell is static
    /// and the row is relative, so the offsets resolve against the row.
    /// </remarks>
    private static string GetEventBarStyle(EventBar bar, int topBasePx)
    {
        var startInset = bar.ContinuesBefore ? 0 : EventBarInsetPx;
        var endInset = bar.ContinuesAfter ? 0 : EventBarInsetPx;
        var top = topBasePx + (bar.Lane * LaneHeightPx);

        // The gaps: a bar starting in column s clears s of them, and one spanning n columns swallows
        // n - 1. The insets then trim whichever end is a real start or finish rather than a join.
        var left = SignedPixels(bar.StartColumn + startInset);
        var width = SignedPixels(bar.Span - 1 - startInset - endInset);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"left:calc((100% - 6px) * {bar.StartColumn} / 7 {left});width:calc((100% - 6px) * {bar.Span} / 7 {width});top:{top}px;height:{EventBarHeightPx}px;");
    }

    /// <summary>
    /// A signed pixel term for a <c>calc()</c>. Written as <c>- 4px</c> rather than <c>+ -4px</c>,
    /// which is legal but reads like a mistake in devtools.
    /// </summary>
    private static string SignedPixels(int value) =>
        string.Create(CultureInfo.InvariantCulture, $"{(value < 0 ? '-' : '+')} {Math.Abs(value)}px");

    /// <summary>
    /// Reserves the height the bars float above, so the chips below them are never overlapped.
    /// Every cell in the row reserves the same height, which keeps the chips on one baseline.
    /// </summary>
    private static string GetLaneSpacerStyle(int laneCount) =>
        string.Create(CultureInfo.InvariantCulture, $"height:{laneCount * LaneHeightPx}px;");

    /// <summary>
    /// Names the whole event and the days it covers, not just the part in this row — the visual
    /// split across a week boundary is a layout detail and should not leak into the label.
    /// </summary>
    private string GetEventBarAriaLabel(EventBar bar)
    {
        var start = EventStart(bar.Event).Date;
        var end = (EventEnd?.Invoke(bar.Event) ?? EventStart(bar.Event)).Date;

        return string.Format(
            culture,
            "{0}, {1} – {2}",
            EventTitle(bar.Event),
            start.ToString("D", culture),
            end.ToString("D", culture));
    }

    private string GetMonthCellClasses(DateTime day)
    {
        var isOutside = day.Month != CurrentDate.Month || day.Year != CurrentDate.Year;
        return isOutside
            ? "bb:min-h-28 bb:bg-card bb:p-1.5 bb:text-muted-foreground"
            : "bb:min-h-28 bb:bg-card bb:p-1.5";
    }

    private static string GetDayNumberClasses(DateTime day) =>
        day.Date == DateTime.Today ? DayNumberTodayClasses : DayNumberBaseClasses;

    private string GetViewSwitchClasses(EventCalendarView view) =>
        View == view
            ? "bb:rounded-sm bb:px-3 bb:py-1 bb:text-sm bb:font-medium bb:bg-primary bb:text-primary-foreground"
            : "bb:rounded-sm bb:px-3 bb:py-1 bb:text-sm bb:font-medium bb:text-muted-foreground bb:hover:bg-accent bb:hover:text-accent-foreground bb:focus-visible:outline-none bb:focus-visible:ring-2 bb:focus-visible:ring-ring";

    #endregion
}
