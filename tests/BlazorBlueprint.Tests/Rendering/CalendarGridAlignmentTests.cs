using System.Text.RegularExpressions;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorBlueprint.Tests.Rendering;

/// <summary>
/// The calendar's day grid is centred in the table, not left-aligned.
/// <para>
/// The seven day cells are a fixed <c>w-9</c> each — 252px — while the table is as wide as the
/// month and year selects above it, which is wider. Left-aligning the rows left the difference as
/// dead space on the right of the grid. It only became visible when the year select was widened
/// from 80px to 100px (b39be911) to stop the year truncating, which is why it looks like it
/// appeared from nowhere.
/// </para>
/// <para>
/// Asserted on the rendered rows rather than the source string so the header row and the week rows
/// are both covered, and so a row added later cannot quietly miss it.
/// </para>
/// </summary>
public class CalendarGridAlignmentTests
{
    [Fact]
    public async Task EveryRowOfTheDayGridIsFullWidthAndCentred()
    {
        var markup = await RenderAsync();
        var rows = Regex.Matches(markup, @"<tr class=""(?<cls>[^""]*)""")
            .Select(m => m.Groups["cls"].Value)
            .ToList();

        // One header row of day names, plus a row per week shown.
        Assert.True(rows.Count >= 5, $"Expected the calendar to render a header row and several week rows; got {rows.Count}.");

        foreach (var cls in rows)
        {
            Assert.True(
                cls.Contains("bb:justify-center", StringComparison.Ordinal),
                $"A calendar row is missing bb:justify-center, so its day cells sit left of the table's "
                + $"full width and leave dead space on the right. Row classes: '{cls}'");

            Assert.True(
                cls.Contains("bb:w-full", StringComparison.Ordinal),
                $"A calendar row is missing bb:w-full, so it shrinks to its cells and has nothing to "
                + $"centre within. Row classes: '{cls}'");
        }
    }

    [Fact]
    public async Task TheDayNameHeaderUsesTheSameCellWidthAsTheDays()
    {
        // Centring only keeps the day names over their columns while both are the same fixed width.
        var markup = await RenderAsync();

        var headerCells = Regex.Matches(markup, @"<th scope=""col"" class=""(?<cls>[^""]*)""")
            .Select(m => m.Groups["cls"].Value)
            .ToList();

        Assert.Equal(7, headerCells.Count);
        Assert.All(headerCells, cls => Assert.Contains("bb:w-9", cls, StringComparison.Ordinal));
        Assert.Contains("bb:h-9 bb:w-9", markup, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync()
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
            await renderer.MountAsync<BbCalendar>(new()
            {
                [nameof(BbCalendar.Selected)] = new DateTime(2026, 9, 19),
                [nameof(BbCalendar.FirstDayOfWeek)] = DayOfWeek.Sunday,
            });

            return renderer.Markup();
        });
    }
}
