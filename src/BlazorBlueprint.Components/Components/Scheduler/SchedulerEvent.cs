using Microsoft.AspNetCore.Components;
namespace BlazorBlueprint.Components;

/// <summary>A timed appointment. Start and End are instants; recurrence follows TimeZoneId wall time.</summary>
/// <remarks>
/// Derive from this to carry your own fields — a description, an agenda, a customer ID. Override
/// <see cref="Clone"/> to return your type and copy your fields, and give the scheduler a
/// <c>NewEventFactory</c> so a newly created event is your type rather than this base one.
/// <code>
/// public sealed class MyEvent : SchedulerEvent
/// {
///     public string? Description { get; set; }
///
///     public override SchedulerEvent Clone()
///     {
///         var copy = new MyEvent { Description = Description };
///         CopyTo(copy);
///         return copy;
///     }
/// }
/// </code>
/// </remarks>
public class SchedulerEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    /// <summary>An IANA time zone, such as America/New_York. Defaults to UTC.</summary>
    public string TimeZoneId { get; set; } = "UTC";
    /// <summary>
    /// Draws the event as a day band rather than a timed block. Start and End are then read as
    /// dates in TimeZoneId and their time components are ignored. <b>End is exclusive</b>: a
    /// single all-day event on the 19th runs from the 19th to the 20th.
    /// </summary>
    public bool IsAllDay { get; set; }
    public List<string> ResourceIds { get; set; } = [];
    /// <summary>An RFC 5545 RRULE without the RRULE: prefix. Null means no recurrence.</summary>
    public string? RecurrenceRule { get; set; }
    /// <summary>Original occurrence start instants omitted from this series.</summary>
    public HashSet<DateTimeOffset> ExcludedStarts { get; set; } = [];
    /// <summary>Series ID for an independently edited occurrence.</summary>
    public string? SeriesId { get; set; }
    /// <summary>Original start instant of an independently edited occurrence.</summary>
    public DateTimeOffset? RecurrenceId { get; set; }

    /// <summary>
    /// Creates an independent editor copy, including resource and exception collections.
    /// </summary>
    /// <remarks>
    /// Override this in a derived type, or every edit silently drops your own fields. Call
    /// <see cref="CopyTo"/> from the override so you only have to remember your own.
    /// </remarks>
    public virtual SchedulerEvent Clone()
    {
        var copy = new SchedulerEvent();
        CopyTo(copy);
        return copy;
    }

    /// <summary>Copies every field this base type owns onto <paramref name="target"/>.</summary>
    protected void CopyTo(SchedulerEvent target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Id = Id;
        target.Title = Title;
        target.Start = Start;
        target.End = End;
        target.TimeZoneId = TimeZoneId;
        target.IsAllDay = IsAllDay;
        target.ResourceIds = [.. ResourceIds];
        target.RecurrenceRule = RecurrenceRule;
        target.ExcludedStarts = [.. ExcludedStarts];
        target.SeriesId = SeriesId;
        target.RecurrenceId = RecurrenceId;
    }
}

/// <summary>
/// The editor's draft, for rendering your own fields beside the built-in ones.
/// </summary>
/// <param name="Draft">
/// The event being edited — an independent copy, so writes here are discarded on Cancel. Cast it to
/// your own type when you derive from <see cref="SchedulerEvent"/>.
/// </param>
/// <param name="IsNew">True when the editor was opened to create rather than to edit.</param>
/// <param name="Scope">Whether the edit applies to one occurrence or the whole series.</param>
/// <param name="DefaultContent">
/// The built-in fields. Render it to keep them and add your own around it; leave it out to replace
/// them entirely.
/// </param>
public sealed record SchedulerEditorContext(
    SchedulerEvent Draft,
    bool IsNew,
    SchedulerEditScope Scope,
    RenderFragment DefaultContent);

/// <summary>
/// The part of one weekday the schedule emphasises. Slots outside every range for their day are
/// muted.
/// </summary>
/// <remarks>
/// Muting is visual only — the slots stay clickable and events can still be created in them.
/// List two ranges for the same day to mute a gap between them, such as a lunch break. A day with
/// no range at all is muted end to end.
/// </remarks>
/// <param name="Day">The weekday the range applies to.</param>
/// <param name="Start">First emphasised wall time, in the schedule's display zone.</param>
/// <param name="End">
/// End of the emphasised range, exclusive. Use <see cref="TimeOnly.MinValue"/> for midnight at the
/// end of the day — <c>TimeOnly</c> cannot express 24:00.
/// </param>
public sealed record SchedulerDayHours(DayOfWeek Day, TimeOnly Start, TimeOnly End);

/// <summary>
/// A time zone the editor offers, under a name of your choosing.
/// </summary>
/// <param name="Id">
/// An IANA identifier such as <c>Australia/Sydney</c>. Validated — you cannot rename your way into
/// a zone that does not exist.
/// </param>
/// <param name="Title">The label shown to the user. "Sydney" rather than "Australia/Sydney".</param>
public sealed record SchedulerTimeZone(string Id, string Title);

/// <summary>A resource displayed as its own scheduler lane.</summary>
public sealed record SchedulerResource(string Id, string Title);

/// <summary>A materialized occurrence. Event refers to the source series or standalone event.</summary>
public sealed record SchedulerOccurrence(SchedulerEvent Event, DateTimeOffset Start, DateTimeOffset End);

public enum SchedulerView { Day, Week, WorkWeek, Month }
public enum SchedulerEditScope { Occurrence, Series }
public enum SchedulerChangeKind { Create, Update, Delete }
public enum SchedulerAmbiguousTimeResolution { Earlier, Later }

/// <summary>Proposed event changes. Persist Events atomically, or set Cancel to retain the editor.</summary>
public sealed class SchedulerChangeContext
{
    public required SchedulerChangeKind Kind { get; init; }
    public required SchedulerEditScope Scope { get; init; }
    public required SchedulerEvent Event { get; init; }
    public DateTimeOffset? OccurrenceStart { get; init; }
    public required IReadOnlyList<SchedulerEvent> Events { get; init; }
    public bool Cancel { get; set; }
}

/// <summary>
/// What a slot context menu was opened on, and the items the scheduler would have shown.
/// </summary>
/// <param name="Start">The instant the slot starts at.</param>
/// <param name="End">The instant the slot ends at — one <c>SlotMinutes</c> later.</param>
/// <param name="Resource">The slot's resource lane, or null when the scheduler has no resources.</param>
/// <param name="DefaultItems">
/// The built-in items. Render it to keep them and add your own around it; leave it out to replace
/// the menu entirely.
/// </param>
public sealed record SchedulerSlotMenuContext(
    DateTimeOffset Start,
    DateTimeOffset End,
    SchedulerResource? Resource,
    RenderFragment DefaultItems);

/// <summary>
/// What an event context menu was opened on, and the items the scheduler would have shown.
/// </summary>
/// <param name="Occurrence">The occurrence that was right-clicked.</param>
/// <param name="DefaultItems">
/// The built-in items. Render it to keep them and add your own around it; leave it out to replace
/// the menu entirely.
/// </param>
public sealed record SchedulerEventMenuContext(
    SchedulerOccurrence Occurrence,
    RenderFragment DefaultItems);

/// <summary>
/// The state and commands a replacement scheduler toolbar needs.
/// </summary>
/// <remarks>
/// Deliberately small. <c>Date</c>, <c>View</c>, <c>FirstDayOfWeek</c> and
/// <c>VisibleResourceIds</c> are all two-way bindable parameters already, so a custom toolbar can
/// drive them with <c>@bind-</c> from its own state. Only the commands that cannot be expressed
/// from outside are carried here: <see cref="Navigate"/> knows the step size for the current view
/// (one day, seven days, one month) and <see cref="GoToToday"/> resolves "today" in the schedule's
/// time zone rather than the server's.
/// </remarks>
public sealed class SchedulerToolbarContext
{
    /// <summary>The bound date.</summary>
    public required DateOnly Date { get; init; }
    /// <summary>The first day the current view renders — the week or month-grid start.</summary>
    public required DateOnly RangeStart { get; init; }
    /// <summary>The heading the built-in toolbar shows: a month name in Month, otherwise a full date.</summary>
    public required string Heading { get; init; }
    public required SchedulerView View { get; init; }
    public required DayOfWeek FirstDayOfWeek { get; init; }
    public required string TimeZoneId { get; init; }
    public required IReadOnlyList<SchedulerResource> Resources { get; init; }
    /// <summary>Null shows every resource; an empty list shows none.</summary>
    public required IReadOnlyList<string>? VisibleResourceIds { get; init; }
    /// <summary>The built-in toolbar. Render it to keep it and add around it; leave it out to replace it.</summary>
    public required RenderFragment DefaultContent { get; init; }
    /// <summary>Steps the view back or forward. Pass -1 or 1; the step size follows the view.</summary>
    public required EventCallback<int> Navigate { get; init; }
    /// <summary>Moves to today in the schedule's time zone, keeping the current view.</summary>
    public required EventCallback GoToToday { get; init; }
    public required EventCallback<SchedulerView> SetView { get; init; }
    public required EventCallback<IReadOnlyList<string>?> SetVisibleResources { get; init; }
}
