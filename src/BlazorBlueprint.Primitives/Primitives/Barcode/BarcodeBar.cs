namespace BlazorBlueprint.Primitives.Barcode;

/// <summary>
/// One dark bar of a barcode, positioned and sized in modules.
/// </summary>
/// <param name="Start">The bar's left edge, in modules from the start of the symbol.</param>
/// <param name="Width">The bar's width in modules.</param>
/// <param name="Top">
/// Where the bar starts vertically, as a fraction of the bar area: 0 is the top, 0.5 is halfway
/// down.
/// </param>
/// <param name="Bottom">
/// Where the bar ends vertically, as a fraction of the bar area: 1 is the bottom.
/// </param>
/// <remarks>
/// Most symbologies carry their data in the widths and leave every bar full height, so
/// <paramref name="Top"/> is 0 and <paramref name="Bottom"/> is 1. The postal codes carry their
/// data in the heights instead — see <see cref="BarcodeType.Postnet"/> and
/// <see cref="BarcodeType.Rm4scc"/> — and the retail codes use a slight descender on their guard
/// bars so the digits sit between them.
/// </remarks>
public readonly record struct BarcodeBar(int Start, int Width, double Top, double Bottom)
{
    /// <summary>
    /// Creates a full-height bar.
    /// </summary>
    /// <param name="start">The bar's left edge, in modules.</param>
    /// <param name="width">The bar's width in modules.</param>
    public BarcodeBar(int start, int width)
        : this(start, width, 0, 1)
    {
    }
}
