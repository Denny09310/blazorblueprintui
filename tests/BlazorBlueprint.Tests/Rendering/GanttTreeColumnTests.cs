using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorBlueprint.Tests.Rendering;

/// <summary>
/// The tree column in <see cref="BbGantt{TItem}"/> shows the task's name when it has nothing else
/// to show.
/// </summary>
/// <remarks>
/// Which column carries the tree is the chart's decision: <c>IsTree</c> left unset on every column
/// gives the job to the first one declared. The column used to answer that question from its own
/// parameter, so a first column that had never been told it was the tree column returned nothing —
/// expander and indent drawn, no name beside them. Only a chart that declared no columns escaped
/// it, because the built-in default column sets <c>IsTree</c> on itself.
/// </remarks>
public class GanttTreeColumnTests
{
    private sealed record Job(string Id, string Name, DateTimeOffset Start, DateTimeOffset End);

    [Fact]
    public async Task ADeclaredFirstColumnShowsTheTaskNameWithoutBeingToldItIsTheTree()
    {
        // The reported case: one column, no Value, no ChildContent, no IsTree.
        var markup = await Render(builder =>
        {
            builder.OpenComponent<BbGanttColumn<Job>>(0);
            builder.AddAttribute(1, nameof(BbGanttColumn<Job>.Title), "Task");
            builder.AddAttribute(2, nameof(BbGanttColumn<Job>.Width), 200);
            builder.CloseComponent();
        });

        Assert.Contains("Write the thing", VisibleText(markup), StringComparison.Ordinal);
        Assert.Contains("Ship the thing", VisibleText(markup), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADeclaredColumnThatSaysItIsTheTreeStillShowsTheName()
    {
        var markup = await Render(builder =>
        {
            builder.OpenComponent<BbGanttColumn<Job>>(0);
            builder.AddAttribute(1, nameof(BbGanttColumn<Job>.Title), "Task");
            builder.AddAttribute(2, nameof(BbGanttColumn<Job>.IsTree), true);
            builder.CloseComponent();
        });

        Assert.Contains("Write the thing", VisibleText(markup), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASortableTreeColumnStillShowsTheName()
    {
        // Sorting already had the right fallback, so it kept working while the cell went blank.
        // Worth holding, because the two paths read the same parameter and only one was wrong.
        var markup = await Render(builder =>
        {
            builder.OpenComponent<BbGanttColumn<Job>>(0);
            builder.AddAttribute(1, nameof(BbGanttColumn<Job>.Title), "Task");
            builder.AddAttribute(2, nameof(BbGanttColumn<Job>.Sortable), true);
            builder.CloseComponent();
        });

        Assert.Contains("Write the thing", VisibleText(markup), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASecondColumnWithNothingToReadStaysEmpty()
    {
        // The other half. Falling back to the task name for every column would print it in every
        // unconfigured cell, which is the blunt fix this must not become.
        //
        // Compared against the same chart with one column rather than counted outright: the name
        // legitimately appears more than once per row already — as the cell's title attribute, and
        // in the aria-label on the expander — so an absolute count would be asserting the wrong
        // thing. What matters is that adding an empty column adds no more of them.
        var oneColumn = await Render(builder =>
        {
            builder.OpenComponent<BbGanttColumn<Job>>(0);
            builder.AddAttribute(1, nameof(BbGanttColumn<Job>.Title), "Task");
            builder.CloseComponent();
        });

        var twoColumns = await Render(builder =>
        {
            builder.OpenComponent<BbGanttColumn<Job>>(0);
            builder.AddAttribute(1, nameof(BbGanttColumn<Job>.Title), "Task");
            builder.CloseComponent();

            builder.OpenComponent<BbGanttColumn<Job>>(10);
            builder.AddAttribute(11, nameof(BbGanttColumn<Job>.Title), "Owner");
            builder.CloseComponent();
        });

        Assert.Equal(
            Occurrences(VisibleText(oneColumn), "Write the thing"),
            Occurrences(VisibleText(twoColumns), "Write the thing"));
        Assert.Equal(
            Occurrences(VisibleText(oneColumn), "Ship the thing"),
            Occurrences(VisibleText(twoColumns), "Ship the thing"));

        // And the name is drawn at all, or two empty columns would also compare equal.
        Assert.Contains("Write the thing", VisibleText(twoColumns), StringComparison.Ordinal);

        // And the second column really was drawn, or the comparison proves nothing.
        Assert.Contains("Owner", twoColumns, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AColumnWithItsOwnValueShowsThatRatherThanTheName()
    {
        var markup = await Render(builder =>
        {
            builder.OpenComponent<BbGanttColumn<Job>>(0);
            builder.AddAttribute(1, nameof(BbGanttColumn<Job>.Title), "Task");
            builder.AddAttribute(2, nameof(BbGanttColumn<Job>.Value), (Func<Job, object?>)(j => j.Id));
            builder.CloseComponent();
        });

        Assert.Contains("job-1", VisibleText(markup), StringComparison.Ordinal);

        // And the name is not also printed. A column that reads something must show that.
        Assert.DoesNotContain("Write the thing", VisibleText(markup), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChartWithNoDeclaredColumnsStillShowsTheName()
    {
        // The case that always worked, held so the default column cannot regress with the rest.
        var markup = await Render(columns: null);

        Assert.Contains("Write the thing", VisibleText(markup), StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------

    /// <summary>
    /// Strips the attributes that also carry the task's name, leaving only what is on screen.
    /// </summary>
    /// <remarks>
    /// The cell writes the name into a <c>title</c> for the truncation tooltip, and the expander
    /// and the drag handle both put it in an <c>aria-label</c>. Searching the raw markup therefore
    /// finds the name whether or not the cell drew it — which is exactly how the first version of
    /// these tests passed against the bug they were written for.
    /// </remarks>
    private static string VisibleText(string markup) =>
        System.Text.RegularExpressions.Regex.Replace(markup, "(title|aria-label)=\"[^\"]*\"", string.Empty);

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        var at = haystack.IndexOf(needle, StringComparison.Ordinal);

        while (at >= 0)
        {
            count++;
            at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private static async Task<string> Render(Action<RenderTreeBuilder>? columns)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddBlazorBlueprintComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var jobs = new List<Job>
        {
            new("job-1", "Write the thing", start, start.AddDays(4)),
            new("job-2", "Ship the thing", start.AddDays(3), start.AddDays(9)),
        };

        var parameters = new Dictionary<string, object?>
        {
            [nameof(BbGantt<Job>.Data)] = jobs,
            [nameof(BbGantt<Job>.IdSelector)] = (Func<Job, string>)(j => j.Id),
            [nameof(BbGantt<Job>.TextSelector)] = (Func<Job, string>)(j => j.Name),
            [nameof(BbGantt<Job>.StartSelector)] = (Func<Job, DateTimeOffset>)(j => j.Start),
            [nameof(BbGantt<Job>.EndSelector)] = (Func<Job, DateTimeOffset>)(j => j.End),
            [nameof(BbGantt<Job>.ScrollToToday)] = false,

            // Both of these print the task's name somewhere other than the task list — the hover
            // card in full, the legend by example — so either one left on lets the name be found
            // in the markup whether or not the cell drew it. That is how the first version of
            // these tests passed against the bug they were written for.
            [nameof(BbGantt<Job>.ShowTooltip)] = false,
            [nameof(BbGantt<Job>.ShowLegend)] = false,
            [nameof(BbGantt<Job>.ShowTaskLabels)] = false,
        };

        if (columns is not null)
        {
            parameters[nameof(BbGantt<Job>.Columns)] = (RenderFragment)(builder => columns(builder));
        }

        var markup = string.Empty;

        await renderer.Dispatcher.InvokeAsync(async () => await renderer.MountAsync<BbGantt<Job>>(parameters));

        // The chart is built in OnAfterRender, so the first markup is the empty message.
        for (var i = 0; i < 6; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
            await Task.Delay(10);
        }

        await renderer.Dispatcher.InvokeAsync(() => markup = renderer.Markup());
        return markup;
    }
}
