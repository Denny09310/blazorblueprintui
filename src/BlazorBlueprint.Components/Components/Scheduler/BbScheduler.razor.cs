using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using BlazorBlueprint.Primitives.Services;

namespace BlazorBlueprint.Components;

/// <summary>A resource scheduler with time slots, recurring appointments and an optional event editor.</summary>
public partial class BbScheduler
{
    [Parameter] public IReadOnlyList<SchedulerEvent> Events { get; set; } = [];
    [Parameter] public EventCallback<IReadOnlyList<SchedulerEvent>> EventsChanged { get; set; }
    [Parameter] public IReadOnlyList<SchedulerResource> Resources { get; set; } = [];
    /// <summary>Resource IDs to display. Null shows every resource; an empty list shows none. The empty string selects the Unassigned lane. Supports @bind-VisibleResourceIds.</summary>
    [Parameter] public IReadOnlyList<string>? VisibleResourceIds { get; set; }
    /// <summary>Raised when the visible resources change. Supports @bind-VisibleResourceIds.</summary>
    [Parameter] public EventCallback<IReadOnlyList<string>?> VisibleResourceIdsChanged { get; set; }
    /// <summary>Shows the built-in resource picker in the toolbar. Has no effect without Resources.</summary>
    [Parameter] public bool ShowResourceFilter { get; set; }
    [Parameter] public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [Parameter] public EventCallback<DateOnly> DateChanged { get; set; }
    [Parameter] public SchedulerView View { get; set; } = SchedulerView.Week;
    [Parameter] public EventCallback<SchedulerView> ViewChanged { get; set; }
    /// <summary>First day in Week view. The toolbar offers Monday and Sunday; WorkWeek always shows Monday through Friday.</summary>
    [Parameter] public DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Monday;
    /// <summary>Raised when the week-start choice changes. Supports @bind-FirstDayOfWeek.</summary>
    [Parameter] public EventCallback<DayOfWeek> FirstDayOfWeekChanged { get; set; }
    /// <summary>The IANA time zone used for lane dates and time labels. Event zones remain independent.</summary>
    [Parameter] public string TimeZoneId { get; set; } = "UTC";
    /// <summary>
    /// The zones the editor offers, under your own names. Empty — the default — lists every IANA
    /// zone the host supports, which is roughly 600 entries and rarely what an application wants.
    /// An event already using a zone you did not list still shows it, so opening that event cannot
    /// silently rewrite its zone.
    /// </summary>
    [Parameter] public IReadOnlyList<SchedulerTimeZone> TimeZones { get; set; } = [];
    /// <summary>Shows per-event time zones. When false, display and editor use TimeZoneId and zone controls are hidden. Stored instants and recurrence zones are preserved.</summary>
    [Parameter] public bool EnableTimeZones { get; set; } = true;
    /// <summary>Allows dragging appointments between time slots, days and resource lanes. Recurring changes apply to one occurrence.</summary>
    [Parameter] public bool AllowDrag { get; set; } = true;
    /// <summary>Allows resizing the top and bottom edges of appointments, snapped to SlotMinutes.</summary>
    [Parameter] public bool AllowResize { get; set; } = true;
    /// <summary>Time-slot size and drag/resize snapping interval, in minutes. Supports 15, 30, 60 and other divisors of 1440 between 5 and 120.</summary>
    [Parameter] public int SlotMinutes { get; set; } = 30;
    /// <summary>
    /// Weekday ranges the schedule emphasises. Slots outside every range for their day are muted.
    /// Empty — the default — mutes nothing. A day with no range is muted end to end, so weekends
    /// need no entry; two ranges for one day mute the gap between them. Muting is visual only, and
    /// Month has no time axis to mute.
    /// </summary>
    [Parameter] public IReadOnlyList<SchedulerDayHours> ActiveHours { get; set; } = [];
    /// <summary>
    /// Refuses to create, move or resize into a slot outside <see cref="ActiveHours"/>. Off by
    /// default, so active hours only mute. It guards the pointer and keyboard surfaces; it does not
    /// validate <see cref="Events"/> you supply, and <c>CreateEvent</c> stays open as the
    /// programmatic escape hatch.
    /// </summary>
    [Parameter] public bool BlockOutsideActiveHours { get; set; }
    [Parameter] public int StartHour { get; set; } = 8;
    [Parameter] public int EndHour { get; set; } = 18;
    /// <summary>Display-zone hour (0–23) to scroll to on the first interactive render. Null starts at StartHour. Later renders preserve the user's scroll position.</summary>
    [Parameter] public int? InitialScrollHour { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public string? AriaLabel { get; set; }
    [Parameter] public RenderFragment<SchedulerOccurrence>? EventTemplate { get; set; }
    /// <summary>Event chips drawn in a Month cell before the rest collapse into a "+x more" count. A multi-day bar spends the budget in every day it covers.</summary>
    [Parameter] public int MaxEventsPerDay { get; set; } = 3;
    /// <summary>All-day rows drawn before the rest collapse into a "+x more" count. A bar spends the budget in every day it covers.</summary>
    [Parameter] public int MaxAllDayRows { get; set; } = 2;
    /// <summary>
    /// Renders your own fields in the editor. Render the context's DefaultContent to keep the
    /// built-in ones and add around them. Derive from SchedulerEvent and cast the context's Draft
    /// to bind your fields.
    /// </summary>
    [Parameter] public RenderFragment<SchedulerEditorContext>? EditorContent { get; set; }
    /// <summary>
    /// Creates the blank event the editor starts from. Supply this when you derive from
    /// SchedulerEvent, or a newly created event is the base type and your fields are lost.
    /// </summary>
    [Parameter] public Func<SchedulerEvent>? NewEventFactory { get; set; }
    /// <summary>Replaces the built-in toolbar. Render the context's DefaultContent to keep it and add around it.</summary>
    [Parameter] public RenderFragment<SchedulerToolbarContext>? ToolbarContent { get; set; }
    /// <summary>Replaces the context menu shown for an empty time slot. Render the context's DefaultItems to keep the built-in items and add your own around them.</summary>
    [Parameter] public RenderFragment<SchedulerSlotMenuContext>? SlotContextMenuContent { get; set; }
    /// <summary>Replaces the context menu shown for an event. Render the context's DefaultItems to keep the built-in items and add your own around them.</summary>
    [Parameter] public RenderFragment<SchedulerEventMenuContext>? EventContextMenuContent { get; set; }
    /// <summary>Runs before EventsChanged. Set Cancel, or throw, to retain the editor and original events.</summary>
    [Parameter] public EventCallback<SchedulerChangeContext> OnEventChange { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private readonly string editorId = $"bb-scheduler-{Guid.NewGuid():N}";
    private IReadOnlyList<SchedulerOccurrence> occurrences = [];
    private List<Lane> lanes = [];
    private (int LaneIndex, DateTimeOffset Start)? selectedSlot;
    private DateOnly? selectedDay;
    private (string Id, DateTimeOffset Start)? selectedOccurrence;
    private List<BandRow> bandRows = [];
    private List<MonthWeek> monthWeeks = [];
    private BbContextMenu? contextMenu;
    private SchedulerSlotMenuContext? slotMenuContext;
    private SchedulerEventMenuContext? eventMenuContext;
    private SchedulerEvent? draft;
    private SchedulerOccurrence? editingOccurrence;
    private SchedulerEditScope editScope = SchedulerEditScope.Occurrence;
    private SchedulerAmbiguousTimeResolution startResolution;
    private SchedulerAmbiguousTimeResolution endResolution;
    private DateTime? localStart;
    private DateTime? localEnd;
    /// <summary>
    /// The zones the editor lists. A stored zone that is not in the supplied list is appended, so
    /// opening an event never quietly moves it to a zone the application happens to prefer.
    /// </summary>
    private IEnumerable<SchedulerTimeZone> EditorTimeZones
    {
        get
        {
            if (TimeZones.Count == 0)
            {
                return TimeZoneIds.Select(id => new SchedulerTimeZone(id, id.Replace('_', ' ')));
            }
            var current = draft?.TimeZoneId;
            return string.IsNullOrEmpty(current) || TimeZones.Any(zone => zone.Id == current)
                ? TimeZones
                : [.. TimeZones, new SchedulerTimeZone(current, current.Replace('_', ' '))];
        }
    }

    /// <summary>The label for a zone: yours when you named it, the identifier otherwise.</summary>
    private string TimeZoneTitle(string id) =>
        TimeZones.FirstOrDefault(zone => zone.Id == id)?.Title ?? id.Replace('_', ' ');

    private static readonly string[] TimeZoneIds = [.. NodaTime.DateTimeZoneProviders.Tzdb.Ids
        .Where(id => TimeZoneInfo.TryFindSystemTimeZoneById(id, out _)).Order(StringComparer.Ordinal)];
    private static readonly string[] RecurrenceFrequencies = ["NONE", "DAILY", "WEEKLY", "MONTHLY", "YEARLY"];
    private static readonly string[] RecurrenceDayCodes = ["SU", "MO", "TU", "WE", "TH", "FR", "SA"];
    private readonly HashSet<DayOfWeek> recurrenceDays = [];
    private bool limitRecurrence;
    private string recurrenceFrequency = "NONE";
    private int? recurrenceCount;
    private bool editorOpen;
    private bool deleteConfirmationOpen;
    private bool restoreDeleteFocus;
    private bool saving;
    private string? editorError;
    private string? loadError;
    private ElementReference schedulerElement;
    private IJSObjectReference? interactionModule;
    private DotNetObjectReference<BbScheduler>? dotNetReference;
    private bool disposed;
    private int revision;

    private DayOfWeek EffectiveWeekStart => View == SchedulerView.WorkWeek ? DayOfWeek.Monday : FirstDayOfWeek;
    private DateOnly RangeDate => View switch
    {
        SchedulerView.Day => Date,
        SchedulerView.Month => WeekStartOn(new DateOnly(Date.Year, Date.Month, 1)),
        _ => WeekStartOn(Date)
    };
    private DateOnly WeekStartOn(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek - (int)EffectiveWeekStart + 7) % 7));
    // Six rows always, so the grid height does not jump between months.
    private int DayCount => View switch { SchedulerView.Day => 1, SchedulerView.WorkWeek => 5, SchedulerView.Month => 42, _ => 7 };
    private bool IsMonth => View == SchedulerView.Month;
    private string RangeHeading => IsMonth
        ? Date.ToDateTime(TimeOnly.MinValue).ToString("Y", CultureInfo.CurrentCulture)
        : RangeDate.ToString("D", CultureInfo.CurrentCulture);
    private TimeZoneInfo DisplayZone => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
    private string EditorTimeZoneId => EnableTimeZones ? draft!.TimeZoneId : TimeZoneId;
    private string DeleteDescription => Localizer[editingOccurrence != null && !string.IsNullOrWhiteSpace(editingOccurrence.Event.RecurrenceRule)
        ? editScope == SchedulerEditScope.Series ? "Scheduler.DeleteSeriesDescription" : "Scheduler.DeleteOccurrenceDescription"
        : "Scheduler.DeleteDescription", editingOccurrence?.Event.Title ?? ""];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }
        if (firstRender)
        {
            interactionModule = await JsModules.GetAsync(JSRuntime, JsModules.Versioned("./_content/BlazorBlueprint.Components/js/scheduler.js?assets=2", typeof(BbScheduler).Assembly));
            if (disposed)
            {
                return;
            }
            dotNetReference = DotNetObjectReference.Create(this);
            await interactionModule.InvokeVoidAsync("initialize", schedulerElement, dotNetReference);
        }
        if (restoreDeleteFocus && interactionModule != null)
        {
            restoreDeleteFocus = false;
            await interactionModule.InvokeVoidAsync("restoreDeleteFocus", editorId);
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        disposed = true;
        if (interactionModule != null)
        {
            try { await interactionModule.InvokeVoidAsync("dispose", schedulerElement); }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException) { }
        }
        dotNetReference?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override void OnParametersSet()
    {
        if (SlotMinutes < 5 || SlotMinutes > 120 || 1440 % SlotMinutes != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SlotMinutes), "Use a divisor of 1440 between 5 and 120 minutes.");
        }
        if (StartHour < 0 || EndHour > 24 || EndHour <= StartHour)
        {
            throw new ArgumentOutOfRangeException(nameof(StartHour), "Use 0 <= StartHour < EndHour <= 24.");
        }
        if (InitialScrollHour is < 0 or > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(InitialScrollHour), "Use an hour between 0 and 23, or null.");
        }
        if (Resources.Any(r => string.IsNullOrWhiteSpace(r.Id)) || Resources.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != Resources.Count)
        {
            throw new ArgumentException("Resource IDs must be unique.", nameof(Resources));
        }
        if (TimeZones.Any(zone => string.IsNullOrWhiteSpace(zone.Id) || string.IsNullOrWhiteSpace(zone.Title))
            || TimeZones.Select(zone => zone.Id).Distinct(StringComparer.Ordinal).Count() != TimeZones.Count)
        {
            throw new ArgumentException("Time zone IDs must be unique and every zone needs a title.", nameof(TimeZones));
        }
        foreach (var zone in TimeZones)
        {
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone.Id, out _)
                || NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(zone.Id) == null)
            {
                throw new ArgumentException($"'{zone.Id}' is not an IANA time zone this host supports.", nameof(TimeZones));
            }
        }
        // MinValue is the agreed spelling of "midnight at the end of the day"; TimeOnly has no 24:00.
        if (ActiveHours.Any(range => range.End != TimeOnly.MinValue && range.End <= range.Start))
        {
            throw new ArgumentException("An active-hours range must end after it starts. Use TimeOnly.MinValue for midnight.", nameof(ActiveHours));
        }
        RefreshSchedule();
    }

    private double? GetInitialScrollTop()
    {
        // Month has no time axis to scroll to.
        if (IsMonth || InitialScrollHour is not { } hour || lanes.Count == 0)
        {
            return null;
        }
        var lane = lanes[0];
        var minutes = (DayBoundary(lane.Date, hour) - lane.Start).TotalMinutes;
        return Math.Clamp(minutes, 0, (lane.End - lane.Start).TotalMinutes) / SlotMinutes * 40;
    }

    private void RefreshSchedule()
    {
        revision++;
        loadError = null;
        // The lane indices the highlight is keyed to are about to be rebuilt.
        selectedSlot = null;
        selectedDay = null;
        selectedOccurrence = null;
        try
        {
            var start = DayBoundary(RangeDate, 0);
            var end = DayBoundary(RangeDate.AddDays(DayCount), 0);
            occurrences = SchedulerEngine.Expand(Events, start, end);
            lanes = [];
            monthWeeks = [];
            if (IsMonth)
            {
                BuildMonthGrid();
                return;
            }
            for (var day = 0; day < DayCount; day++)
            {
                var date = RangeDate.AddDays(day);
                var laneStart = DayBoundary(date, StartHour);
                var laneEnd = DayBoundary(date, EndHour);
                if (Resources.Count == 0)
                {
                    lanes.Add(CreateLane(date, null, laneStart, laneEnd));
                }
                else
                {
                    foreach (var resource in VisibleResources)
                    {
                        lanes.Add(CreateLane(date, resource, laneStart, laneEnd));
                    }
                    if (ShowUnassignedLane)
                    {
                        lanes.Add(CreateLane(date, new SchedulerResource(string.Empty, Localizer["Scheduler.Unassigned"]), laneStart, laneEnd));
                    }
                }
            }
            BuildAllDayBand();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or TimeZoneNotFoundException or InvalidTimeZoneException or Ical.Net.Evaluation.EvaluationException)
        {
            lanes = [];
            bandRows = [];
            loadError = Localizer["Scheduler.InvalidEvents"];
        }
    }

    private DateTimeOffset DayBoundary(DateOnly date, int hour)
    {
        var local = date.ToDateTime(TimeOnly.MinValue).AddHours(hour);
        // A few zones advance at midnight. Start the lane at the first valid wall minute.
        while (DisplayZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }
        return SchedulerEngine.ToInstant(local, TimeZoneId);
    }

    /// <summary>
    /// Builds the six-week Month grid: multi-day runs become bars across a week row, single-day
    /// events stay as chips in their cell.
    /// </summary>
    /// <remarks>
    /// Resources are a filter here, not lanes — a month cell cannot carry one column per resource.
    /// The visible set narrows which occurrences are drawn; the toolbar picker is how a user
    /// changes it.
    /// </remarks>
    private void BuildMonthGrid()
    {
        monthWeeks = [];
        var month = Date.Month;
        var budget = Math.Max(1, MaxEventsPerDay);
        var visible = occurrences.Where(IsVisibleInMonth).ToList();

        for (var week = 0; week < 6; week++)
        {
            var weekStart = RangeDate.AddDays(week * 7);
            var weekEnd = weekStart.AddDays(6);
            var runs = new List<(SchedulerOccurrence Occurrence, int StartColumn, int Span, bool Before, bool After, int Length)>();
            var single = new List<SchedulerOccurrence>[7];
            for (var day = 0; day < 7; day++)
            {
                single[day] = [];
            }

            foreach (var occurrence in visible)
            {
                var startDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(occurrence.Start, DisplayZone).Date);
                // End is exclusive for all-day; a timed event ending exactly at midnight belongs to
                // the previous day, so both cases take the last covered day from the final tick.
                var endDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(occurrence.End.AddTicks(-1), DisplayZone).Date);
                if (endDate < startDate)
                {
                    endDate = startDate;
                }
                if (endDate < weekStart || startDate > weekEnd)
                {
                    continue;
                }
                if (startDate == endDate)
                {
                    single[startDate.DayNumber - weekStart.DayNumber].Add(occurrence);
                    continue;
                }
                var visibleStart = startDate < weekStart ? weekStart : startDate;
                var visibleEnd = endDate > weekEnd ? weekEnd : endDate;
                runs.Add((occurrence, visibleStart.DayNumber - weekStart.DayNumber,
                    visibleEnd.DayNumber - visibleStart.DayNumber + 1,
                    (startDate < visibleStart), (endDate > visibleEnd), endDate.DayNumber - startDate.DayNumber));
            }

            runs.Sort((a, b) =>
            {
                var length = b.Length.CompareTo(a.Length);
                if (length != 0)
                {
                    return length;
                }
                var start = a.Occurrence.Start.CompareTo(b.Occurrence.Start);
                return start != 0 ? start : string.CompareOrdinal(a.Occurrence.Event.Id, b.Occurrence.Event.Id);
            });

            var bars = new List<BandBar>();
            var barsPerColumn = new int[7];
            var hidden = new int[7];
            var occupied = new List<bool[]>();
            var laneCount = 0;

            foreach (var run in runs)
            {
                var lane = DayBandLayout.Claim(occupied, 7, run.StartColumn, run.Span);
                if (lane >= budget)
                {
                    for (var column = run.StartColumn; column < run.StartColumn + run.Span; column++)
                    {
                        hidden[column]++;
                    }
                    continue;
                }
                bars.Add(new BandBar(run.Occurrence, run.StartColumn, run.Span, lane, run.Before, run.After));
                for (var column = run.StartColumn; column < run.StartColumn + run.Span; column++)
                {
                    barsPerColumn[column]++;
                }
                laneCount = Math.Max(laneCount, lane + 1);
            }

            var cells = new List<MonthCell>(7);
            for (var day = 0; day < 7; day++)
            {
                var date = weekStart.AddDays(day);
                // A bar spends the day's budget in every day it covers, so a day carrying one shows
                // fewer chips and counts the difference into its own "+x more".
                var chipBudget = Math.Max(0, budget - barsPerColumn[day]);
                var chips = single[day].OrderBy(o => o.Start).ThenBy(o => o.Event.Id, StringComparer.Ordinal).ToList();
                var overflow = hidden[day] + Math.Max(0, chips.Count - chipBudget);
                if (chips.Count > chipBudget)
                {
                    chips = [.. chips.Take(chipBudget)];
                }
                cells.Add(new MonthCell(date, date.Month == month, chips, overflow));
            }

            monthWeeks.Add(new MonthWeek(cells, bars, laneCount));
        }
    }

    private bool IsVisibleInMonth(SchedulerOccurrence occurrence)
    {
        if (Resources.Count == 0 || VisibleResourceIds is null)
        {
            return true;
        }
        return VisibleResources.Any(r => occurrence.Event.ResourceIds.Contains(r.Id, StringComparer.Ordinal))
            || (ShowUnassignedLane && IsUnassigned(occurrence.Event));
    }

    /// <summary>
    /// Bars float over the week row, so they must clear the day-number button in the cell beneath:
    /// 4px cell padding + a 24px button + its 2px margin.
    /// </summary>
    private const int MonthBarsTopPx = 30;

    private static string MonthBarStyle(BandBar bar) =>
        DayBandLayout.BarStyle(bar.StartColumn, bar.Span, 7, bar.Lane, MonthBarsTopPx, bar.ContinuesBefore, bar.ContinuesAfter);

    private string MonthChipLabel(SchedulerOccurrence occurrence) => occurrence.Event.IsAllDay
        ? Localizer["Scheduler.AllDay"]
        : TimeLabel(occurrence.Start);

    private static string MonthCellClass(MonthCell cell) => ClassNames.cn(
        "bb:relative bb:flex-1 bb:min-w-0 bb:border-e bb:last:border-e-0 bb:p-1 bb:text-start bb:align-top",
        cell.InMonth ? null : "bb:bg-muted/30 bb:text-muted-foreground");

    /// <summary>
    /// Packs the visible all-day occurrences into the band above the time grid.
    /// </summary>
    /// <remarks>
    /// With no resources, or one visible, the band is a single row of day columns and a multi-day
    /// event is one contiguous bar. With two or more visible, the band gets a row per resource
    /// instead of mirroring the day-major lane order below it: lanes run day0-roomA, day0-roomB,
    /// day1-roomA, so one event's columns are not adjacent and no single bar could span them. The
    /// band therefore stops lining up column-for-column with the grid, which is the price of
    /// drawing the bar correctly. Narrowing the resource filter to one restores the alignment.
    /// </remarks>
    private void BuildAllDayBand()
    {
        bandRows = [];
        var allDay = occurrences.Where(o => o.Event.IsAllDay).ToList();
        if (allDay.Count == 0 || DayCount <= 0)
        {
            return;
        }

        var visible = VisibleResources;
        var groups = new List<SchedulerResource?>();
        if (Resources.Count > 0)
        {
            groups.AddRange(visible);
            if (ShowUnassignedLane)
            {
                groups.Add(new SchedulerResource(string.Empty, Localizer["Scheduler.Unassigned"]));
            }
        }
        if (groups.Count <= 1)
        {
            // One resource, or none: a single row keeps the band aligned to the grid. It still
            // filters when there is a resource, so the band shows what the lanes below show.
            var single = BuildBandRow(groups.Count == 1 ? groups[0] : null, allDay, filterByResource: groups.Count == 1);
            if (single.Bars.Count > 0 || single.Hidden.Any(count => count > 0))
            {
                bandRows.Add(single);
            }
            return;
        }
        foreach (var resource in groups)
        {
            var row = BuildBandRow(resource, allDay, filterByResource: true);
            // A resource with nothing all-day would otherwise get an empty labelled strip. The band
            // does not line up with the lanes below anyway, so there is nothing to preserve by
            // keeping it.
            if (row.Bars.Count > 0 || row.Hidden.Any(count => count > 0))
            {
                bandRows.Add(row);
            }
        }
    }

    private BandRow BuildBandRow(SchedulerResource? resource, List<SchedulerOccurrence> allDay, bool filterByResource)
    {
        var first = RangeDate;
        var last = RangeDate.AddDays(DayCount - 1);
        var runs = new List<(SchedulerOccurrence Occurrence, int StartColumn, int Span, bool Before, bool After, int Length)>();

        foreach (var occurrence in allDay)
        {
            if (filterByResource && !MatchesResource(occurrence.Event, resource))
            {
                continue;
            }
            // End is exclusive, so the last covered day is the one the final tick falls in.
            var startDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(occurrence.Start, DisplayZone).Date);
            var endDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(occurrence.End.AddTicks(-1), DisplayZone).Date);
            if (endDate < first || startDate > last)
            {
                continue;
            }
            var visibleStart = startDate < first ? first : startDate;
            var visibleEnd = endDate > last ? last : endDate;
            // The comparisons need their own parentheses: bare "a < b, c > d" inside a tuple parses
            // as the generic type a<b, c> followed by d.
            runs.Add((occurrence, visibleStart.DayNumber - first.DayNumber, visibleEnd.DayNumber - visibleStart.DayNumber + 1,
                (startDate < visibleStart), (endDate > visibleEnd), endDate.DayNumber - startDate.DayNumber));
        }

        // Longest first, then earliest, so long runs sit at the top and read as continuous lines.
        runs.Sort((a, b) =>
        {
            var length = b.Length.CompareTo(a.Length);
            if (length != 0)
            {
                return length;
            }
            var start = a.Occurrence.Start.CompareTo(b.Occurrence.Start);
            return start != 0 ? start : string.CompareOrdinal(a.Occurrence.Event.Id, b.Occurrence.Event.Id);
        });

        var bars = new List<BandBar>();
        var hidden = new int[DayCount];
        var occupied = new List<bool[]>();
        var laneCount = 0;
        var budget = Math.Max(1, MaxAllDayRows);

        foreach (var run in runs)
        {
            var lane = DayBandLayout.Claim(occupied, DayCount, run.StartColumn, run.Span);
            if (lane >= budget)
            {
                // Past the budget: count it into the row's "+x more" rather than let the band grow.
                for (var column = run.StartColumn; column < run.StartColumn + run.Span; column++)
                {
                    hidden[column]++;
                }
                continue;
            }
            bars.Add(new BandBar(run.Occurrence, run.StartColumn, run.Span, lane, run.Before, run.After));
            laneCount = Math.Max(laneCount, lane + 1);
        }

        return new BandRow(resource, bars, Math.Max(1, laneCount), hidden);
    }

    private bool MatchesResource(SchedulerEvent item, SchedulerResource? resource) => resource is null
        || (resource.Id.Length == 0 ? IsUnassigned(item) : item.ResourceIds.Contains(resource.Id, StringComparer.Ordinal));

    private string BandBarStyle(BandBar bar) =>
        DayBandLayout.BarStyle(bar.StartColumn, bar.Span, DayCount, bar.Lane, 2, bar.ContinuesBefore, bar.ContinuesAfter);

    private static string BandBarClass(BandBar bar) => ClassNames.cn(
        "bb:absolute bb:z-10 bb:flex bb:items-center bb:overflow-hidden bb:rounded bb:px-1.5 bb:text-start bb:text-xs bb:font-medium bb:bg-primary/10 bb:text-primary bb:hover:bg-primary/20 bb:focus-visible:outline-none bb:focus-visible:ring-2 bb:focus-visible:ring-ring",
        DayBandLayout.JoinRounding(bar.ContinuesBefore, bar.ContinuesAfter));

    /// <summary>
    /// Names the whole event and every day it covers, not just the part inside the visible range —
    /// the clipping is a layout detail and should not leak into the label.
    /// </summary>
    private string BandBarLabel(BandBar bar)
    {
        var start = TimeZoneInfo.ConvertTime(bar.Occurrence.Start, DisplayZone).Date;
        var last = TimeZoneInfo.ConvertTime(bar.Occurrence.End.AddTicks(-1), DisplayZone).Date;
        return start == last
            ? Localizer["Scheduler.AllDayLabel", bar.Occurrence.Event.Title, start.ToString("D", CultureInfo.CurrentCulture)]
            : Localizer["Scheduler.AllDayRangeLabel", bar.Occurrence.Event.Title,
                start.ToString("D", CultureInfo.CurrentCulture), last.ToString("D", CultureInfo.CurrentCulture)];
    }

    /// <summary>
    /// Whether the resource filter has excluded everything there is to show. Month builds no lanes,
    /// so it has to ask the filter directly rather than infer emptiness from the lane count.
    /// </summary>
    private bool NothingSelected => Resources.Count > 0
        && (IsMonth ? VisibleResources.Count == 0 && !ShowUnassignedLane : lanes.Count == 0);

    private bool IsUnassigned(SchedulerEvent item) => !item.ResourceIds.Any(id => Resources.Any(r => r.Id == id));

    /// <summary>The resources that pass the filter, in declaration order. Null filter means every resource.</summary>
    private IReadOnlyList<SchedulerResource> VisibleResources => VisibleResourceIds is null
        ? Resources
        : [.. Resources.Where(r => VisibleResourceIds.Contains(r.Id, StringComparer.Ordinal))];

    /// <summary>
    /// Whether the Unassigned lane is drawn. With no filter it appears only when something needs it,
    /// which is the behaviour that shipped. With a filter it appears whenever the user picked it —
    /// an explicitly chosen lane that silently vanishes reads as a bug.
    /// </summary>
    private bool ShowUnassignedLane => VisibleResourceIds is null
        ? occurrences.Any(o => IsUnassigned(o.Event))
        : VisibleResourceIds.Contains(string.Empty, StringComparer.Ordinal);

    /// <summary>Every resource the picker offers, including Unassigned under the empty-string key.</summary>
    private IEnumerable<SelectOption<string>> ResourceFilterOptions =>
        Resources.Select(r => new SelectOption<string>(r.Id, r.Title))
            .Append(new SelectOption<string>(string.Empty, Localizer["Scheduler.Unassigned"]));

    /// <summary>Null and "everything selected" are the same view; store null so @bind round-trips the simpler value.</summary>
    private async Task SetVisibleResourcesAsync(IEnumerable<string>? values)
    {
        VisibleResourceIds = values is null ? null : [.. values];
        RefreshSchedule();
        await VisibleResourceIdsChanged.InvokeAsync(VisibleResourceIds);
    }

    private Lane CreateLane(DateOnly date, SchedulerResource? resource, DateTimeOffset start, DateTimeOffset end)
    {
        // All-day events are drawn as bars in the band above; letting them through here would
        // show every one of them twice, and as a full-height block.
        var items = occurrences.Where(o => !o.Event.IsAllDay && o.Start < end && o.End > start
            && (resource == null || (resource.Id.Length == 0 ? IsUnassigned(o.Event) : o.Event.ResourceIds.Contains(resource.Id))))
            .OrderBy(o => o.Start).ThenByDescending(o => o.End).ToList();
        var placements = new List<Placement>();
        var group = new List<Placement>();
        var columns = new List<DateTimeOffset>();
        var groupEnd = DateTimeOffset.MinValue;
        foreach (var item in items)
        {
            if (item.Start >= groupEnd)
            {
                FinishGroup();
            }
            var column = columns.FindIndex(e => e <= item.Start);
            if (column < 0)
            {
                column = columns.Count;
                columns.Add(item.End);
            }
            else
            {
                columns[column] = item.End;
            }
            group.Add(new Placement(item, column, 1));
            if (item.End > groupEnd)
            {
                groupEnd = item.End;
            }
        }
        FinishGroup();
        return new Lane(date, resource, start, end, placements);

        void FinishGroup()
        {
            placements.AddRange(group.Select(p => p with { Columns = columns.Count }));
            group.Clear();
            columns.Clear();
            groupEnd = DateTimeOffset.MinValue;
        }
    }

    private string PlacementStyle(Lane lane, Placement placement)
    {
        var from = placement.Occurrence.Start > lane.Start ? placement.Occurrence.Start : lane.Start;
        var to = placement.Occurrence.End < lane.End ? placement.Occurrence.End : lane.End;
        // Inset cards within their time/overlap allocation so neighboring events stay separate.
        var top = ((from - lane.Start).TotalMinutes / SlotMinutes * 40) + 2;
        var height = Math.Max(20, (to - from).TotalMinutes / SlotMinutes * 40) - 4;
        var left = 100.0 * placement.Column / placement.Columns;
        var width = 100.0 / placement.Columns;
        return FormattableString.Invariant($"top:{top}px;height:{height}px;inset-inline-start:calc(4.5rem + (100% - 4.5rem) * {left / 100} + 4px);width:calc((100% - 4.5rem) * {width / 100} - 8px);");
    }

    /// <summary>Whether a slot is both outside the active hours and closed to interaction.</summary>
    private bool IsSlotBlocked(DateTimeOffset slot) => BlockOutsideActiveHours && IsSlotMuted(slot);

    /// <summary>Whether every minute an event covers sits inside the active hours.</summary>
    private bool IsRangeAllowed(DateTimeOffset start, DateTimeOffset end)
    {
        if (!BlockOutsideActiveHours || ActiveHours.Count == 0)
        {
            return true;
        }
        // Walk the slots the event covers: a move is only legal if none of them is blocked.
        for (var slot = start; slot < end; slot = slot.AddMinutes(SlotMinutes))
        {
            if (IsSlotMuted(slot))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Whether a slot falls outside every active range for its own weekday. Ranges are wall times
    /// in the display zone, so a day that gains or loses an hour keeps the same clock boundaries.
    /// </summary>
    private bool IsSlotMuted(DateTimeOffset slot)
    {
        if (ActiveHours.Count == 0)
        {
            return false;
        }
        var local = TimeZoneInfo.ConvertTime(slot, DisplayZone);
        var time = TimeOnly.FromDateTime(local.DateTime);
        foreach (var range in ActiveHours)
        {
            if (range.Day != local.DayOfWeek)
            {
                continue;
            }
            // An End of MinValue runs to the end of the day.
            if (time >= range.Start && (range.End == TimeOnly.MinValue || time < range.End))
            {
                return false;
            }
        }
        return true;
    }

    private string TimeLabel(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, DisplayZone).ToString("t", CultureInfo.CurrentCulture);
    private string OccurrenceLabel(SchedulerOccurrence item) => $"{item.Event.Title}, {TimeZoneInfo.ConvertTime(item.Start, DisplayZone):f} – {TimeZoneInfo.ConvertTime(item.End, DisplayZone):t}";

    private async Task NavigateAsync(int direction)
    {
        // Both week views advance whole weeks, even when only five days are displayed; Month steps
        // a calendar month rather than six weeks, so the heading follows the month it names.
        Date = IsMonth
            ? Date.AddMonths(direction)
            : Date.AddDays(direction * (View == SchedulerView.Day ? 1 : 7));
        RefreshSchedule();
        await DateChanged.InvokeAsync(Date);
    }

    private async Task NavigateToTodayAsync()
    {
        // The schedule's day can differ from the server's local day.
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(TimeZoneId, out var zone))
        {
            return;
        }
        Date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        RefreshSchedule();
        await DateChanged.InvokeAsync(Date);
    }

    private async Task SetViewAsync(SchedulerView view)
    {
        View = view;
        RefreshSchedule();
        await ViewChanged.InvokeAsync(view);
    }

    private string WeekStartLabel(DayOfWeek day) => Localizer["Scheduler.WeekStartDay", CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day)];

    private async Task SetFirstDayOfWeekAsync(DayOfWeek day)
    {
        if (FirstDayOfWeek == day)
        {
            return;
        }
        FirstDayOfWeek = day;
        RefreshSchedule();
        await FirstDayOfWeekChanged.InvokeAsync(day);
    }

    // The hover tint has to swap rather than stack: :hover raises specificity, so a plain
    // hover:bg-muted would repaint a selected slot as unselected while the cursor is on it.
    private static string SlotClass(bool selected, bool muted) => ClassNames.cn(
        muted ? "bb:bg-muted/60" : null,
        "bb:absolute bb:left-0 bb:w-full bb:border-b bb:border-dashed bb:text-start bb:text-xs bb:focus-visible:ring-2 bb:focus-visible:ring-ring bb:focus-visible:z-10",
        selected
            ? "bb:bg-primary/15 bb:hover:bg-primary/25 bb:ring-1 bb:ring-inset bb:ring-primary bb:text-foreground"
            : "bb:text-muted-foreground bb:hover:bg-muted/50");

    /// <summary>Names the slot, and the resource only when there is one — "10:00 AM for ." reads as a bug.</summary>
    private string SlotLabel(DateTimeOffset start, SchedulerResource? resource, bool muted = false)
    {
        var time = TimeZoneInfo.ConvertTime(start, DisplayZone).ToString("f", CultureInfo.CurrentCulture);
        var label = string.IsNullOrWhiteSpace(resource?.Title)
            ? Localizer["Scheduler.SlotLabel", time]
            : Localizer["Scheduler.SlotLabelForResource", time, resource.Title];
        // A muted slot reads differently to a sighted user, so say so rather than rely on the tint.
        return muted ? $"{Localizer["Scheduler.OutsideActiveHours"]} {label}" : label;
    }

    private bool IsSlotSelected(int laneIndex, DateTimeOffset start) =>
        selectedSlot is { } slot && slot.LaneIndex == laneIndex && slot.Start == start;

    /// <summary>Highlights one slot. Keyed by lane as well as instant, so the same time in two resource lanes stays distinct.</summary>
    private void SelectSlot(int laneIndex, DateTimeOffset start)
    {
        if (ReadOnly || saving)
        {
            return;
        }
        selectedOccurrence = null;
        selectedDay = null;
        selectedSlot = (laneIndex, start);
    }

    private void SlotKeyDown(KeyboardEventArgs args, DateTimeOffset start, string? resourceId)
    {
        if (IsSlotBlocked(start))
        {
            return;
        }
        // Enter creates, which is what Enter did before this component gained a selection.
        // Space is left alone: the browser turns it into a click, and click selects.
        if (args.Key is "Enter")
        {
            CreateEvent(start, resourceId);
        }
    }

    /// <summary>
    /// Opens the shared menu at the pointer. A ReadOnly scheduler with no override has nothing to
    /// offer, so it opens nothing rather than an empty box.
    /// </summary>
    private async Task ShowSlotMenuAsync(MouseEventArgs args, DateTimeOffset start, SchedulerResource? resource)
    {
        if (contextMenu == null || (ReadOnly && SlotContextMenuContent == null) || saving)
        {
            return;
        }
        eventMenuContext = null;
        slotMenuContext = new SchedulerSlotMenuContext(start, start.AddMinutes(SlotMinutes), resource, DefaultSlotMenuItems(start, resource?.Id));
        await contextMenu.OpenAt(args.ClientX, args.ClientY);
    }

    private async Task ShowEventMenuAsync(MouseEventArgs args, SchedulerOccurrence occurrence)
    {
        if (contextMenu == null || (ReadOnly && EventContextMenuContent == null) || saving)
        {
            return;
        }
        slotMenuContext = null;
        eventMenuContext = new SchedulerEventMenuContext(occurrence, DefaultEventMenuItems(occurrence));
        await contextMenu.OpenAt(args.ClientX, args.ClientY);
    }

    private async Task CloseContextMenuAsync()
    {
        if (contextMenu != null)
        {
            await contextMenu.Close();
        }
    }

    /// <summary>
    /// Opens the delete confirmation for an occurrence without going through the editor, so the
    /// context menu can delete in one step.
    /// </summary>
    private void RequestDeleteFor(SchedulerOccurrence occurrence)
    {
        if (ReadOnly || saving)
        {
            return;
        }
        editingOccurrence = occurrence;
        editScope = SchedulerEditScope.Occurrence;
        draft = occurrence.Event.Clone();
        draft.Start = occurrence.Start;
        draft.End = occurrence.End;
        editorError = null;
        deleteConfirmationOpen = true;
    }

    /// <summary>
    /// Month has no 30-minute slot to select, so a click selects the whole day. Creating from a day
    /// needs a time: StartHour, for one SlotMinutes, which is the same default the editor opens with.
    /// </summary>
    private bool IsDaySelected(DateOnly date) => selectedDay == date;

    private void SelectDay(DateOnly date)
    {
        if (ReadOnly || saving)
        {
            return;
        }
        selectedOccurrence = null;
        selectedSlot = null;
        selectedDay = date;
    }

    /// <summary>
    /// Month has no time axis, so only a day with no active range at all can be called closed.
    /// A day that is merely partly active stays open; its cell creates at StartHour as usual.
    /// </summary>
    private bool IsDayBlocked(DateOnly date) => BlockOutsideActiveHours && ActiveHours.Count > 0
        && !ActiveHours.Any(range => range.Day == date.DayOfWeek);

    private DateTimeOffset DayCreateInstant(DateOnly date) => DayBoundary(date, StartHour);

    private void CreateEventOnDay(DateOnly date)
    {
        if (IsDayBlocked(date))
        {
            return;
        }
        CreateEvent(DayCreateInstant(date));
    }

    private void DayKeyDown(KeyboardEventArgs args, DateOnly date)
    {
        if (args.Key is "Enter")
        {
            CreateEventOnDay(date);
        }
    }

    private SchedulerEditorContext BuildEditorContext(RenderFragment defaultContent) =>
        new(draft!, editingOccurrence is null, editScope, defaultContent);

    private SchedulerToolbarContext BuildToolbarContext(RenderFragment defaultContent) => new()
    {
        Date = Date,
        RangeStart = RangeDate,
        Heading = RangeHeading,
        View = View,
        FirstDayOfWeek = FirstDayOfWeek,
        TimeZoneId = TimeZoneId,
        Resources = Resources,
        VisibleResourceIds = VisibleResourceIds,
        DefaultContent = defaultContent,
        Navigate = EventCallback.Factory.Create<int>(this, NavigateAsync),
        GoToToday = EventCallback.Factory.Create(this, NavigateToTodayAsync),
        SetView = EventCallback.Factory.Create<SchedulerView>(this, SetViewAsync),
        SetVisibleResources = EventCallback.Factory.Create<IReadOnlyList<string>?>(this,
            values => SetVisibleResourcesAsync(values))
    };

    private static string MonthDayButtonClass(bool selected) => ClassNames.cn(
        "bb:mb-0.5 bb:flex bb:h-6 bb:w-6 bb:items-center bb:justify-center bb:rounded bb:text-xs bb:focus-visible:outline-none bb:focus-visible:ring-2 bb:focus-visible:ring-ring",
        selected ? "bb:bg-primary bb:text-primary-foreground" : "bb:hover:bg-muted");

    /// <summary>Opens the editor for a new appointment at an instant.</summary>
    public void CreateEvent(DateTimeOffset start, string? resourceId = null)
    {
        if (ReadOnly || saving)
        {
            return;
        }
        editingOccurrence = null;
        editScope = SchedulerEditScope.Series;
        // The factory keeps a derived type through creation; Clone already keeps it through editing.
        draft = NewEventFactory?.Invoke() ?? new SchedulerEvent();
        draft.Start = start;
        draft.End = start.AddMinutes(SlotMinutes);
        draft.TimeZoneId = TimeZoneId;
        draft.ResourceIds = string.IsNullOrEmpty(resourceId) ? [] : [resourceId];
        OpenEditor();
    }

    /// <summary>
    /// A single click highlights an event; it takes a double-click, Enter, or the context menu's
    /// Edit item to open the editor. Slots behave the same way, and only one thing is highlighted
    /// at a time.
    /// </summary>
    private bool IsOccurrenceSelected(SchedulerOccurrence occurrence) =>
        selectedOccurrence is { } selected && selected.Id == occurrence.Event.Id && selected.Start == occurrence.Start;

    private void SelectOccurrence(SchedulerOccurrence occurrence)
    {
        if (saving)
        {
            return;
        }
        selectedSlot = null;
        selectedDay = null;
        selectedOccurrence = (occurrence.Event.Id, occurrence.Start);
    }

    private void OccurrenceKeyDown(KeyboardEventArgs args, SchedulerOccurrence occurrence)
    {
        if (args.Key is "Enter")
        {
            EditEvent(occurrence);
        }
    }

    private static string SelectedRing(bool selected) =>
        selected ? "bb:ring-2 bb:ring-ring bb:ring-offset-1" : string.Empty;

    /// <summary>Opens an independent editor copy for one occurrence or its series.</summary>
    public void EditEvent(SchedulerOccurrence occurrence, SchedulerEditScope scope = SchedulerEditScope.Occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        if (ReadOnly || saving)
        {
            return;
        }
        editingOccurrence = occurrence;
        editScope = scope;
        SetDraftForScope();
        OpenEditor();
    }

    private void SetDraftForScope()
    {
        draft = editingOccurrence!.Event.Clone();
        if (editScope == SchedulerEditScope.Occurrence)
        {
            draft.Start = editingOccurrence.Start;
            draft.End = editingOccurrence.End;
        }
        UpdateLocalTimes();
        ReadRecurrence();
    }

    private void ScopeChanged(SchedulerEditScope value)
    {
        editScope = value;
        SetDraftForScope();
    }

    private void UpdateLocalTimes()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(EditorTimeZoneId);
        localStart = TimeZoneInfo.ConvertTime(draft!.Start, zone).DateTime;
        localEnd = TimeZoneInfo.ConvertTime(draft.End, zone).DateTime;
        if (draft.IsAllDay)
        {
            // The stored end is exclusive; the editor asks for the last day the event covers.
            localStart = localStart.Value.Date;
            localEnd = TimeZoneInfo.ConvertTime(draft.End.AddTicks(-1), zone).Date;
        }
        startResolution = ResolutionFor(draft.Start, zone);
        endResolution = ResolutionFor(draft.End, zone);
    }

    /// <summary>
    /// Switches the draft between a timed block and a day band, snapping its bounds to whole local
    /// days on the way in so the editor never shows a half-day all-day event.
    /// </summary>
    private void AllDayChanged(bool value)
    {
        if (draft == null || saving)
        {
            return;
        }
        draft.IsAllDay = value;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(EditorTimeZoneId);
        if (value)
        {
            var startDate = TimeZoneInfo.ConvertTime(draft.Start, zone).Date;
            var lastDate = TimeZoneInfo.ConvertTime(draft.End.AddTicks(-1), zone).Date;
            if (lastDate < startDate)
            {
                lastDate = startDate;
            }
            draft.Start = SchedulerEngine.StartOfDay(startDate, EditorTimeZoneId);
            draft.End = SchedulerEngine.StartOfDay(lastDate.AddDays(1), EditorTimeZoneId);
        }
        else
        {
            // Coming back to a timed event, a full day is meaningless. Fall back to one slot at StartHour.
            var startDate = TimeZoneInfo.ConvertTime(draft.Start, zone).Date;
            draft.Start = SchedulerEngine.ToInstant(startDate.AddHours(StartHour), EditorTimeZoneId);
            draft.End = draft.Start.AddMinutes(SlotMinutes);
        }
        UpdateLocalTimes();
    }

    private string ResolutionLabel(SchedulerAmbiguousTimeResolution value) =>
        Localizer[value == SchedulerAmbiguousTimeResolution.Earlier ? "Scheduler.EarlierOffset" : "Scheduler.LaterOffset"];

    private bool IsAmbiguousTime(DateTime? local)
    {
        if (!local.HasValue || draft == null)
        {
            return false;
        }
        return TimeZoneInfo.FindSystemTimeZoneById(EditorTimeZoneId).IsAmbiguousTime(DateTime.SpecifyKind(local.Value, DateTimeKind.Unspecified));
    }

    private static SchedulerAmbiguousTimeResolution ResolutionFor(DateTimeOffset instant, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        return zone.IsAmbiguousTime(local.DateTime) && local.Offset == zone.GetAmbiguousTimeOffsets(local.DateTime).Min()
            ? SchedulerAmbiguousTimeResolution.Later : SchedulerAmbiguousTimeResolution.Earlier;
    }

    private void ReadRecurrence()
    {
        recurrenceFrequency = string.IsNullOrWhiteSpace(draft!.RecurrenceRule) ? "NONE" : "EXISTING";
        recurrenceCount = 10;
        limitRecurrence = false;
        recurrenceDays.Clear();
        if (recurrenceFrequency == "NONE")
        {
            return;
        }

        // Only expose rules that this editor can round-trip. Keep more complex
        // application-supplied schedules intact until the user replaces them.
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in draft.RecurrenceRule!.Split(';'))
        {
            var pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || !fields.TryAdd(pair[0].ToUpperInvariant(), pair[1].ToUpperInvariant()))
            {
                return;
            }
        }
        if (fields.Keys.Any(key => key is not ("FREQ" or "INTERVAL" or "COUNT" or "BYDAY"))
            || !fields.TryGetValue("FREQ", out var frequency)
            || !RecurrenceFrequencies.Contains(frequency, StringComparer.Ordinal) || frequency == "NONE"
            || (fields.TryGetValue("INTERVAL", out var interval) && interval != "1"))
        {
            return;
        }
        if (fields.TryGetValue("COUNT", out var count))
        {
            if (!int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
            {
                return;
            }
            recurrenceCount = parsed;
            limitRecurrence = true;
        }
        if (fields.TryGetValue("BYDAY", out var days))
        {
            if (frequency != "WEEKLY")
            {
                return;
            }
            foreach (var code in days.Split(','))
            {
                var day = Array.IndexOf(RecurrenceDayCodes, code);
                if (day < 0)
                {
                    recurrenceDays.Clear();
                    return;
                }
                recurrenceDays.Add((DayOfWeek)day);
            }
        }
        if (frequency == "WEEKLY" && recurrenceDays.Count == 0)
        {
            recurrenceDays.Add(TimeZoneInfo.ConvertTime(draft.Start, TimeZoneInfo.FindSystemTimeZoneById(draft.TimeZoneId)).DayOfWeek);
        }
        recurrenceFrequency = frequency;
    }

    private void FrequencyChanged(string? value)
    {
        recurrenceFrequency = value ?? "NONE";
        if (recurrenceFrequency == "WEEKLY" && recurrenceDays.Count == 0)
        {
            recurrenceDays.Add((localStart ?? DateTime.Today).DayOfWeek);
        }
        UpdateRecurrence();
    }

    private IEnumerable<DayOfWeek> RecurrenceWeekDays => Enumerable.Range(0, 7)
        .Select(offset => (DayOfWeek)(((int)FirstDayOfWeek + offset) % 7));

    private void ToggleRecurrenceDay(DayOfWeek day, bool selected)
    {
        if (selected)
        {
            recurrenceDays.Add(day);
        }
        else
        {
            recurrenceDays.Remove(day);
        }
        UpdateRecurrence();
    }

    private void UpdateRecurrence()
    {
        if (recurrenceFrequency == "NONE")
        {
            draft!.RecurrenceRule = null;
        }
        else if (recurrenceFrequency != "EXISTING" && (recurrenceFrequency != "WEEKLY" || recurrenceDays.Count > 0))
        {
            draft!.RecurrenceRule = $"FREQ={recurrenceFrequency}"
                + (recurrenceFrequency == "WEEKLY" ? ";BYDAY=" + string.Join(',', recurrenceDays.Order().Select(day => RecurrenceDayCodes[(int)day])) : "")
                + (limitRecurrence ? FormattableString.Invariant($";COUNT={recurrenceCount ?? 10}") : "");
        }
    }

    private void OpenEditor()
    {
        UpdateLocalTimes();
        ReadRecurrence();
        editorError = null;
        deleteConfirmationOpen = false;
        editorOpen = true;
        StateHasChanged();
    }

    private void ToggleResource(string resourceId, bool selected)
    {
        if (selected && !draft!.ResourceIds.Contains(resourceId))
        {
            draft.ResourceIds.Add(resourceId);
        }
        else if (!selected)
        {
            draft!.ResourceIds.Remove(resourceId);
        }
    }

    private async Task SaveAsync(bool delete)
    {
        if (draft == null || saving || ReadOnly)
        {
            return;
        }
        editorError = null;
        saving = true;
        StateHasChanged();
        try
        {
            if (!delete)
            {
                if (recurrenceFrequency == "WEEKLY" && recurrenceDays.Count == 0
                    && (editingOccurrence == null || editScope == SchedulerEditScope.Series
                        || string.IsNullOrWhiteSpace(editingOccurrence.Event.RecurrenceRule)))
                {
                    editorError = Localizer["Scheduler.ChooseRepeatDay"];
                    return;
                }
                if (!localStart.HasValue || !localEnd.HasValue)
                {
                    throw new ArgumentException("Start and end are required.");
                }
                if (draft.IsAllDay)
                {
                    // The editor collects the last covered day; the event stores an exclusive end.
                    var lastDay = localEnd.Value.Date;
                    if (lastDay < localStart.Value.Date)
                    {
                        lastDay = localStart.Value.Date;
                    }
                    draft.Start = SchedulerEngine.StartOfDay(localStart.Value.Date, EditorTimeZoneId);
                    draft.End = SchedulerEngine.StartOfDay(lastDay.AddDays(1), EditorTimeZoneId);
                }
                else
                {
                    draft.Start = SchedulerEngine.ToInstant(localStart.Value, EditorTimeZoneId, startResolution);
                    draft.End = SchedulerEngine.ToInstant(localEnd.Value, EditorTimeZoneId, endResolution);
                }
                SchedulerEngine.Validate(draft);
            }
            var kind = delete ? SchedulerChangeKind.Delete : editingOccurrence == null ? SchedulerChangeKind.Create : SchedulerChangeKind.Update;
            var proposed = SchedulerEngine.ApplyChange(Events, draft, kind, editScope, editingOccurrence?.Start);
            _ = SchedulerEngine.Expand(proposed, DayBoundary(RangeDate, 0), DayBoundary(RangeDate.AddDays(DayCount), 0));
            var context = new SchedulerChangeContext { Kind = kind, Scope = editScope, Event = draft.Clone(),
                OccurrenceStart = editingOccurrence?.Start, Events = proposed };
            await OnEventChange.InvokeAsync(context);
            if (context.Cancel)
            {
                editorError = Localizer["Scheduler.SaveRejected"];
                return;
            }
            await EventsChanged.InvokeAsync(proposed);
            Events = proposed;
            editorOpen = false;
            deleteConfirmationOpen = false;
            RefreshSchedule();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or TimeZoneNotFoundException or InvalidTimeZoneException or Ical.Net.Evaluation.EvaluationException)
        {
            editorError = Localizer["Scheduler.InvalidEdit"];
        }
        catch (Exception)
        {
            editorError = Localizer["Scheduler.SaveFailed"];
        }
        finally
        {
            saving = false;
        }
    }

    private void SetEditorOpen(bool value)
    {
        if (!saving)
        {
            editorOpen = value;
            if (!value)
            {
                deleteConfirmationOpen = false;
            }
        }
    }

    private void RequestDelete()
    {
        if (editingOccurrence == null || saving || ReadOnly)
        {
            return;
        }
        editorError = null;
        deleteConfirmationOpen = true;
    }

    private void SetDeleteConfirmationOpen(bool value)
    {
        if (!saving)
        {
            restoreDeleteFocus = deleteConfirmationOpen && !value && editorOpen;
            deleteConfirmationOpen = value;
        }
    }

    private Task ConfirmDeleteAsync() => deleteConfirmationOpen ? SaveAsync(true) : Task.CompletedTask;

    /// <summary>Commits a browser drag/resize after checking the visible occurrence, lane and snapping bounds. Applications normally use the pointer UI or EditEvent.</summary>
    [JSInvokable]
    public async Task CommitInteractionAsync(int renderedRevision, string eventId, long occurrenceStart, int sourceLaneIndex,
        int targetLaneIndex, long startMilliseconds, long endMilliseconds, string action)
    {
        if (disposed || ReadOnly || saving || editorOpen || renderedRevision != revision
            || (action == "move" ? !AllowDrag : !AllowResize || action is not ("start" or "end"))
            || sourceLaneIndex < 0 || sourceLaneIndex >= lanes.Count || targetLaneIndex < 0 || targetLaneIndex >= lanes.Count)
        {
            return;
        }
        var source = lanes[sourceLaneIndex];
        var target = lanes[targetLaneIndex];
        var occurrence = source.Placements.Select(p => p.Occurrence).FirstOrDefault(o => o.Event.Id == eventId && o.Start.ToUnixTimeMilliseconds() == occurrenceStart);
        if (occurrence == null)
        {
            return;
        }
        var originalStart = occurrence.Start.ToUnixTimeMilliseconds();
        var originalEnd = occurrence.End.ToUnixTimeMilliseconds();
        var slot = SlotMinutes * 60_000L;
        var laneStart = target.Start.ToUnixTimeMilliseconds();
        var laneEnd = target.End.ToUnixTimeMilliseconds();
        if (startMilliseconds >= endMilliseconds)
        {
            return;
        }
        if (action == "move")
        {
            var earliest = laneStart - Math.Max(0, source.Start.ToUnixTimeMilliseconds() - originalStart);
            if (startMilliseconds < earliest || startMilliseconds >= laneEnd || endMilliseconds <= laneStart
                || endMilliseconds > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds() || startMilliseconds < DateTimeOffset.MinValue.ToUnixTimeMilliseconds()
                || endMilliseconds - startMilliseconds != originalEnd - originalStart
                || (startMilliseconds - laneStart) % slot != 0)
            {
                return;
            }
        }
        else
        {
            if (sourceLaneIndex != targetLaneIndex || endMilliseconds - startMilliseconds < slot)
            {
                return;
            }
            var edge = action == "start" ? startMilliseconds : endMilliseconds;
            if (edge < laneStart || edge > laneEnd || (edge - laneStart) % slot != 0
                || (action == "start" ? endMilliseconds != originalEnd || originalStart < laneStart : startMilliseconds != originalStart || originalEnd > laneEnd))
            {
                return;
            }
        }
        if (originalStart == startMilliseconds && originalEnd == endMilliseconds && sourceLaneIndex == targetLaneIndex)
        {
            return;
        }
        // Blocking has to cover gestures too, or an event could simply be dragged into a closed slot.
        if (!IsRangeAllowed(DateTimeOffset.FromUnixTimeMilliseconds(startMilliseconds),
                DateTimeOffset.FromUnixTimeMilliseconds(endMilliseconds)))
        {
            return;
        }
        editingOccurrence = occurrence;
        editScope = SchedulerEditScope.Occurrence;
        draft = occurrence.Event.Clone();
        draft.Start = DateTimeOffset.FromUnixTimeMilliseconds(startMilliseconds);
        draft.End = DateTimeOffset.FromUnixTimeMilliseconds(endMilliseconds);
        if (action == "move" && source.Resource?.Id != target.Resource?.Id)
        {
            if (string.IsNullOrEmpty(target.Resource?.Id))
            {
                draft.ResourceIds.Clear();
            }
            else
            {
                if (source.Resource != null)
                {
                    draft.ResourceIds.Remove(source.Resource.Id);
                }
                if (!draft.ResourceIds.Contains(target.Resource.Id))
                {
                    draft.ResourceIds.Add(target.Resource.Id);
                }
            }
        }
        UpdateLocalTimes();
        ReadRecurrence();
        await SaveAsync(false);
        if (editorError != null)
        {
            editorOpen = true;
        }
        StateHasChanged();
    }

    /// <summary>One run of an all-day event inside the band, already packed into a lane.</summary>
    private sealed record BandBar(SchedulerOccurrence Occurrence, int StartColumn, int Span, int Lane, bool ContinuesBefore, bool ContinuesAfter);

    /// <summary>
    /// One row of the all-day band. With two or more resources visible there is a row each, so a
    /// multi-day bar covers adjacent day columns; the day-major lane order below cannot do that.
    /// </summary>
    private sealed record BandRow(SchedulerResource? Resource, List<BandBar> Bars, int LaneCount, int[] Hidden);

    /// <summary>One day cell of the Month grid.</summary>
    private sealed record MonthCell(DateOnly Date, bool InMonth, List<SchedulerOccurrence> Chips, int HiddenCount);

    /// <summary>One week row of the Month grid: its cells, and the multi-day bars floating over them.</summary>
    private sealed record MonthWeek(List<MonthCell> Cells, List<BandBar> Bars, int LaneCount);

    private sealed record Lane(DateOnly Date, SchedulerResource? Resource, DateTimeOffset Start, DateTimeOffset End, List<Placement> Placements);
    private sealed record Placement(SchedulerOccurrence Occurrence, int Column, int Columns);
}
