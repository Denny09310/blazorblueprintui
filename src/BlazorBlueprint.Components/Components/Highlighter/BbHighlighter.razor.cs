using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// Marks the parts of a string that match a search term, so a result list can show why each row
/// matched.
/// </summary>
/// <remarks>
/// <para>
/// The matched runs render as <c>&lt;mark&gt;</c> elements. That is the element browsers and
/// screen readers already understand as "relevant to a search", so the highlight is not colour
/// alone.
/// </para>
/// <para>
/// The text is rendered as text, never as markup: a term that arrives from a search box cannot
/// inject an element. Matching is ordinal and case-insensitive by default, and overlapping terms
/// are merged into one run rather than nested.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BbHighlighter Text="@row.Name" Query="@search" /&gt;
///
/// &lt;BbHighlighter Text="@row.Name" Queries="@terms" CaseSensitive="true" /&gt;
/// </code>
/// </example>
public partial class BbHighlighter : ComponentBase
{
    /// <summary>
    /// Gets or sets the text to render.
    /// </summary>
    [Parameter, EditorRequired]
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets the term to mark. Ignored when <see cref="Queries"/> is set.
    /// </summary>
    [Parameter]
    public string? Query { get; set; }

    /// <summary>
    /// Gets or sets several terms to mark. Overlapping matches merge into one run.
    /// </summary>
    [Parameter]
    public IReadOnlyList<string>? Queries { get; set; }

    /// <summary>
    /// Gets or sets whether matching is case-sensitive. Defaults to <c>false</c>, which is what a
    /// search box normally wants.
    /// </summary>
    [Parameter]
    public bool CaseSensitive { get; set; }

    /// <summary>
    /// Gets or sets whether a term only matches at the start of a word. Use it to stop a short
    /// term marking a fragment in the middle of every other word.
    /// </summary>
    [Parameter]
    public bool WholeWord { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes for the <c>&lt;mark&gt;</c> elements.
    /// </summary>
    [Parameter]
    public string? MarkClass { get; set; }

    private string MarkCssClass => ClassNames.cn(
        "bb:rounded-sm bb:bg-primary/20 bb:px-0.5 bb:text-inherit",
        MarkClass);

    /// <summary>A run of the text, flagged as matched or not.</summary>
    private readonly record struct Segment(string Text, bool IsMatch);

    private IEnumerable<Segment> Segments
    {
        get
        {
            if (string.IsNullOrEmpty(Text))
            {
                return [];
            }

            var terms = (Queries ?? (Query is null ? [] : [Query]))
                .Where(term => !string.IsNullOrEmpty(term))
                .ToList();

            return terms.Count == 0 ? [new Segment(Text, false)] : Build(Text, terms);
        }
    }

    private List<Segment> Build(string text, List<string> terms)
    {
        var comparison = CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        // Collect every match, then merge, so two terms that overlap produce one run rather than
        // a nested mark.
        var ranges = new List<(int Start, int End)>();

        foreach (var term in terms)
        {
            var index = 0;

            while (index <= text.Length - term.Length)
            {
                var found = text.IndexOf(term, index, comparison);

                if (found < 0)
                {
                    break;
                }

                if (!WholeWord || IsWordStart(text, found))
                {
                    ranges.Add((found, found + term.Length));
                }

                index = found + 1;
            }
        }

        if (ranges.Count == 0)
        {
            return [new Segment(text, false)];
        }

        ranges.Sort((left, right) => left.Start.CompareTo(right.Start));

        var segments = new List<Segment>();
        var cursor = 0;
        var (start, end) = ranges[0];

        foreach (var (nextStart, nextEnd) in ranges.Skip(1))
        {
            if (nextStart <= end)
            {
                end = Math.Max(end, nextEnd);
                continue;
            }

            cursor = Emit(segments, text, cursor, start, end);
            (start, end) = (nextStart, nextEnd);
        }

        cursor = Emit(segments, text, cursor, start, end);

        if (cursor < text.Length)
        {
            segments.Add(new Segment(text[cursor..], false));
        }

        return segments;
    }

    private static int Emit(List<Segment> segments, string text, int cursor, int start, int end)
    {
        if (start > cursor)
        {
            segments.Add(new Segment(text[cursor..start], false));
        }

        segments.Add(new Segment(text[start..end], true));
        return end;
    }

    [SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity",
        Justification = "Character classification, not a string comparison")]
    private static bool IsWordStart(string text, int index) =>
        index == 0 || !char.IsLetterOrDigit(text[index - 1]);
}
