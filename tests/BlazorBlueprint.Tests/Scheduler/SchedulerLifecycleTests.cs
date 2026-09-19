using System.Globalization;
using BlazorBlueprint.Components;
using BlazorBlueprint.Tests.Performance;
using BlazorBlueprint.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorBlueprint.Tests.Scheduler;

public class SchedulerLifecycleTests
{
    [Theory]
    [InlineData(15, "move", 9, 15, 10, 15)]
    [InlineData(30, "start", 8, 30, 10, 0)]
    [InlineData(60, "end", 9, 0, 11, 0)]
    public async Task GesturesSaveSnappedTimesWithoutMutatingSource(int slots, string action, int startHour, int startMinute, int endHour, int endMinute)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(BbScheduler.SlotMinutes)] = slots }));
            await Gesture(scheduler, original, At(startHour, startMinute), At(endHour, endMinute), action);
            Assert.Equal(At(startHour, startMinute), scheduler.Events[0].Start);
            Assert.Equal(At(endHour, endMinute), scheduler.Events[0].End);
            Assert.Equal(At(9), original.Start);
            Assert.Equal(At(10), original.End);
            Assert.False(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
        });
    }

    [Fact]
    public async Task OvernightMovePreservesDurationBeyondVisibleHours()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.End = At(9).AddHours(20);
            await scheduler.SetParametersAsync(ParameterView.Empty);
            await Gesture(scheduler, original, At(10), At(10).AddHours(20), "move");
            Assert.Equal(At(10), scheduler.Events[0].Start);
            Assert.Equal(TimeSpan.FromHours(20), scheduler.Events[0].End - scheduler.Events[0].Start);
        });
    }

    [Fact]
    public async Task MoveAcrossResourcesPreservesOtherAssignmentsAndRecurringSeries()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.ResourceIds = ["a", "b"];
            original.RecurrenceRule = "FREQ=DAILY;COUNT=3";
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.Resources)] = new SchedulerResource[] { new("a", "A"), new("b", "B"), new("c", "C") }
            }));
            await Gesture(scheduler, original, At(10), At(11), "move", target: 2);
            var series = scheduler.Events.Single(e => e.Id == original.Id);
            var moved = scheduler.Events.Single(e => e.SeriesId == original.Id);
            Assert.Contains(At(9), series.ExcludedStarts);
            Assert.Contains("b", moved.ResourceIds);
            Assert.Contains("c", moved.ResourceIds);
            Assert.DoesNotContain("a", moved.ResourceIds);
            Assert.Equal(At(10), moved.Start);
            Assert.Empty(original.ExcludedStarts);
            Assert.Contains("a", original.ResourceIds);
        });
    }

    [Fact]
    public async Task RejectedGestureRetainsOriginalAndOpensDraftForRetry()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var reject = true;
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.OnEventChange)] = EventCallback.Factory.Create<SchedulerChangeContext>(this, context => context.Cancel = reject)
            }));
            await Gesture(scheduler, original, At(10), At(11), "move");
            Assert.Same(original, scheduler.Events[0]);
            Assert.True(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
            Assert.Equal(At(10), ComponentProbe.Field<SchedulerEvent>(scheduler, "draft").Start);
            Assert.NotNull(ComponentProbe.Field<string>(scheduler, "editorError"));
            reject = false;
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            Assert.Equal(At(10), scheduler.Events[0].Start);
        });
    }

    [Fact]
    public async Task ReadOnlyDisabledStaleAndInvalidGesturesDoNotSave()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await Gesture(scheduler, original, At(9, 10), At(10, 10), "move"); // off the slot grid
            await Gesture(scheduler, original, At(9), At(9), "end");
            await Gesture(scheduler, original, At(7), At(8), "move");
            await scheduler.CommitInteractionAsync(-1, original.Id, original.Start.ToUnixTimeMilliseconds(), 0, 0, At(10).ToUnixTimeMilliseconds(), At(11).ToUnixTimeMilliseconds(), "move");
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(BbScheduler.AllowDrag)] = false, [nameof(BbScheduler.AllowResize)] = false }));
            await Gesture(scheduler, original, At(10), At(11), "move");
            await Gesture(scheduler, original, At(9), At(11), "end");
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(BbScheduler.ReadOnly)] = true, [nameof(BbScheduler.AllowDrag)] = true }));
            await Gesture(scheduler, original, At(10), At(11), "move");
            Assert.Same(original, scheduler.Events[0]);
        });
    }

    [Fact]
    public async Task DeleteRequiresConfirmationCancelRetainsEventAndRejectionCanRetry()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var reject = true;
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.OnEventChange)] = EventCallback.Factory.Create<SchedulerChangeContext>(this, context => context.Cancel = reject)
            }));
            scheduler.EditEvent(new(original, original.Start, original.End));
            await (Task)ComponentProbe.Call(scheduler, "ConfirmDeleteAsync")!;
            Assert.Single(scheduler.Events);
            ComponentProbe.Call(scheduler, "RequestDelete");
            Assert.True(ComponentProbe.Field<bool>(scheduler, "deleteConfirmationOpen"));
            ComponentProbe.Call(scheduler, "SetDeleteConfirmationOpen", false);
            Assert.Single(scheduler.Events);
            Assert.True(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
            ComponentProbe.Call(scheduler, "RequestDelete");
            await (Task)ComponentProbe.Call(scheduler, "ConfirmDeleteAsync")!;
            Assert.Single(scheduler.Events);
            Assert.True(ComponentProbe.Field<bool>(scheduler, "deleteConfirmationOpen"));
            reject = false;
            await (Task)ComponentProbe.Call(scheduler, "ConfirmDeleteAsync")!;
            Assert.Empty(scheduler.Events);
            Assert.False(ComponentProbe.Field<bool>(scheduler, "deleteConfirmationOpen"));
        });
    }

    [Fact]
    public async Task SingleZoneEditorUsesDisplayZoneAndPreservesStoredInstantsAndRecurrenceZone()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.TimeZoneId = "America/New_York";
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.EnableTimeZones)] = false,
                [nameof(BbScheduler.TimeZoneId)] = "Asia/Singapore"
            }));
            scheduler.EditEvent(new(original, original.Start, original.End));
            Assert.Equal(At(17).DateTime, ComponentProbe.Field<DateTime?>(scheduler, "localStart"));
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            Assert.Equal(original.Start, scheduler.Events[0].Start);
            Assert.Equal("America/New_York", scheduler.Events[0].TimeZoneId);
        });
    }

    [Theory]
    [InlineData(SchedulerView.Week, DayOfWeek.Monday, "2026-09-16", "2026-09-14", 7)]
    [InlineData(SchedulerView.Week, DayOfWeek.Sunday, "2026-09-16", "2026-09-13", 7)]
    [InlineData(SchedulerView.WorkWeek, DayOfWeek.Sunday, "2026-09-16", "2026-09-14", 5)]
    [InlineData(SchedulerView.WorkWeek, DayOfWeek.Monday, "2026-09-20", "2026-09-14", 5)]
    [InlineData(SchedulerView.Week, DayOfWeek.Sunday, "2027-01-01", "2026-12-27", 7)]
    [InlineData(SchedulerView.WorkWeek, DayOfWeek.Monday, "2027-01-01", "2026-12-28", 5)]
    public async Task WeekViewsRenderTheCorrectDates(SchedulerView view, DayOfWeek firstDay, string date, string firstDate, int days)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.View)] = view,
                [nameof(BbScheduler.FirstDayOfWeek)] = firstDay,
                [nameof(BbScheduler.Date)] = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            }));
            var expected = DateOnly.ParseExact(firstDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            Assert.Equal(Enumerable.Range(0, days).Select(expected.AddDays), LaneDates(scheduler));
        });
    }

    [Theory]
    [InlineData(SchedulerView.Day, 1)]
    [InlineData(SchedulerView.Week, 7)]
    [InlineData(SchedulerView.WorkWeek, 7)]
    public async Task ViewNavigationUsesWholePeriodsAndReportsDateChanges(SchedulerView view, int step)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var notifications = new List<DateOnly>();
            var date = new DateOnly(2026, 12, 30);
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.Date)] = date,
                [nameof(BbScheduler.View)] = view,
                [nameof(BbScheduler.DateChanged)] = EventCallback.Factory.Create<DateOnly>(this, notifications.Add)
            }));
            var initialLanes = LaneDates(scheduler);
            await (Task)ComponentProbe.Call(scheduler, "NavigateAsync", 1)!;
            Assert.Equal(date.AddDays(step), scheduler.Date);
            Assert.Equal(initialLanes.Select(d => d.AddDays(step)), LaneDates(scheduler));
            await (Task)ComponentProbe.Call(scheduler, "NavigateAsync", -1)!;
            Assert.Equal(initialLanes, LaneDates(scheduler));
            Assert.Equal(date, scheduler.Date);
            Assert.Equal(new[] { date.AddDays(step), date }, notifications);
        });
    }

    [Fact]
    public async Task WeekStartBindingAndViewChangesRetainTheWeekPreference()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var starts = new List<DayOfWeek>();
            var views = new List<SchedulerView>();
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.View)] = SchedulerView.Week,
                [nameof(BbScheduler.FirstDayOfWeekChanged)] = EventCallback.Factory.Create<DayOfWeek>(this, starts.Add),
                [nameof(BbScheduler.ViewChanged)] = EventCallback.Factory.Create<SchedulerView>(this, views.Add)
            }));
            await (Task)ComponentProbe.Call(scheduler, "SetFirstDayOfWeekAsync", DayOfWeek.Sunday)!;
            Assert.Equal(DayOfWeek.Sunday, LaneDates(scheduler)[0].DayOfWeek);
            await (Task)ComponentProbe.Call(scheduler, "SetViewAsync", SchedulerView.WorkWeek)!;
            Assert.Equal(DayOfWeek.Monday, LaneDates(scheduler)[0].DayOfWeek);
            Assert.Equal(DayOfWeek.Friday, LaneDates(scheduler)[^1].DayOfWeek);
            Assert.Equal(DayOfWeek.Sunday, scheduler.FirstDayOfWeek);
            await (Task)ComponentProbe.Call(scheduler, "SetViewAsync", SchedulerView.Week)!;
            Assert.Equal(DayOfWeek.Sunday, LaneDates(scheduler)[0].DayOfWeek);
            Assert.Equal(DayOfWeek.Sunday, Assert.Single(starts));
            Assert.Equal(SchedulerView.WorkWeek, views[0]);
            Assert.Equal(SchedulerView.Week, views[1]);
        });
    }

    [Fact]
    public async Task WorkWeekOmitsWeekendRecurrencesAndKeepsResourceLanes()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.Start = At(9).AddDays(-1); // Sunday
            original.End = At(10).AddDays(-1);
            original.RecurrenceRule = "FREQ=DAILY;COUNT=7";
            original.ResourceIds = ["a", "b"];
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.View)] = SchedulerView.WorkWeek,
                [nameof(BbScheduler.FirstDayOfWeek)] = DayOfWeek.Sunday,
                [nameof(BbScheduler.Resources)] = new SchedulerResource[] { new("a", "A"), new("b", "B") }
            }));
            var dates = LaneDates(scheduler);
            Assert.Equal(10, dates.Length);
            Assert.All(dates.GroupBy(d => d), group => Assert.Equal(2, group.Count()));
            var occurrences = ComponentProbe.Field<IReadOnlyList<SchedulerOccurrence>>(scheduler, "occurrences");
            Assert.Equal(5, occurrences.Count);
            Assert.All(occurrences, occurrence => Assert.InRange(occurrence.Start.DayOfWeek, DayOfWeek.Monday, DayOfWeek.Friday));
        });
    }

    [Theory]
    [InlineData(SchedulerView.Day, "Pacific/Kiritimati", 14)]
    [InlineData(SchedulerView.Week, "Pacific/Kiritimati", 14)]
    [InlineData(SchedulerView.WorkWeek, "Pacific/Kiritimati", 14)]
    [InlineData(SchedulerView.Day, "Pacific/Pago_Pago", -11)]
    [InlineData(SchedulerView.Week, "Pacific/Pago_Pago", -11)]
    [InlineData(SchedulerView.WorkWeek, "Pacific/Pago_Pago", -11)]
    public async Task TodayUsesTheScheduleZoneAndRetainsViewAndWeekStart(SchedulerView view, string zone, int offsetHours)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var notifications = new List<DateOnly>();
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.Date)] = new DateOnly(2000, 1, 1),
                [nameof(BbScheduler.View)] = view,
                [nameof(BbScheduler.FirstDayOfWeek)] = DayOfWeek.Sunday,
                [nameof(BbScheduler.TimeZoneId)] = zone,
                [nameof(BbScheduler.EnableTimeZones)] = false,
                [nameof(BbScheduler.ReadOnly)] = true,
                [nameof(BbScheduler.DateChanged)] = EventCallback.Factory.Create<DateOnly>(this, notifications.Add)
            }));
            var before = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(offsetHours)).DateTime);
            await (Task)ComponentProbe.Call(scheduler, "NavigateToTodayAsync")!;
            var after = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(offsetHours)).DateTime);
            Assert.InRange(scheduler.Date, before, after);
            Assert.Equal(new[] { scheduler.Date }, notifications);
            Assert.Equal(view, scheduler.View);
            Assert.Equal(DayOfWeek.Sunday, scheduler.FirstDayOfWeek);
            var weekStart = view == SchedulerView.WorkWeek ? DayOfWeek.Monday : DayOfWeek.Sunday;
            var first = view == SchedulerView.Day ? scheduler.Date
                : scheduler.Date.AddDays(-(((int)scheduler.Date.DayOfWeek - (int)weekStart + 7) % 7));
            var days = view == SchedulerView.Day ? 1 : view == SchedulerView.WorkWeek ? 5 : 7;
            Assert.Equal(Enumerable.Range(0, days).Select(first.AddDays), LaneDates(scheduler));
            Assert.Same(original, Assert.Single(scheduler.Events));
        });
    }

    [Theory]
    [InlineData("DAILY")]
    [InlineData("MONTHLY")]
    [InlineData("YEARLY")]
    public async Task SimpleRepeatPresetsSaveAndReopenWithoutAnInterval(string frequency)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.EditEvent(new(original, original.Start, original.End), SchedulerEditScope.Series);
            ComponentProbe.Call(scheduler, "FrequencyChanged", frequency);
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            var saved = Assert.Single(scheduler.Events);
            Assert.Equal($"FREQ={frequency}", saved.RecurrenceRule);
            scheduler.EditEvent(new(saved, saved.Start, saved.End), SchedulerEditScope.Series);
            Assert.Equal(frequency, ComponentProbe.Field<string>(scheduler, "recurrenceFrequency"));
            Assert.Null(original.RecurrenceRule);
        });
    }

    [Fact]
    public async Task WeeklyCheckboxesSaveSelectedDaysAndRestoreThemWhenReopened()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.EditEvent(new(original, original.Start, original.End), SchedulerEditScope.Series);
            ComponentProbe.Call(scheduler, "FrequencyChanged", "WEEKLY");
            Assert.Equal([DayOfWeek.Monday], ComponentProbe.Field<HashSet<DayOfWeek>>(scheduler, "recurrenceDays"));
            ComponentProbe.Call(scheduler, "ToggleRecurrenceDay", DayOfWeek.Wednesday, true);
            ComponentProbe.Call(scheduler, "ToggleRecurrenceDay", DayOfWeek.Friday, true);
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            var saved = Assert.Single(scheduler.Events);
            Assert.Equal("FREQ=WEEKLY;BYDAY=MO,WE,FR", saved.RecurrenceRule);
            Assert.Equal([14, 16, 18, 21, 23, 25], SchedulerEngine.Expand([saved], At(0), At(0).AddDays(14)).Select(item => item.Start.Day));
            scheduler.EditEvent(new(saved, saved.Start, saved.End), SchedulerEditScope.Series);
            Assert.Equal("WEEKLY", ComponentProbe.Field<string>(scheduler, "recurrenceFrequency"));
            Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], ComponentProbe.Field<HashSet<DayOfWeek>>(scheduler, "recurrenceDays").Order());
        });
    }

    [Theory]
    [InlineData(SchedulerEditScope.Series)]
    [InlineData(SchedulerEditScope.Occurrence)]
    public async Task WeeklyRepeatRequiresADayAndAllowsRetryWithoutDiscardingTheDraft(SchedulerEditScope scope)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.EditEvent(new(original, original.Start, original.End), scope);
            ComponentProbe.Call(scheduler, "FrequencyChanged", "WEEKLY");
            ComponentProbe.Call(scheduler, "ToggleRecurrenceDay", DayOfWeek.Monday, false);
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            Assert.Same(original, Assert.Single(scheduler.Events));
            Assert.True(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
            Assert.Contains("at least one day", ComponentProbe.Field<string>(scheduler, "editorError"));
            ComponentProbe.Call(scheduler, "ToggleRecurrenceDay", DayOfWeek.Tuesday, true);
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            Assert.Equal("FREQ=WEEKLY;BYDAY=TU", Assert.Single(scheduler.Events).RecurrenceRule);
        });
    }

    [Fact]
    public async Task WeeklyPresetReadsExistingDaysAndCountBeforeChangingOneDay()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.RecurrenceRule = "FREQ=WEEKLY;INTERVAL=1;BYDAY=MO,WE;COUNT=7";
            scheduler.EditEvent(new(original, original.Start, original.End), SchedulerEditScope.Series);
            Assert.True(ComponentProbe.Field<bool>(scheduler, "limitRecurrence"));
            Assert.Equal(7, ComponentProbe.Field<int?>(scheduler, "recurrenceCount"));
            ComponentProbe.Call(scheduler, "ToggleRecurrenceDay", DayOfWeek.Wednesday, false);
            ComponentProbe.Call(scheduler, "ToggleRecurrenceDay", DayOfWeek.Friday, true);
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            Assert.Equal("FREQ=WEEKLY;BYDAY=MO,FR;COUNT=7", Assert.Single(scheduler.Events).RecurrenceRule);
        });
    }

    [Theory]
    [InlineData("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE")]
    [InlineData("FREQ=DAILY;UNTIL=20261001T090000Z")]
    [InlineData("FREQ=MONTHLY;BYDAY=2MO")]
    public async Task AdvancedApplicationRulesArePreservedUnlessExplicitlyReplaced(string rule)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.RecurrenceRule = rule;
            scheduler.EditEvent(new(original, original.Start, original.End), SchedulerEditScope.Series);
            Assert.Equal("EXISTING", ComponentProbe.Field<string>(scheduler, "recurrenceFrequency"));
            ComponentProbe.Field<SchedulerEvent>(scheduler, "draft").Title = "Updated title";
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            var saved = Assert.Single(scheduler.Events);
            Assert.Equal(rule, saved.RecurrenceRule);
            Assert.Equal("Updated title", saved.Title);
            scheduler.EditEvent(new(saved, saved.Start, saved.End), SchedulerEditScope.Series);
            ComponentProbe.Call(scheduler, "FrequencyChanged", "DAILY");
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            Assert.Equal("FREQ=DAILY", Assert.Single(scheduler.Events).RecurrenceRule);
        });
    }

    [Theory]
    [InlineData("2026-09-14", "UTC", 0, 24, 8, 640)]
    [InlineData("2026-09-14", "UTC", 8, 18, 0, 0)]
    [InlineData("2026-09-14", "UTC", 8, 18, 23, 800)]
    [InlineData("2026-03-08", "America/New_York", 0, 24, 8, 560)]
    [InlineData("2026-11-01", "America/New_York", 0, 24, 8, 720)]
    public async Task InitialScrollUsesDisplayZoneElapsedTimeAndClampsToTheRenderedRange(
        string date, string zone, int startHour, int endHour, int initialHour, double expectedTop)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            Assert.Null(ComponentProbe.Call(scheduler, "GetInitialScrollTop"));
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.Date)] = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                [nameof(BbScheduler.TimeZoneId)] = zone,
                [nameof(BbScheduler.StartHour)] = startHour,
                [nameof(BbScheduler.EndHour)] = endHour,
                [nameof(BbScheduler.InitialScrollHour)] = initialHour
            }));
            Assert.Equal(expectedTop, (double)ComponentProbe.Call(scheduler, "GetInitialScrollTop")!);
            Assert.Null(ComponentProbe.Field<string?>(scheduler, "loadError"));
        });
    }

    [Fact]
    public async Task NullVisibleResourceIdsShowsEveryResourceLane()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, null);
            Assert.Equal(["room-a", "room-b"], LaneResourceIds(scheduler));
        });
    }

    [Theory]
    [InlineData(new[] { "room-a" }, new[] { "room-a" })]
    [InlineData(new[] { "room-b" }, new[] { "room-b" })]
    [InlineData(new[] { "room-b", "room-a" }, new[] { "room-a", "room-b" })]
    [InlineData(new[] { "room-a", "missing" }, new[] { "room-a" })]
    public async Task VisibleResourceIdsNarrowsLanesAndKeepsDeclarationOrder(string[] visible, string[] expected)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, visible);
            Assert.Equal(expected, LaneResourceIds(scheduler));
        });
    }

    [Fact]
    public async Task EmptyStringSelectsTheUnassignedLaneEvenWithNothingUnassigned()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            // original is assigned to room-a, so nothing is unassigned.
            await WithResourcesAsync(scheduler, original, [""]);
            Assert.Equal([""], LaneResourceIds(scheduler));
        });
    }

    [Fact]
    public async Task UnassignedLaneStaysHiddenWithoutAFilterWhenNothingIsUnassigned()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, null);
            Assert.DoesNotContain("", LaneResourceIds(scheduler));
        });
    }

    [Fact]
    public async Task AnEmptyFilterShowsNoLanesAtAll()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, []);
            Assert.Empty(LaneResourceIds(scheduler));
            Assert.Null(ComponentProbe.Field<string?>(scheduler, "loadError"));
        });
    }

    [Fact]
    public async Task ChangingTheFilterInvalidatesAnInFlightGesture()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, null);
            var staleRevision = ComponentProbe.Field<int>(scheduler, "revision");

            await (Task)ComponentProbe.Call(scheduler, "SetVisibleResourcesAsync", (object)RoomAOnly)!;
            Assert.NotEqual(staleRevision, ComponentProbe.Field<int>(scheduler, "revision"));

            // A gesture that began before the filter changed must not commit against the new lanes.
            await scheduler.CommitInteractionAsync(staleRevision, original.Id, original.Start.ToUnixTimeMilliseconds(),
                0, 0, At(11).ToUnixTimeMilliseconds(), At(12).ToUnixTimeMilliseconds(), "move");
            Assert.Equal(At(9), scheduler.Events[0].Start);
        });
    }

    [Fact]
    public async Task TheToolbarPickerAndTheBoundParameterShareOneState()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            IReadOnlyList<string>? reported = null;
            await WithResourcesAsync(scheduler, original, null);
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.VisibleResourceIdsChanged)] =
                    EventCallback.Factory.Create<IReadOnlyList<string>?>(scheduler, value => reported = value)
            }));

            await (Task)ComponentProbe.Call(scheduler, "SetVisibleResourcesAsync", (object)RoomBOnly)!;

            Assert.Equal(["room-b"], reported);
            Assert.Equal(["room-b"], LaneResourceIds(scheduler));
        });
    }

    [Fact]
    public async Task ClickingASlotSelectsItWithoutOpeningTheEditor()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            ComponentProbe.Call(scheduler, "SelectSlot", 0, At(11));
            Assert.False(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(11))!);
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(12))!);
        });
    }

    [Fact]
    public async Task TheSameTimeInTwoResourceLanesSelectsIndependently()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, null);
            ComponentProbe.Call(scheduler, "SelectSlot", 1, At(11));
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(11))!);
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 1, At(11))!);
        });
    }

    [Fact]
    public async Task DoubleClickingASlotOpensTheEditorForANewEvent()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.CreateEvent(At(11));
            Assert.True(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
            Assert.Null(ComponentProbe.Field<SchedulerOccurrence?>(scheduler, "editingOccurrence"));
            Assert.Equal(At(11), ComponentProbe.Field<SchedulerEvent>(scheduler, "draft").Start);
        });
    }

    [Theory]
    [InlineData("Enter", true)]
    [InlineData(" ", false)]
    [InlineData("a", false)]
    public async Task EnterCreatesFromAFocusedSlotAndOtherKeysDoNot(string key, bool opens)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            ComponentProbe.Call(scheduler, "SlotKeyDown",
                new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = key }, At(11), (string?)null);
            Assert.Equal(opens, ComponentProbe.Field<bool>(scheduler, "editorOpen"));
        });
    }

    [Fact]
    public async Task ReadOnlySchedulesSelectNothing()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(BbScheduler.ReadOnly)] = true }));
            ComponentProbe.Call(scheduler, "SelectSlot", 0, At(11));
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(11))!);
        });
    }

    [Fact]
    public async Task NavigationClearsTheSelectionBecauseLaneIndicesAreRebuilt()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            ComponentProbe.Call(scheduler, "SelectSlot", 0, At(11));
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(11))!);

            await (Task)ComponentProbe.Call(scheduler, "NavigateAsync", 1)!;

            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(11))!);
        });
    }

    [Fact]
    public async Task ChangingTheResourceFilterClearsTheSelection()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, null);
            ComponentProbe.Call(scheduler, "SelectSlot", 0, At(11));
            await (Task)ComponentProbe.Call(scheduler, "SetVisibleResourcesAsync", (object)RoomAOnly)!;
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(11))!);
        });
    }

    [Fact]
    public async Task TheSlotMenuContextCarriesTheSlotBoundsAndItsResource()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, null);
            var resource = new SchedulerResource("room-b", "Room B");

            await (Task)ComponentProbe.Call(scheduler, "ShowSlotMenuAsync", new MouseEventArgs { ClientX = 10, ClientY = 20 }, At(11), resource)!;

            var context = ComponentProbe.Field<SchedulerSlotMenuContext?>(scheduler, "slotMenuContext");
            Assert.NotNull(context);
            Assert.Equal(At(11), context.Start);
            Assert.Equal(At(11, 30), context.End);
            Assert.Equal("room-b", context.Resource?.Id);
            Assert.NotNull(context.DefaultItems);
            Assert.Null(ComponentProbe.Field<SchedulerEventMenuContext?>(scheduler, "eventMenuContext"));
        });
    }

    [Fact]
    public async Task TheSlotMenuEndFollowsSlotMinutes()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(BbScheduler.SlotMinutes)] = 60 }));

            await (Task)ComponentProbe.Call(scheduler, "ShowSlotMenuAsync", new MouseEventArgs(), At(11), (SchedulerResource?)null)!;

            Assert.Equal(At(12), ComponentProbe.Field<SchedulerSlotMenuContext>(scheduler, "slotMenuContext").End);
        });
    }

    [Fact]
    public async Task OpeningTheEventMenuClearsTheSlotMenuSoOnlyOneRenders()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var occurrence = new SchedulerOccurrence(original, original.Start, original.End);

            await (Task)ComponentProbe.Call(scheduler, "ShowSlotMenuAsync", new MouseEventArgs(), At(11), (SchedulerResource?)null)!;
            await (Task)ComponentProbe.Call(scheduler, "ShowEventMenuAsync", new MouseEventArgs(), occurrence)!;

            Assert.Null(ComponentProbe.Field<SchedulerSlotMenuContext?>(scheduler, "slotMenuContext"));
            Assert.Equal(occurrence, ComponentProbe.Field<SchedulerEventMenuContext>(scheduler, "eventMenuContext").Occurrence);
        });
    }

    [Fact]
    public async Task AReadOnlySchedulerWithNoOverrideOpensNoMenuAtAll()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(BbScheduler.ReadOnly)] = true }));

            await (Task)ComponentProbe.Call(scheduler, "ShowSlotMenuAsync", new MouseEventArgs(), At(11), (SchedulerResource?)null)!;
            await (Task)ComponentProbe.Call(scheduler, "ShowEventMenuAsync", new MouseEventArgs(),
                new SchedulerOccurrence(original, original.Start, original.End))!;

            Assert.Null(ComponentProbe.Field<SchedulerSlotMenuContext?>(scheduler, "slotMenuContext"));
            Assert.Null(ComponentProbe.Field<SchedulerEventMenuContext?>(scheduler, "eventMenuContext"));
        });
    }

    [Fact]
    public async Task DeletingFromTheContextMenuConfirmsWithoutOpeningTheEditor()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var occurrence = new SchedulerOccurrence(original, original.Start, original.End);

            ComponentProbe.Call(scheduler, "RequestDeleteFor", occurrence);

            Assert.True(ComponentProbe.Field<bool>(scheduler, "deleteConfirmationOpen"));
            Assert.False(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
            Assert.Equal(occurrence, ComponentProbe.Field<SchedulerOccurrence?>(scheduler, "editingOccurrence"));

            await (Task)ComponentProbe.Call(scheduler, "ConfirmDeleteAsync")!;

            Assert.Empty(scheduler.Events);
            Assert.False(ComponentProbe.Field<bool>(scheduler, "deleteConfirmationOpen"));
        });
    }

    [Fact]
    public async Task ContextMenuDeleteOfOneOccurrenceKeepsTheRestOfTheSeries()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.RecurrenceRule = "FREQ=DAILY;COUNT=5";
            await scheduler.SetParametersAsync(ParameterView.Empty);

            ComponentProbe.Call(scheduler, "RequestDeleteFor", new SchedulerOccurrence(original, At(9), At(10)));
            await (Task)ComponentProbe.Call(scheduler, "ConfirmDeleteAsync")!;

            var series = Assert.Single(scheduler.Events);
            Assert.Equal("FREQ=DAILY;COUNT=5", series.RecurrenceRule);
            Assert.Contains(At(9), series.ExcludedStarts);
        });
    }

    [Fact]
    public async Task AReadOnlySchedulerRefusesToDeleteFromTheContextMenu()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(BbScheduler.ReadOnly)] = true }));

            ComponentProbe.Call(scheduler, "RequestDeleteFor", new SchedulerOccurrence(original, original.Start, original.End));

            Assert.False(ComponentProbe.Field<bool>(scheduler, "deleteConfirmationOpen"));
            Assert.Single(scheduler.Events);
        });
    }

    [Fact]
    public async Task AMultiDayAllDayEventIsOneBarNotAChipPerDay()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithAllDayAsync(scheduler, AllDay("conf", "Conference", 16, 19));

            var bar = Assert.Single(BandBars(scheduler));
            Assert.Equal(2, bar.StartColumn);   // Monday-start week: the 16th is Wednesday.
            Assert.Equal(3, bar.Span);          // 16th, 17th, 18th — End is exclusive.
            Assert.False(bar.ContinuesBefore);
            Assert.False(bar.ContinuesAfter);
        });
    }

    [Fact]
    public async Task AllDayEventsAreKeptOutOfTheTimeGrid()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            // original is a timed 09:00–10:00 event on the 14th and must stay in the grid.
            await WithAllDayAsync(scheduler, AllDay("conf", "Conference", 14, 15));

            Assert.Single(BandBars(scheduler));
            var placed = ComponentProbe.Field<IEnumerable<object>>(scheduler, "lanes")
                .SelectMany(lane => (IEnumerable<object>)lane.GetType().GetProperty("Placements")!.GetValue(lane)!)
                .Select(p => (SchedulerOccurrence)p.GetType().GetProperty("Occurrence")!.GetValue(p)!)
                .Select(o => o.Event.Id).ToArray();
            Assert.Equal(["event"], placed);
        });
    }

    [Fact]
    public async Task OverlappingRunsStackAndSeparateRunsShareALane()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithAllDayAsync(scheduler,
                AllDay("long", "Launch week", 16, 20),    // Wed–Sat, longest, takes lane 0
                AllDay("conf", "Conference", 17, 19),     // overlaps it, so lane 1
                AllDay("holiday", "Holiday", 14, 15));    // Monday, touches neither, back to lane 0

            var bars = BandBars(scheduler).ToDictionary(b => b.Id, b => b.Lane);
            Assert.Equal(0, bars["long"]);
            Assert.Equal(1, bars["conf"]);
            Assert.Equal(0, bars["holiday"]);
        });
    }

    [Fact]
    public async Task ARunLeavingTheVisibleWeekIsClippedAndMarkedAsContinuing()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            // The 13th is the Sunday before the Monday-start week; the 23rd is the week after.
            await WithAllDayAsync(scheduler, AllDay("long", "Long run", 13, 23));

            var bar = Assert.Single(BandBars(scheduler));
            Assert.Equal(0, bar.StartColumn);
            Assert.Equal(7, bar.Span);
            Assert.True(bar.ContinuesBefore);
            Assert.True(bar.ContinuesAfter);
        });
    }

    [Fact]
    public async Task BarsPastMaxAllDayRowsAreCountedIntoTheOverflowRatherThanGrowingTheBand()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(BbScheduler.MaxAllDayRows)] = 1 }));
            await WithAllDayAsync(scheduler,
                AllDay("a", "A", 16, 17),
                AllDay("b", "B", 16, 17));

            var row = Assert.Single(BandRows(scheduler));
            Assert.Single((System.Collections.IEnumerable)row.GetType().GetProperty("Bars")!.GetValue(row)!);
            var hidden = (int[])row.GetType().GetProperty("Hidden")!.GetValue(row)!;
            Assert.Equal(1, hidden[2]);
        });
    }

    [Fact]
    public async Task TwoVisibleResourcesGroupTheBandByResourceSoBarsStayContiguous()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var setup = AllDay("setup", "Room A setup", 15, 18);
            setup.ResourceIds = ["room-a"];
            var maint = AllDay("maint", "Room B maintenance", 16, 17);
            maint.ResourceIds = ["room-b"];
            await WithAllDayAsync(scheduler, setup, maint);
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.Resources)] = new SchedulerResource[] { new("room-a", "Room A"), new("room-b", "Room B") }
            }));

            var rows = BandRows(scheduler);
            // Two rows, not three: the unassigned timed workshop earns an Unassigned lane below,
            // but an all-day row with nothing in it is dropped.
            Assert.Equal(2, rows.Count);
            Assert.Equal(["setup"], BarsOf(rows[0]).Select(b => b.Id));
            Assert.Equal(["maint"], BarsOf(rows[1]).Select(b => b.Id));
            // Contiguous spans are the whole reason for grouping: the 15th, 16th and 17th.
            Assert.Equal(3, BarsOf(rows[0]).Single().Span);
        });
    }

    [Fact]
    public async Task NarrowingToOneResourceCollapsesTheBandBackToASingleFilteredRow()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var setup = AllDay("setup", "Room A setup", 15, 18);
            setup.ResourceIds = ["room-a"];
            var unassigned = AllDay("audit", "Site audit", 16, 18);
            await WithAllDayAsync(scheduler, setup, unassigned);
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.Resources)] = new SchedulerResource[] { new("room-a", "Room A"), new("room-b", "Room B") },
                [nameof(BbScheduler.VisibleResourceIds)] = RoomAOnly
            }));

            var row = Assert.Single(BandRows(scheduler));
            // One row again — and it still filters, so the unassigned run is not smuggled in.
            Assert.Equal(["setup"], BarsOf(row).Select(b => b.Id));
        });
    }

    private static SchedulerEvent AllDay(string id, string title, int startDay, int endDayExclusive) => new()
    {
        Id = id,
        Title = title,
        IsAllDay = true,
        Start = new DateTimeOffset(2026, 9, startDay, 0, 0, 0, TimeSpan.Zero),
        End = new DateTimeOffset(2026, 9, endDayExclusive, 0, 0, 0, TimeSpan.Zero)
    };

    private static Task WithAllDayAsync(BbScheduler scheduler, params SchedulerEvent[] allDay)
    {
        var timed = new SchedulerEvent { Id = "event", Title = "Workshop", Start = At(9), End = At(10) };
        return scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(BbScheduler.View)] = SchedulerView.Week,
            [nameof(BbScheduler.Events)] = (SchedulerEvent[])[timed, .. allDay]
        }));
    }

    private static List<object> BandRows(BbScheduler scheduler) =>
        [.. ComponentProbe.Field<IEnumerable<object>>(scheduler, "bandRows")];

    private static List<(string Id, int StartColumn, int Span, int Lane, bool ContinuesBefore, bool ContinuesAfter)> BarsOf(object row) =>
        [.. ((System.Collections.IEnumerable)row.GetType().GetProperty("Bars")!.GetValue(row)!).Cast<object>().Select(Describe)];

    private static List<(string Id, int StartColumn, int Span, int Lane, bool ContinuesBefore, bool ContinuesAfter)> BandBars(BbScheduler scheduler) =>
        [.. BandRows(scheduler).SelectMany(BarsOf)];

    private static (string Id, int StartColumn, int Span, int Lane, bool ContinuesBefore, bool ContinuesAfter) Describe(object bar)
    {
        var type = bar.GetType();
        object Get(string name) => type.GetProperty(name)!.GetValue(bar)!;
        return (((SchedulerOccurrence)Get("Occurrence")).Event.Id, (int)Get("StartColumn"), (int)Get("Span"),
            (int)Get("Lane"), (bool)Get("ContinuesBefore"), (bool)Get("ContinuesAfter"));
    }

    [Fact]
    public async Task MonthAlwaysRendersSixWeekRowsStartingOnTheWeekStart()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14));

            var weeks = MonthWeeks(scheduler);
            Assert.Equal(6, weeks.Count);
            // September 2026 starts on a Tuesday, so a Monday-start grid opens on 31 August.
            Assert.Equal(new DateOnly(2026, 8, 31), CellDates(weeks[0])[0]);
            Assert.Equal(42, weeks.Sum(w => CellDates(w).Count));
        });
    }

    [Fact]
    public async Task AFebruaryOf28DaysStillRendersSixRows()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 2, 10));
            Assert.Equal(6, MonthWeeks(scheduler).Count);
        });
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(-1, 8)]
    public async Task MonthNavigationStepsACalendarMonthNotSixWeeks(int direction, int expectedMonth)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14));
            await (Task)ComponentProbe.Call(scheduler, "NavigateAsync", direction)!;
            Assert.Equal(expectedMonth, scheduler.Date.Month);
        });
    }

    [Fact]
    public async Task MonthNavigationCrossesAYearBoundary()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 12, 15));
            await (Task)ComponentProbe.Call(scheduler, "NavigateAsync", 1)!;
            Assert.Equal(new DateOnly(2027, 1, 15), scheduler.Date);
        });
    }

    [Fact]
    public async Task AMultiDayEventCrossingAWeekBoundaryBecomesOneBarPerRowSquaredAtTheJoin()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            // Fri 18 Sep to Tue 22 Sep, over the Sunday/Monday boundary of a Monday-start week.
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14), AllDay("sprint", "Sprint", 18, 23));

            var bars = MonthWeeks(scheduler).SelectMany(BarsOf).Where(b => b.Id == "sprint").ToList();
            Assert.Equal(2, bars.Count);
            Assert.False(bars[0].ContinuesBefore);
            Assert.True(bars[0].ContinuesAfter);
            Assert.True(bars[1].ContinuesBefore);
            Assert.False(bars[1].ContinuesAfter);
        });
    }

    [Fact]
    public async Task ABarSpendsTheDayBudgetSoTheCellUnderItShowsFewerChips()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var chips = Enumerable.Range(0, 3).Select(i => new SchedulerEvent
            {
                Id = $"chip{i}", Title = $"Chip {i}",
                Start = new DateTimeOffset(2026, 9, 16, 9 + i, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 9, 16, 9 + i, 30, 0, TimeSpan.Zero)
            }).ToArray();
            // Two days, so it is a bar. A one-day all-day event is a chip like any other.
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14), 2, [AllDay("conf", "Conference", 16, 18), .. chips]);

            // Budget 2, one of which the bar takes: one chip is drawn and two go to "+x more".
            var cell = MonthWeeks(scheduler).SelectMany(CellsOf).Single(c => c.Date == new DateOnly(2026, 9, 16));
            Assert.Equal(1, cell.ChipCount);
            Assert.Equal(2, cell.Hidden);
        });
    }

    [Fact]
    public async Task MonthMarksDaysOutsideTheMonthSoTheyCanBeGreyed()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14));
            var cells = MonthWeeks(scheduler).SelectMany(CellsOf).ToList();
            Assert.False(cells.Single(c => c.Date == new DateOnly(2026, 8, 31)).InMonth);
            Assert.True(cells.Single(c => c.Date == new DateOnly(2026, 9, 1)).InMonth);
        });
    }

    [Fact]
    public async Task MonthRefusesAForgedDragBecauseItsGeometryDoesNotExist()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14));

            // Month builds no lanes, so a commit cannot resolve one — the guard that stops the
            // pointer code writing garbage times from a cell that has no 40px-per-slot scale.
            await scheduler.CommitInteractionAsync(ComponentProbe.Field<int>(scheduler, "revision"), original.Id,
                original.Start.ToUnixTimeMilliseconds(), 0, 0,
                At(11).ToUnixTimeMilliseconds(), At(12).ToUnixTimeMilliseconds(), "move");

            Assert.Equal(At(9), scheduler.Events[0].Start);
        });
    }

    [Fact]
    public async Task ClickingADayInMonthSelectsItAndEnterCreatesAtStartHour()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14));
            var day = new DateOnly(2026, 9, 16);

            ComponentProbe.Call(scheduler, "SelectDay", day);
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsDaySelected", day)!);
            Assert.False(ComponentProbe.Field<bool>(scheduler, "editorOpen"));

            ComponentProbe.Call(scheduler, "DayKeyDown", new KeyboardEventArgs { Key = "Enter" }, day);

            Assert.True(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
            var draft = ComponentProbe.Field<SchedulerEvent>(scheduler, "draft");
            Assert.Equal(new DateTimeOffset(2026, 9, 16, 8, 0, 0, TimeSpan.Zero), draft.Start);
            Assert.Equal(30, (draft.End - draft.Start).TotalMinutes);
        });
    }

    [Fact]
    public async Task TheToolbarContextCarriesTheHeadingAndTheCurrentView()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14));

            var context = (SchedulerToolbarContext)ComponentProbe.Call(scheduler, "BuildToolbarContext", (RenderFragment)(builder => { }))!;

            Assert.Equal(SchedulerView.Month, context.View);
            Assert.Equal(new DateOnly(2026, 9, 14), context.Date);
            Assert.Equal(new DateOnly(2026, 8, 31), context.RangeStart);
            Assert.Contains("2026", context.Heading, StringComparison.Ordinal);
            Assert.NotNull(context.DefaultContent);
        });
    }

    [Fact]
    public async Task TheToolbarContextNavigateUsesTheCurrentViewsStepSize()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14));
            var month = (SchedulerToolbarContext)ComponentProbe.Call(scheduler, "BuildToolbarContext", (RenderFragment)(builder => { }))!;
            await month.Navigate.InvokeAsync(1);
            Assert.Equal(new DateOnly(2026, 10, 14), scheduler.Date);

            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(BbScheduler.View)] = SchedulerView.Week }));
            var week = (SchedulerToolbarContext)ComponentProbe.Call(scheduler, "BuildToolbarContext", (RenderFragment)(builder => { }))!;
            await week.Navigate.InvokeAsync(1);
            Assert.Equal(new DateOnly(2026, 10, 21), scheduler.Date);
        });
    }

    private static Task InMonthAsync(BbScheduler scheduler, DateOnly date, params SchedulerEvent[] events) =>
        InMonthAsync(scheduler, date, 3, events);

    private static Task InMonthAsync(BbScheduler scheduler, DateOnly date, int maxPerDay, params SchedulerEvent[] events)
    {
        var timed = new SchedulerEvent { Id = "event", Title = "Workshop", Start = At(9), End = At(10) };
        return scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(BbScheduler.View)] = SchedulerView.Month,
            [nameof(BbScheduler.Date)] = date,
            [nameof(BbScheduler.MaxEventsPerDay)] = maxPerDay,
            [nameof(BbScheduler.Events)] = (SchedulerEvent[])[timed, .. events]
        }));
    }

    private static List<object> MonthWeeks(BbScheduler scheduler) =>
        [.. ComponentProbe.Field<IEnumerable<object>>(scheduler, "monthWeeks")];

    private static List<DateOnly> CellDates(object week) => [.. CellsOf(week).Select(c => c.Date)];

    private static List<(DateOnly Date, bool InMonth, int ChipCount, int Hidden)> CellsOf(object week) =>
        [.. ((System.Collections.IEnumerable)week.GetType().GetProperty("Cells")!.GetValue(week)!).Cast<object>().Select(cell =>
        {
            var type = cell.GetType();
            object Get(string name) => type.GetProperty(name)!.GetValue(cell)!;
            return ((DateOnly)Get("Date"), (bool)Get("InMonth"),
                ((System.Collections.ICollection)Get("Chips")).Count, (int)Get("HiddenCount"));
        })];

    [Fact]
    public async Task ASingleClickOnAnEventHighlightsItWithoutOpeningTheEditor()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var occurrence = new SchedulerOccurrence(original, original.Start, original.End);

            ComponentProbe.Call(scheduler, "SelectOccurrence", occurrence);

            Assert.True((bool)ComponentProbe.Call(scheduler, "IsOccurrenceSelected", occurrence)!);
            Assert.False(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
        });
    }

    [Theory]
    [InlineData("Enter", true)]
    [InlineData(" ", false)]
    public async Task EnterOpensTheEditorForAHighlightedEventAndSpaceDoesNot(string key, bool opens)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            ComponentProbe.Call(scheduler, "OccurrenceKeyDown", new KeyboardEventArgs { Key = key },
                new SchedulerOccurrence(original, original.Start, original.End));

            Assert.Equal(opens, ComponentProbe.Field<bool>(scheduler, "editorOpen"));
        });
    }

    [Fact]
    public async Task OnlyOneThingIsHighlightedAtATime()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var occurrence = new SchedulerOccurrence(original, original.Start, original.End);

            ComponentProbe.Call(scheduler, "SelectSlot", 0, At(11));
            ComponentProbe.Call(scheduler, "SelectOccurrence", occurrence);
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(11))!);

            ComponentProbe.Call(scheduler, "SelectSlot", 0, At(11));
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsOccurrenceSelected", occurrence)!);
        });
    }

    [Fact]
    public async Task TwoOccurrencesOfOneSeriesHighlightIndependently()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.RecurrenceRule = "FREQ=DAILY;COUNT=3";
            await scheduler.SetParametersAsync(ParameterView.Empty);
            var first = new SchedulerOccurrence(original, At(9), At(10));
            var second = new SchedulerOccurrence(original, At(9).AddDays(1), At(10).AddDays(1));

            ComponentProbe.Call(scheduler, "SelectOccurrence", second);

            Assert.False((bool)ComponentProbe.Call(scheduler, "IsOccurrenceSelected", first)!);
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsOccurrenceSelected", second)!);
        });
    }

    [Fact]
    public async Task DraggingAcrossDaysInWeekViewKeepsTheWeekView()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            SchedulerView? reportedView = null;
            DateOnly? reportedDate = null;
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.View)] = SchedulerView.Week,
                [nameof(BbScheduler.ViewChanged)] = EventCallback.Factory.Create<SchedulerView>(scheduler, v => reportedView = v),
                [nameof(BbScheduler.DateChanged)] = EventCallback.Factory.Create<DateOnly>(scheduler, d => reportedDate = d)
            }));

            // Lane 0 is Monday the 14th, lane 1 the Tuesday: a move to another day of the same week.
            await scheduler.CommitInteractionAsync(ComponentProbe.Field<int>(scheduler, "revision"), original.Id,
                original.Start.ToUnixTimeMilliseconds(), 0, 1,
                At(9).AddDays(1).ToUnixTimeMilliseconds(), At(10).AddDays(1).ToUnixTimeMilliseconds(), "move");

            Assert.Equal(At(9).AddDays(1), scheduler.Events[0].Start);
            // A save must not move the schedule: neither callback should have fired.
            Assert.Equal(SchedulerView.Week, scheduler.View);
            Assert.Null(reportedView);
            Assert.Null(reportedDate);
        });
    }

    [Fact]
    public async Task MonthWithAResourceSelectedShowsTheGridNotTheEmptyState()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.ResourceIds = ["room-b"];
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.View)] = SchedulerView.Month,
                [nameof(BbScheduler.Resources)] = new SchedulerResource[] { new("room-a", "Room A"), new("room-b", "Room B") },
                [nameof(BbScheduler.VisibleResourceIds)] = RoomBOnly
            }));

            // Month builds no lanes, so the empty state must not infer emptiness from the lane count.
            Assert.False((bool)ComponentProbe.Call(scheduler, "get_NothingSelected")!);
            Assert.Equal(6, MonthWeeks(scheduler).Count);
        });
    }

    [Fact]
    public async Task MonthWithEveryResourceDeselectedStillShowsTheEmptyState()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.View)] = SchedulerView.Month,
                [nameof(BbScheduler.Resources)] = new SchedulerResource[] { new("room-a", "Room A") },
                [nameof(BbScheduler.VisibleResourceIds)] = Array.Empty<string>()
            }));

            Assert.True((bool)ComponentProbe.Call(scheduler, "get_NothingSelected")!);
        });
    }

    [Fact]
    public async Task MonthFiltersItsOwnGridByTheVisibleResources()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var roomB = new SchedulerEvent
            {
                Id = "b-only", Title = "Room B only", ResourceIds = ["room-b"],
                Start = new DateTimeOffset(2026, 9, 16, 9, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero)
            };
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.View)] = SchedulerView.Month,
                [nameof(BbScheduler.Events)] = new[] { roomB },
                [nameof(BbScheduler.Resources)] = new SchedulerResource[] { new("room-a", "Room A"), new("room-b", "Room B") },
                [nameof(BbScheduler.VisibleResourceIds)] = RoomAOnly
            }));

            var chips = MonthWeeks(scheduler).SelectMany(CellsOf).Sum(c => c.ChipCount);
            Assert.Equal(0, chips);
        });
    }

    [Fact]
    public async Task NoActiveHoursMutesNothing()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(3))!);
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(23))!);
        });
    }

    [Theory]
    [InlineData(7, true)]    // before the range
    [InlineData(8, false)]   // inclusive start
    [InlineData(16, false)]
    [InlineData(17, true)]   // exclusive end
    [InlineData(20, true)]
    public async Task SlotsOutsideTheDaysRangeAreMuted(int hour, bool muted)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            // 2026-09-14 is a Monday.
            await WithActiveHoursAsync(scheduler, new SchedulerDayHours(DayOfWeek.Monday, new(8, 0), new(17, 0)));
            Assert.Equal(muted, (bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(hour))!);
        });
    }

    [Fact]
    public async Task ADayWithNoRangeIsMutedEndToEnd()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler, new SchedulerDayHours(DayOfWeek.Tuesday, new(8, 0), new(17, 0)));
            // Monday has no entry, so every hour of it is muted.
            Assert.All(MutedProbeHours,
                hour => Assert.True((bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(hour))!));
        });
    }

    [Theory]
    [InlineData(11, false)]  // morning range
    [InlineData(12, true)]   // the gap
    [InlineData(13, false)]  // afternoon range
    public async Task TwoRangesOnOneDayMuteTheGapBetweenThem(int hour, bool muted)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler,
                new SchedulerDayHours(DayOfWeek.Monday, new(8, 0), new(12, 0)),
                new SchedulerDayHours(DayOfWeek.Monday, new(13, 0), new(17, 0)));
            Assert.Equal(muted, (bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(hour))!);
        });
    }

    [Fact]
    public async Task HalfHourBoundariesAreHonouredNotRoundedToTheHour()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler, new SchedulerDayHours(DayOfWeek.Monday, new(8, 30), new(17, 0)));
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(8, 0))!);
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(8, 30))!);
        });
    }

    [Fact]
    public async Task AnEndOfMidnightRunsToTheEndOfTheDay()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler, new SchedulerDayHours(DayOfWeek.Monday, new(18, 0), TimeOnly.MinValue));
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(17))!);
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(23, 30))!);
        });
    }

    [Fact]
    public async Task AnInvertedRangeIsRejected()
    {
        await RunAsync((renderer, scheduler, original) =>
            Assert.ThrowsAsync<ArgumentException>(() => WithActiveHoursAsync(scheduler,
                new SchedulerDayHours(DayOfWeek.Monday, new(17, 0), new(8, 0)))));
    }

    [Fact]
    public async Task MutingAloneLeavesSlotsUsable()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler, new SchedulerDayHours(DayOfWeek.Monday, new(8, 0), new(17, 0)));

            // BlockOutsideActiveHours is off, so a muted slot still creates.
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotBlocked", At(20))!);
            scheduler.CreateEvent(At(20));
            Assert.True(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
        });
    }

    [Fact]
    public async Task BlockingClosesTheSlotsOutsideTheActiveHours()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler, true, new SchedulerDayHours(DayOfWeek.Monday, new(8, 0), new(17, 0)));

            Assert.True((bool)ComponentProbe.Call(scheduler, "IsSlotBlocked", At(20))!);
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotBlocked", At(9))!);

            ComponentProbe.Call(scheduler, "SlotKeyDown", new KeyboardEventArgs { Key = "Enter" }, At(20), (string?)null);
            Assert.False(ComponentProbe.Field<bool>(scheduler, "editorOpen"));

            ComponentProbe.Call(scheduler, "SlotKeyDown", new KeyboardEventArgs { Key = "Enter" }, At(9), (string?)null);
            Assert.True(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
        });
    }

    [Fact]
    public async Task BlockingRefusesADragThatLandsOutsideTheActiveHours()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler, true, new SchedulerDayHours(DayOfWeek.Monday, new(8, 0), new(17, 0)));

            await Gesture(scheduler, original, At(20), At(21), "move");
            Assert.Equal(At(9), scheduler.Events[0].Start);

            // A move that stays inside the range still works.
            await Gesture(scheduler, original, At(11), At(12), "move");
            Assert.Equal(At(11), scheduler.Events[0].Start);
        });
    }

    [Fact]
    public async Task BlockingRefusesAResizeThatSpillsPastTheActiveHours()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler, true, new SchedulerDayHours(DayOfWeek.Monday, new(8, 0), new(17, 0)));

            // 09:00 to 18:00 crosses the 17:00 edge, so every minute is not inside the range.
            await Gesture(scheduler, original, At(9), At(18), "end");
            Assert.Equal(At(10), scheduler.Events[0].End);
        });
    }

    [Fact]
    public async Task MonthClosesOnlyTheDaysWithNoActiveRangeAtAll()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.View)] = SchedulerView.Month,
                [nameof(BbScheduler.BlockOutsideActiveHours)] = true,
                [nameof(BbScheduler.ActiveHours)] = new SchedulerDayHours[]
                {
                    new(DayOfWeek.Monday, new(8, 0), new(17, 0))
                }
            }));

            // Monday is partly active, so its cell stays open; Tuesday has no range at all.
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsDayBlocked", new DateOnly(2026, 9, 14))!);
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsDayBlocked", new DateOnly(2026, 9, 15))!);

            ComponentProbe.Call(scheduler, "CreateEventOnDay", new DateOnly(2026, 9, 15));
            Assert.False(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
        });
    }

    [Fact]
    public async Task NoSuppliedZonesKeepsTheFullIanaList()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.EditEvent(new(original, original.Start, original.End));
            var zones = ((IEnumerable<SchedulerTimeZone>)ComponentProbe.Call(scheduler, "get_EditorTimeZones")!).ToList();
            Assert.True(zones.Count > 100, $"expected the full TZDB list, got {zones.Count}");
            Assert.Contains(zones, z => z.Id == "UTC");
        });
    }

    [Fact]
    public async Task SuppliedZonesReplaceTheListAndCarryTheirOwnNames()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithZonesAsync(scheduler, new SchedulerTimeZone("UTC", "Coordinated"),
                new SchedulerTimeZone("Australia/Sydney", "Sydney"));
            scheduler.EditEvent(new(original, original.Start, original.End));

            var zones = ((IEnumerable<SchedulerTimeZone>)ComponentProbe.Call(scheduler, "get_EditorTimeZones")!).ToList();
            Assert.Equal(["UTC", "Australia/Sydney"], zones.Select(z => z.Id));
            Assert.Equal("Sydney", (string)ComponentProbe.Call(scheduler, "TimeZoneTitle", "Australia/Sydney")!);
        });
    }

    [Fact]
    public async Task AZoneAnEventAlreadyUsesIsOfferedEvenWhenItWasNotListed()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.TimeZoneId = "Europe/London";
            await WithZonesAsync(scheduler, new SchedulerTimeZone("UTC", "Coordinated"));
            scheduler.EditEvent(new(original, original.Start, original.End));

            // Otherwise opening this event would quietly move it to whichever zone sorted first.
            var zones = ((IEnumerable<SchedulerTimeZone>)ComponentProbe.Call(scheduler, "get_EditorTimeZones")!).ToList();
            Assert.Contains(zones, z => z.Id == "Europe/London");
        });
    }

    [Theory]
    [InlineData("Not/AZone")]
    [InlineData("")]
    public async Task AnUnusableZoneIsRejected(string id)
    {
        await RunAsync((renderer, scheduler, original) =>
            Assert.ThrowsAsync<ArgumentException>(() => WithZonesAsync(scheduler, new SchedulerTimeZone(id, "Anything"))));
    }

    [Fact]
    public async Task DuplicateZonesAreRejected()
    {
        await RunAsync((renderer, scheduler, original) =>
            Assert.ThrowsAsync<ArgumentException>(() => WithZonesAsync(scheduler,
                new SchedulerTimeZone("UTC", "One"), new SchedulerTimeZone("UTC", "Two"))));
    }

    [Fact]
    public void ADerivedEventSurvivesAClone()
    {
        var item = new DescribedEvent
        {
            Id = "e", Title = "Workshop", Start = At(9), End = At(10),
            Description = "Bring the agenda", ResourceIds = ["room-a"]
        };

        var copy = Assert.IsType<DescribedEvent>(item.Clone());

        Assert.Equal("Bring the agenda", copy.Description);
        Assert.Equal("Workshop", copy.Title);
        Assert.Equal(["room-a"], copy.ResourceIds);
        Assert.NotSame(item.ResourceIds, copy.ResourceIds);
    }

    [Fact]
    public async Task ADerivedEventKeepsItsOwnFieldsThroughAnEditAndSave()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var described = new DescribedEvent
            {
                Id = "described", Title = "Workshop", Start = At(9), End = At(10), Description = "Original"
            };
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(BbScheduler.Events)] = new SchedulerEvent[] { described } }));

            scheduler.EditEvent(new(described, described.Start, described.End));
            var draft = Assert.IsType<DescribedEvent>(ComponentProbe.Field<SchedulerEvent>(scheduler, "draft"));
            draft.Description = "Edited";
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;

            var saved = Assert.IsType<DescribedEvent>(Assert.Single(scheduler.Events));
            Assert.Equal("Edited", saved.Description);
            // The source object must not have been mutated in place.
            Assert.Equal("Original", described.Description);
        });
    }

    [Fact]
    public async Task ADerivedEventSurvivesADrag()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var described = new DescribedEvent
            {
                Id = "described", Title = "Workshop", Start = At(9), End = At(10), Description = "Keep me"
            };
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(BbScheduler.Events)] = new SchedulerEvent[] { described } }));

            await Gesture(scheduler, described, At(11), At(12), "move");

            var moved = Assert.IsType<DescribedEvent>(Assert.Single(scheduler.Events));
            Assert.Equal(At(11), moved.Start);
            Assert.Equal("Keep me", moved.Description);
        });
    }

    [Fact]
    public async Task NewEventFactoryDecidesTheTypeOfACreatedEvent()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BbScheduler.NewEventFactory)] = (Func<SchedulerEvent>)(() => new DescribedEvent { Description = "New" })
            }));

            scheduler.CreateEvent(At(11));

            var draft = Assert.IsType<DescribedEvent>(ComponentProbe.Field<SchedulerEvent>(scheduler, "draft"));
            Assert.Equal("New", draft.Description);
            Assert.Equal(At(11), draft.Start);
            Assert.Equal(30, (draft.End - draft.Start).TotalMinutes);
        });
    }

    [Fact]
    public async Task WithoutAFactoryACreatedEventIsStillTheBaseType()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.CreateEvent(At(11));
            Assert.Equal(typeof(SchedulerEvent), ComponentProbe.Field<SchedulerEvent>(scheduler, "draft").GetType());
        });
    }

    [Fact]
    public async Task TheEditorContextReportsWhatIsBeingEdited()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.CreateEvent(At(11));
            var create = (SchedulerEditorContext)ComponentProbe.Call(scheduler, "BuildEditorContext", (RenderFragment)(builder => { }))!;
            Assert.True(create.IsNew);

            scheduler.EditEvent(new(original, original.Start, original.End), SchedulerEditScope.Series);
            var edit = (SchedulerEditorContext)ComponentProbe.Call(scheduler, "BuildEditorContext", (RenderFragment)(builder => { }))!;
            Assert.False(edit.IsNew);
            Assert.Equal(SchedulerEditScope.Series, edit.Scope);
            Assert.Equal("Workshop", edit.Draft.Title);
            Assert.NotNull(edit.DefaultContent);
        });
    }

    private sealed class DescribedEvent : SchedulerEvent
    {
        public string? Description { get; set; }

        public override SchedulerEvent Clone()
        {
            var copy = new DescribedEvent { Description = Description };
            CopyTo(copy);
            return copy;
        }
    }

    private static Task WithZonesAsync(BbScheduler scheduler, params SchedulerTimeZone[] zones) =>
        scheduler.SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?> { [nameof(BbScheduler.TimeZones)] = zones }));

    private static readonly int[] MutedProbeHours = [0, 9, 13, 23];

    private static Task WithActiveHoursAsync(BbScheduler scheduler, params SchedulerDayHours[] hours) =>
        WithActiveHoursAsync(scheduler, false, hours);

    private static Task WithActiveHoursAsync(BbScheduler scheduler, bool block, params SchedulerDayHours[] hours) =>
        scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(BbScheduler.StartHour)] = 0,
            [nameof(BbScheduler.EndHour)] = 24,
            [nameof(BbScheduler.ActiveHours)] = hours,
            [nameof(BbScheduler.BlockOutsideActiveHours)] = block
        }));

    private static readonly string[] RoomAOnly = ["room-a"];
    private static readonly string[] RoomBOnly = ["room-b"];

    private static Task WithResourcesAsync(BbScheduler scheduler, SchedulerEvent original, IReadOnlyList<string>? visible)
    {
        original.ResourceIds = ["room-a"];
        return scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(BbScheduler.Resources)] = new SchedulerResource[] { new("room-a", "Room A"), new("room-b", "Room B") },
            [nameof(BbScheduler.VisibleResourceIds)] = visible
        }));
    }

    private static string[] LaneResourceIds(BbScheduler scheduler) => ComponentProbe.Field<IEnumerable<object>>(scheduler, "lanes")
        .Select(lane => (SchedulerResource?)lane.GetType().GetProperty("Resource")!.GetValue(lane))
        .Select(resource => resource?.Id ?? "<none>").ToArray();

    private static DateOnly[] LaneDates(BbScheduler scheduler) => ComponentProbe.Field<IEnumerable<object>>(scheduler, "lanes")
        .Select(lane => (DateOnly)lane.GetType().GetProperty("Date")!.GetValue(lane)!).ToArray();

    private static Task Gesture(BbScheduler scheduler, SchedulerEvent original, DateTimeOffset start, DateTimeOffset end, string action, int target = 0) =>
        scheduler.CommitInteractionAsync(ComponentProbe.Field<int>(scheduler, "revision"), original.Id, original.Start.ToUnixTimeMilliseconds(), 0, target,
            start.ToUnixTimeMilliseconds(), end.ToUnixTimeMilliseconds(), action);
    private static DateTimeOffset At(int hour, int minute = 0) => new(2026, 9, 14, hour, minute, 0, TimeSpan.Zero);

    private static async Task RunAsync(Func<ComponentTestRenderer, BbScheduler, SchedulerEvent, Task> test)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddBlazorBlueprintComponents();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<BlazorBlueprint.Primitives.BbPortalHost>(new());
            var original = new SchedulerEvent { Id = "event", Title = "Workshop", Start = At(9), End = At(10) };
            var scheduler = await renderer.MountAsync<BbScheduler>(new()
            {
                [nameof(BbScheduler.Date)] = new DateOnly(2026, 9, 14),
                [nameof(BbScheduler.View)] = SchedulerView.Day,
                [nameof(BbScheduler.Events)] = new SchedulerEvent[] { original }
            });
            await test(renderer, scheduler, original);
        });
    }
}
