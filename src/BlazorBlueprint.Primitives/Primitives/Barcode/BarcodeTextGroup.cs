namespace BlazorBlueprint.Primitives.Barcode;

/// <summary>
/// A run of human-readable text and the span of the symbol it belongs under.
/// </summary>
/// <param name="Text">The characters to print.</param>
/// <param name="Start">The left edge of the span, in modules from the start of the symbol.</param>
/// <param name="End">The right edge of the span, in modules.</param>
/// <remarks>
/// Most symbologies print their value as one run centred under the whole symbol. The retail codes
/// do not: EAN-13 puts its first digit in the left quiet zone and splits the rest either side of
/// the centre guard, and where that text lands is part of the standard rather than decoration.
/// </remarks>
public readonly record struct BarcodeTextGroup(string Text, double Start, double End);
