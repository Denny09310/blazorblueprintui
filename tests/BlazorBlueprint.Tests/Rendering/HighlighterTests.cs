using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorBlueprint.Tests.Rendering;

/// <summary>
/// <see cref="BbHighlighter"/> splits a string into matched and unmatched runs.
/// <para>
/// The splitting is the whole component, and the cases that break it are the ones a search box
/// produces by accident: two terms that overlap, a term that appears inside another word, a term
/// that is also HTML. Each would render as something other than the original text if the segments
/// were wrong, so they are checked against the rendered markup rather than the segment list.
/// </para>
/// </summary>
public class HighlighterTests
{
    [Fact]
    public async Task AMatchIsWrappedInAMarkAndTheRestIsNot()
    {
        var markup = await RenderAsync("Data Grid columns", parameters =>
            parameters[nameof(BbHighlighter.Query)] = "grid");

        Assert.Contains("<mark", markup, StringComparison.Ordinal);
        Assert.Contains("Grid", markup, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(markup, "<mark"));
    }

    [Fact]
    public async Task MatchingIsCaseInsensitiveByDefault()
    {
        var markup = await RenderAsync("GRID grid Grid", parameters =>
            parameters[nameof(BbHighlighter.Query)] = "grid");

        Assert.Equal(3, Occurrences(markup, "<mark"));
    }

    [Fact]
    public async Task CaseSensitiveMatchingOnlyTakesTheExactCase()
    {
        var markup = await RenderAsync("GRID grid Grid", parameters =>
        {
            parameters[nameof(BbHighlighter.Query)] = "grid";
            parameters[nameof(BbHighlighter.CaseSensitive)] = true;
        });

        Assert.Equal(1, Occurrences(markup, "<mark"));
    }

    [Fact]
    public async Task OverlappingTermsMergeIntoOneMark()
    {
        // "grid col" and "columns" overlap on "col". Nesting them would emit a mark inside a mark.
        var markup = await RenderAsync("Data grid columns", parameters =>
            parameters[nameof(BbHighlighter.Queries)] =
                (IReadOnlyList<string>)["grid col", "columns"]);

        Assert.Equal(1, Occurrences(markup, "<mark"));
        Assert.Contains("grid columns", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WholeWordDoesNotMatchInsideAWord()
    {
        var markup = await RenderAsync("A grid of gridlines and hybrid", parameters =>
        {
            parameters[nameof(BbHighlighter.Query)] = "grid";
            parameters[nameof(BbHighlighter.WholeWord)] = true;
        });

        // "grid" and "gridlines" start a word; the "brid" inside "hybrid" does not.
        Assert.Equal(2, Occurrences(markup, "<mark"));
    }

    [Fact]
    public async Task AngleBracketsInTheTextAreMatchedLikeAnyOtherCharacters()
    {
        // The term itself is HTML. It should match as text and mark that run, nothing more.
        var markup = await RenderAsync("Angle <b>brackets</b> stay literal", parameters =>
            parameters[nameof(BbHighlighter.Query)] = "<b>");

        Assert.Equal(1, Occurrences(markup, "<mark"));
    }

    [Fact]
    public void TheComponentNeverRendersItsTextAsMarkup()
    {
        // Escaping is the framework's job once the value reaches it as a string, so the property
        // worth guarding is that no MarkupString is ever constructed here. The test renderer
        // cannot tell the two apart in its output, which is why this is checked as source.
        var sources = Conventions.SourceTree.ComponentSources
            .Where(f => f.Name.StartsWith("BbHighlighter", StringComparison.Ordinal))
            .Select(f => File.ReadAllText(f.FullName))
            .ToList();

        Assert.NotEmpty(sources);
        Assert.All(sources, source =>
            Assert.DoesNotContain("MarkupString", source, StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoQueryLeavesTheTextAlone()
    {
        var markup = await RenderAsync("Data Grid columns", _ => { });

        Assert.DoesNotContain("<mark", markup, StringComparison.Ordinal);
        Assert.Contains("Data Grid columns", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEmptyTermIsIgnoredRatherThanMatchingEverything()
    {
        var markup = await RenderAsync("Data Grid columns", parameters =>
            parameters[nameof(BbHighlighter.Query)] = string.Empty);

        Assert.DoesNotContain("<mark", markup, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;

        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static async Task<string> RenderAsync(string text, Action<Dictionary<string, object?>> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddBlazorBlueprintComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        var parameters = new Dictionary<string, object?> { [nameof(BbHighlighter.Text)] = text };
        configure(parameters);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.MountAsync<BbHighlighter>(parameters));

        return renderer.Markup();
    }
}
