using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace BlazorBlueprint.Components;

/// <summary>Time-zone conversion, bounded recurrence expansion and occurrence editing for BbScheduler.</summary>
public static class SchedulerEngine
{
    /// <summary>Converts local wall time to an instant. Missing DST times are rejected; repeated times use resolution.</summary>
    public static DateTimeOffset ToInstant(DateTime local, string timeZoneId,
        SchedulerAmbiguousTimeResolution resolution = SchedulerAmbiguousTimeResolution.Earlier)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            throw new ArgumentException("This local time does not exist because the clocks move forward.", nameof(local));
        }

        var offset = zone.IsAmbiguousTime(local)
            ? (resolution == SchedulerAmbiguousTimeResolution.Earlier
                ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetAmbiguousTimeOffsets(local).Min())
            : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    /// <summary>
    /// The instant a local date starts at. A few zones advance the clock at midnight, so the first
    /// valid wall minute of the day is used rather than 00:00.
    /// </summary>
    public static DateTimeOffset StartOfDay(DateTime localDate, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = localDate.Date;
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }
        return ToInstant(local, timeZoneId);
    }

    /// <summary>Validates identifiers, time bounds, time zone and the supported RRULE frequency.</summary>
    public static void Validate(SchedulerEvent item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Title);
        if (item.End <= item.Start)
        {
            throw new ArgumentException("An event must end after it starts.", nameof(item));
        }
        _ = TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId);
        if (NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(item.TimeZoneId) == null)
        {
            throw new ArgumentException("Use an IANA time zone identifier.", nameof(item));
        }
        if (!string.IsNullOrWhiteSpace(item.RecurrenceRule))
        {
            var rule = ParseRule(item.RecurrenceRule);
            if (rule.Frequency is not (FrequencyType.Daily or FrequencyType.Weekly or FrequencyType.Monthly or FrequencyType.Yearly)
                || rule.Interval < 1 || rule.Count is <= 0 || (rule.Count != null && rule.Until != null))
            {
                throw new ArgumentException("Use a daily, weekly, monthly or yearly RRULE with a positive interval and either COUNT or UNTIL.", nameof(item));
            }
        }
    }

    private static RecurrenceRule ParseRule(string rule) => new(rule);

    /// <summary>Expands only the requested half-open interval. Throws if the occurrence safety limit is exceeded.</summary>
    public static IReadOnlyList<SchedulerOccurrence> Expand(IEnumerable<SchedulerEvent> events,
        DateTimeOffset rangeStart, DateTimeOffset rangeEnd, int maximumOccurrences = 10000)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rangeEnd, rangeStart);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOccurrences);
        var result = new List<SchedulerOccurrence>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in events)
        {
            Validate(item);
            if (!ids.Add(item.Id))
            {
                throw new ArgumentException("Scheduler event IDs must be unique.", nameof(events));
            }
            var duration = item.End - item.Start;
            var zone = TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId);

            // An all-day event is measured in whole local days, never in a fixed TimeSpan. A day
            // that gains or loses an hour to daylight saving is 23 or 25 hours long, so a stored
            // 24-hour duration would drift and land the bar on the wrong day.
            var wholeDays = item.IsAllDay
                ? Math.Max(1, (int)(TimeZoneInfo.ConvertTime(item.End, zone).Date - TimeZoneInfo.ConvertTime(item.Start, zone).Date).TotalDays)
                : 0;

            if (string.IsNullOrWhiteSpace(item.RecurrenceRule))
            {
                Add(item.Start);
                continue;
            }

            var seriesStart = item.IsAllDay
                ? TimeZoneInfo.ConvertTime(item.Start, zone).Date
                : TimeZoneInfo.ConvertTime(item.Start, zone).DateTime;
            var calendarEvent = new CalendarEvent
            {
                DtStart = new CalDateTime(seriesStart, item.TimeZoneId),
                RecurrenceRule = ParseRule(item.RecurrenceRule)
            };
            var earliest = rangeStart > DateTimeOffset.MinValue + duration ? rangeStart - duration : DateTimeOffset.MinValue;
            foreach (var occurrence in calendarEvent.GetOccurrences(new CalDateTime(earliest.UtcDateTime), new Ical.Net.Evaluation.EvaluationOptions { MaxUnmatchedIncrementsLimit = 1000 }))
            {
                var start = new DateTimeOffset(occurrence.Period.StartTime.AsUtc, TimeSpan.Zero);
                if (start >= rangeEnd)
                {
                    break;
                }
                Add(start);
            }

            void Add(DateTimeOffset start)
            {
                DateTimeOffset end;
                if (item.IsAllDay)
                {
                    // Floor to the local date first, so an exclusion recorded against a normalised
                    // occurrence still matches and the time components are genuinely ignored.
                    var localDate = TimeZoneInfo.ConvertTime(start, zone).Date;
                    start = StartOfDay(localDate, item.TimeZoneId);
                    end = StartOfDay(localDate.AddDays(wholeDays), item.TimeZoneId);
                }
                else
                {
                    end = start + duration;
                }
                if (start < rangeEnd && end > rangeStart && !item.ExcludedStarts.Contains(start))
                {
                    if (result.Count >= maximumOccurrences)
                    {
                        throw new InvalidOperationException("The scheduler occurrence limit was exceeded. Narrow the visible range.");
                    }
                    result.Add(new SchedulerOccurrence(item, start, end));
                }
            }
        }
        return result.OrderBy(o => o.Start).ThenBy(o => o.Event.Id, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Creates a new event collection without mutating the original collection or its records.</summary>
    public static IReadOnlyList<SchedulerEvent> ApplyChange(IEnumerable<SchedulerEvent> events, SchedulerEvent edited,
        SchedulerChangeKind kind, SchedulerEditScope scope = SchedulerEditScope.Series, DateTimeOffset? occurrenceStart = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(edited);
        if (kind != SchedulerChangeKind.Delete)
        {
            Validate(edited);
        }
        var result = events.Select(e => e.Clone()).ToList();
        var original = result.Find(e => e.Id == edited.Id);
        if (kind == SchedulerChangeKind.Create)
        {
            if (original != null)
            {
                throw new ArgumentException("The event ID already exists.", nameof(edited));
            }
            result.Add(edited.Clone());
        }
        else if (original == null)
        {
            throw new InvalidOperationException("The event was removed while it was being edited.");
        }
        else if (scope == SchedulerEditScope.Occurrence && !string.IsNullOrWhiteSpace(original.RecurrenceRule))
        {
            if (!occurrenceStart.HasValue)
            {
                throw new ArgumentNullException(nameof(occurrenceStart));
            }
            original.ExcludedStarts.Add(occurrenceStart.Value);
            if (kind != SchedulerChangeKind.Delete)
            {
                var replacement = edited.Clone();
                replacement.Id = Guid.NewGuid().ToString("N");
                replacement.SeriesId = original.Id;
                replacement.RecurrenceId = occurrenceStart;
                replacement.RecurrenceRule = null;
                replacement.ExcludedStarts.Clear();
                result.Add(replacement);
            }
        }
        else if (kind == SchedulerChangeKind.Delete)
        {
            result.RemoveAll(e => e.Id == edited.Id || e.SeriesId == edited.Id);
        }
        else
        {
            result[result.IndexOf(original)] = edited.Clone();
        }
        return result;
    }
}
