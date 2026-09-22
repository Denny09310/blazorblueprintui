namespace BlazorBlueprint.Primitives.Barcode;

/// <summary>
/// A finished barcode as a list of bars, positioned and sized in modules.
/// </summary>
/// <remarks>
/// This is the headless result: geometry and nothing else. No colour, no size, no quiet zone drawn.
/// Render it however suits — as SVG, to a canvas, to a bitmap, or straight to a printer. The styled
/// <c>BbBarcode</c> component renders it as SVG.
/// </remarks>
public sealed class BarcodeSymbol
{
    internal BarcodeSymbol(
        BarcodeType type,
        string value,
        int moduleCount,
        int quietZoneModules,
        IReadOnlyList<BarcodeBar> bars,
        IReadOnlyList<BarcodeTextGroup> textGroups)
    {
        Type = type;
        Value = value;
        ModuleCount = moduleCount;
        QuietZoneModules = quietZoneModules;
        Bars = bars;
        TextGroups = textGroups;
    }

    /// <summary>
    /// Gets the symbology the value was encoded in.
    /// </summary>
    public BarcodeType Type { get; }

    /// <summary>
    /// Gets the value as encoded, including any check digit the encoder worked out.
    /// </summary>
    /// <remarks>
    /// This is not always what was passed in. Give <c>5901234123</c> to <see cref="BarcodeType.Ean13"/>
    /// and the encoder pads and appends a check digit, so this reads <c>5901234123457</c>. Store this
    /// rather than the input where the number has to match what a scanner will report.
    /// </remarks>
    public string Value { get; }

    /// <summary>
    /// Gets the width of the symbol in modules, excluding the quiet zone.
    /// </summary>
    public int ModuleCount { get; }

    /// <summary>
    /// Gets the light margin each side that the symbology asks for, in modules.
    /// </summary>
    /// <remarks>
    /// A reader needs this margin to find the start and the end of the symbol. It differs by
    /// symbology — EAN-13 wants 11 modules on the left and Code 128 wants 10 — and this is the
    /// larger of the two sides, so using it on both is always safe.
    /// </remarks>
    public int QuietZoneModules { get; }

    /// <summary>
    /// Gets the dark bars, left to right.
    /// </summary>
    public IReadOnlyList<BarcodeBar> Bars { get; }

    /// <summary>
    /// Gets the human-readable text and where under the symbol each run belongs.
    /// </summary>
    /// <remarks>
    /// Empty where the symbology has no standard text line. Otherwise usually one run spanning the
    /// whole symbol, except on the retail codes, which place their digits around the guard bars.
    /// </remarks>
    public IReadOnlyList<BarcodeTextGroup> TextGroups { get; }

    /// <summary>
    /// Gets whether the data is carried in the bar heights rather than the bar widths.
    /// </summary>
    /// <remarks>
    /// True for <see cref="BarcodeType.Postnet"/> and <see cref="BarcodeType.Rm4scc"/>. A renderer
    /// that squashes the height of one of these destroys the data, where on every other symbology
    /// height is only a matter of how easy the symbol is to aim at.
    /// </remarks>
    public bool IsHeightModulated => Type is BarcodeType.Postnet or BarcodeType.Rm4scc;
}
