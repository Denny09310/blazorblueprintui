namespace BlazorBlueprint.Primitives.Barcode;

/// <summary>
/// Lays bars and spaces out left to right, keeping track of where the next element starts.
/// </summary>
/// <remarks>
/// Every symbology here is a sequence of dark and light runs, so the encoders differ only in what
/// widths they ask for and in what order. Collecting that in one place keeps each encoder to its
/// own table and its own checksum.
/// </remarks>
internal sealed class BarcodeBuilder
{
    private readonly List<BarcodeBar> bars = [];

    /// <summary>
    /// Gets where the next element will start, in modules, which once finished is the symbol width.
    /// </summary>
    internal int Position { get; private set; }

    /// <summary>
    /// Adds a dark bar and advances past it.
    /// </summary>
    /// <param name="width">The width in modules.</param>
    /// <param name="top">The top edge as a fraction of the bar area.</param>
    /// <param name="bottom">The bottom edge as a fraction of the bar area.</param>
    internal void Bar(int width, double top = 0, double bottom = 1)
    {
        if (width > 0)
        {
            bars.Add(new BarcodeBar(Position, width, top, bottom));
            Position += width;
        }
    }

    /// <summary>
    /// Advances past a light gap.
    /// </summary>
    /// <param name="width">The width in modules.</param>
    internal void Space(int width) => Position += width;

    /// <summary>
    /// Lays out a run of elements given as widths, starting with a bar and alternating.
    /// </summary>
    /// <param name="widths">One digit per element, for example <c>"212222"</c>.</param>
    /// <param name="bottom">The bottom edge of each bar, as a fraction of the bar area.</param>
    internal void Widths(ReadOnlySpan<char> widths, double bottom = 1)
    {
        for (var i = 0; i < widths.Length; i++)
        {
            var width = widths[i] - '0';
            if (i % 2 == 0)
            {
                Bar(width, 0, bottom);
            }
            else
            {
                Space(width);
            }
        }
    }

    /// <summary>
    /// Lays out a run of elements given as narrow or wide flags, starting with a bar and alternating.
    /// </summary>
    /// <param name="flags">One character per element: <c>'1'</c> for wide, anything else for narrow.</param>
    /// <param name="narrow">The narrow width in modules.</param>
    /// <param name="wide">The wide width in modules.</param>
    internal void NarrowWide(ReadOnlySpan<char> flags, int narrow, int wide)
    {
        for (var i = 0; i < flags.Length; i++)
        {
            var width = flags[i] == '1' ? wide : narrow;
            if (i % 2 == 0)
            {
                Bar(width);
            }
            else
            {
                Space(width);
            }
        }
    }

    /// <summary>
    /// Lays out one module per character, dark where the character is <c>'1'</c>.
    /// </summary>
    /// <param name="modules">The module pattern, for example <c>"0001101"</c>.</param>
    /// <param name="bottom">The bottom edge of each bar, as a fraction of the bar area.</param>
    /// <remarks>
    /// Runs of dark modules are merged into one bar, which is both what a renderer wants and what a
    /// reader sees.
    /// </remarks>
    internal void Modules(ReadOnlySpan<char> modules, double bottom = 1)
    {
        var i = 0;
        while (i < modules.Length)
        {
            if (modules[i] != '1')
            {
                Space(1);
                i++;
                continue;
            }

            var run = 0;
            while (i + run < modules.Length && modules[i + run] == '1')
            {
                run++;
            }

            Bar(run, 0, bottom);
            i += run;
        }
    }

    /// <summary>
    /// Returns the bars laid out so far.
    /// </summary>
    /// <returns>The bars, left to right.</returns>
    internal IReadOnlyList<BarcodeBar> ToBars() => bars;
}
