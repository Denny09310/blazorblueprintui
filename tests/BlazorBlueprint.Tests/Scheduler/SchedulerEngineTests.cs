using BlazorBlueprint.Components;
using Xunit;

namespace BlazorBlueprint.Tests.Scheduler;

public class SchedulerEngineTests
{
    [Fact]
    public void WeeklySeriesPreservesLocalTimeAcrossDst()
    {
        var item = Appointment(new DateTime(2026, 3, 1, 9, 0, 0), "America/New_York");
        item.RecurrenceRule = "FREQ=WEEKLY;COUNT=3";
        var occurrences = SchedulerEngine.Expand([item], Utc(2026, 3, 1), Utc(2026, 3, 20));
        Assert.Equal(3, occurrences.Count);
        Assert.Equal([14, 13, 13], occurrences.Select(o => o.Start.UtcDateTime.Hour));
        Assert.All(occurrences, o => Assert.Equal(TimeSpan.FromHours(1), o.End - o.Start));
    }

    [Fact]
    public void MissingDstTimeIsRejectedAndRepeatedTimeHasTwoExplicitChoices()
    {
        Assert.Throws<ArgumentException>(() => SchedulerEngine.ToInstant(new DateTime(2026, 3, 8, 2, 30, 0), "America/New_York"));
        var local = new DateTime(2026, 11, 1, 1, 30, 0);
        var earlier = SchedulerEngine.ToInstant(local, "America/New_York");
        var later = SchedulerEngine.ToInstant(local, "America/New_York", SchedulerAmbiguousTimeResolution.Later);
        Assert.Equal(TimeSpan.FromHours(1), later - earlier);
    }

    [Fact]
    public void MonthlyThirtyFirstSkipsMonthsWithoutThatDay()
    {
        var item = Appointment(new DateTime(2026, 1, 31, 9, 0, 0));
        item.RecurrenceRule = "FREQ=MONTHLY;COUNT=3";
        var results = SchedulerEngine.Expand([item], Utc(2026, 1, 1), Utc(2026, 7, 1));
        Assert.Equal([1, 3, 5], results.Select(o => o.Start.Month));
    }

    [Fact]
    public void OverlapAndExclusiveEndAreRespected()
    {
        var item = Appointment(new DateTime(2026, 9, 16, 23, 0, 0));
        item.End = item.Start.AddHours(3);
        Assert.Single(SchedulerEngine.Expand([item], Utc(2026, 9, 17), Utc(2026, 9, 18)));
        Assert.Empty(SchedulerEngine.Expand([item], Utc(2026, 9, 16), item.Start));
    }

    [Fact]
    public void EditingAnOccurrenceCreatesExceptionAndIndependentReplacement()
    {
        var series = Appointment(new DateTime(2026, 9, 16, 9, 0, 0));
        series.RecurrenceRule = "FREQ=DAILY;COUNT=3";
        var originalStart = series.Start.AddDays(1);
        var edited = series.Clone();
        edited.Title = "Rescheduled";
        edited.Start = originalStart.AddHours(2);
        edited.End = edited.Start.AddHours(1);
        var events = SchedulerEngine.ApplyChange([series], edited, SchedulerChangeKind.Update, SchedulerEditScope.Occurrence, originalStart);
        Assert.Empty(series.ExcludedStarts);
        Assert.Equal(2, events.Count);
        var results = SchedulerEngine.Expand(events, Utc(2026, 9, 16), Utc(2026, 9, 20));
        Assert.Equal(3, results.Count);
        Assert.DoesNotContain(results, o => o.Start == originalStart);
        Assert.Single(results, o => o.Event.Title == "Rescheduled");
        Assert.Empty(SchedulerEngine.ApplyChange(events, series, SchedulerChangeKind.Delete));
    }

    [Fact]
    public void DeleteOccurrenceDoesNotDeleteSeries()
    {
        var series = Appointment(new DateTime(2026, 9, 16, 9, 0, 0));
        series.RecurrenceRule = "FREQ=DAILY;COUNT=3";
        var events = SchedulerEngine.ApplyChange([series], series, SchedulerChangeKind.Delete, SchedulerEditScope.Occurrence, series.Start);
        Assert.Equal(2, SchedulerEngine.Expand(events, Utc(2026, 9, 16), Utc(2026, 9, 20)).Count);
    }

    [Fact]
    public void UnboundedRecurrenceIsLimitedByVisibleRangeAndSafetyCap()
    {
        var item = Appointment(new DateTime(2026, 1, 1, 9, 0, 0));
        item.RecurrenceRule = "FREQ=DAILY";
        Assert.Equal(7, SchedulerEngine.Expand([item], Utc(2026, 9, 1), Utc(2026, 9, 8)).Count);
        Assert.Throws<InvalidOperationException>(() => SchedulerEngine.Expand([item], Utc(2026, 9, 1), Utc(2026, 9, 8), 2));
    }

    [Fact]
    public void UntilAndWeekdayFiltersAreInclusiveOfTheLastMatchingInstant()
    {
        var item = Appointment(new DateTime(2026, 9, 14, 9, 0, 0));
        item.RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO,WE;UNTIL=20260923T090000Z";
        var results = SchedulerEngine.Expand([item], Utc(2026, 9, 1), Utc(2026, 10, 1));
        Assert.Equal([14, 16, 21, 23], results.Select(o => o.Start.Day));
    }

    [Fact]
    public void InvalidFrequencyAndDuplicateIdsAreRejected()
    {
        var item = Appointment(new DateTime(2026, 9, 14, 9, 0, 0));
        item.RecurrenceRule = "FREQ=SECONDLY";
        Assert.Throws<ArgumentException>(() => SchedulerEngine.Validate(item));
        item.RecurrenceRule = null;
        Assert.Throws<ArgumentException>(() => SchedulerEngine.Expand([item, item.Clone()], Utc(2026, 9, 1), Utc(2026, 10, 1)));
    }

    [Fact]
    public void ASingleAllDayEventCoversExactlyOneLocalDay()
    {
        var item = AllDay(new DateTime(2026, 9, 19), 1, "America/New_York");

        var occurrence = Assert.Single(SchedulerEngine.Expand([item], Utc(2026, 9, 1), Utc(2026, 10, 1)));

        Assert.Equal(SchedulerEngine.StartOfDay(new DateTime(2026, 9, 19), "America/New_York"), occurrence.Start);
        Assert.Equal(SchedulerEngine.StartOfDay(new DateTime(2026, 9, 20), "America/New_York"), occurrence.End);
    }

    [Fact]
    public void AllDayTimeComponentsAreIgnoredRatherThanHonoured()
    {
        var item = AllDay(new DateTime(2026, 9, 19), 1);
        // A consumer flipping IsAllDay on existing timed data leaves the clock times behind.
        item.Start = item.Start.AddHours(9);
        item.End = item.End.AddHours(17);

        var occurrence = Assert.Single(SchedulerEngine.Expand([item], Utc(2026, 9, 1), Utc(2026, 10, 1)));

        Assert.Equal(TimeSpan.Zero, occurrence.Start.TimeOfDay);
        Assert.Equal(TimeSpan.FromDays(1), occurrence.End - occurrence.Start);
    }

    [Fact]
    public void AMultiDayAllDayEventKeepsItsDayCountNotItsHourCount()
    {
        var item = AllDay(new DateTime(2026, 9, 17), 3, "America/New_York");

        var occurrence = Assert.Single(SchedulerEngine.Expand([item], Utc(2026, 9, 1), Utc(2026, 10, 1)));

        Assert.Equal(SchedulerEngine.StartOfDay(new DateTime(2026, 9, 20), "America/New_York"), occurrence.End);
    }

    [Theory]
    // Spring forward: 2026-03-08 in New York is 23 hours long.
    [InlineData("2026-03-06", 4, "America/New_York")]
    // Autumn back: 2026-11-01 in New York is 25 hours long.
    [InlineData("2026-10-30", 4, "America/New_York")]
    public void ADailyAllDaySeriesStaysOnMidnightAcrossADaylightSavingBoundary(string first, int count, string zone)
    {
        var start = DateTime.Parse(first, System.Globalization.CultureInfo.InvariantCulture);
        var item = AllDay(start, 1, zone);
        item.RecurrenceRule = $"FREQ=DAILY;COUNT={count}";

        var occurrences = SchedulerEngine.Expand([item], Utc(2026, 1, 1), Utc(2027, 1, 1));

        Assert.Equal(count, occurrences.Count);
        for (var day = 0; day < count; day++)
        {
            var expectedDate = start.AddDays(day);
            Assert.Equal(SchedulerEngine.StartOfDay(expectedDate, zone), occurrences[day].Start);
            Assert.Equal(SchedulerEngine.StartOfDay(expectedDate.AddDays(1), zone), occurrences[day].End);
            // The point of the whole exercise: every occurrence begins at local midnight, even on
            // the 23- and 25-hour days a fixed 24-hour duration would drift across.
            Assert.Equal(TimeSpan.Zero, TimeZoneInfo.ConvertTime(occurrences[day].Start,
                TimeZoneInfo.FindSystemTimeZoneById(zone)).TimeOfDay);
        }
    }

    [Fact]
    public void ADailyAllDaySeriesWouldDriftIfDurationWereAFixedTimeSpan()
    {
        // Guards the specific regression: across autumn-back, day 3 of the series must not begin
        // at 23:00 on day 2.
        var item = AllDay(new DateTime(2026, 10, 30), 1, "America/New_York");
        item.RecurrenceRule = "FREQ=DAILY;COUNT=4";

        var occurrences = SchedulerEngine.Expand([item], Utc(2026, 1, 1), Utc(2027, 1, 1));

        Assert.Equal([30, 31, 1, 2], occurrences
            .Select(o => TimeZoneInfo.ConvertTime(o.Start, TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).Day)
            .ToArray());
    }

    [Fact]
    public void AnAllDayEventInAZoneThatAdvancesAtMidnightStartsAtTheFirstValidMinute()
    {
        // Africa/Cairo moved its clocks forward at midnight on 2026-04-24.
        const string zone = "Africa/Cairo";
        var item = AllDay(new DateTime(2026, 4, 24), 1, zone);

        var occurrence = Assert.Single(SchedulerEngine.Expand([item], Utc(2026, 4, 1), Utc(2026, 5, 1)));

        Assert.Equal(SchedulerEngine.StartOfDay(new DateTime(2026, 4, 24), zone), occurrence.Start);
        Assert.False(TimeZoneInfo.FindSystemTimeZoneById(zone)
            .IsInvalidTime(TimeZoneInfo.ConvertTime(occurrence.Start, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime));
    }

    [Fact]
    public void AnAllDayOccurrenceCanBeExcludedByItsNormalisedStart()
    {
        var item = AllDay(new DateTime(2026, 9, 17), 1);
        item.RecurrenceRule = "FREQ=DAILY;COUNT=3";
        item.ExcludedStarts.Add(SchedulerEngine.StartOfDay(new DateTime(2026, 9, 18), "UTC"));

        var occurrences = SchedulerEngine.Expand([item], Utc(2026, 9, 1), Utc(2026, 10, 1));

        Assert.Equal([17, 19], occurrences.Select(o => o.Start.UtcDateTime.Day).ToArray());
    }

    [Fact]
    public void AllDaySurvivesACloneSoTheEditorCannotLoseIt()
    {
        var item = AllDay(new DateTime(2026, 9, 19), 2);
        Assert.True(item.Clone().IsAllDay);
    }

    [Fact]
    public void TimedEventsStillUseAFixedDurationAcrossDaylightSaving()
    {
        // The all-day rule must not leak into timed events: a 60-minute meeting stays 60 minutes.
        var item = Appointment(new DateTime(2026, 10, 30, 9, 0, 0), "America/New_York");
        item.RecurrenceRule = "FREQ=DAILY;COUNT=4";

        var occurrences = SchedulerEngine.Expand([item], Utc(2026, 1, 1), Utc(2027, 1, 1));

        Assert.All(occurrences, o => Assert.Equal(TimeSpan.FromHours(1), o.End - o.Start));
    }

    private static SchedulerEvent AllDay(DateTime localDate, int days, string zone = "UTC") => new()
    {
        Id = "allday",
        Title = "Conference",
        Start = SchedulerEngine.StartOfDay(localDate, zone),
        End = SchedulerEngine.StartOfDay(localDate.AddDays(days), zone),
        TimeZoneId = zone,
        IsAllDay = true
    };

    private static SchedulerEvent Appointment(DateTime local, string zone = "UTC")
    {
        var start = SchedulerEngine.ToInstant(local, zone);
        return new SchedulerEvent { Id = "series", Title = "Meeting", Start = start, End = start.AddHours(1), TimeZoneId = zone };
    }
    private static DateTimeOffset Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, TimeSpan.Zero);
}
