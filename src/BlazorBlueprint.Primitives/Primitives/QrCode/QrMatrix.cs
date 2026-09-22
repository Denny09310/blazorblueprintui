namespace BlazorBlueprint.Primitives.QrCode;

/// <summary>
/// A finished QR code as a square grid of dark and light modules.
/// </summary>
/// <remarks>
/// <para>
/// This is the headless result: geometry and nothing else. No colour, no size, no quiet zone.
/// Render it however suits — as SVG, to a canvas, to a bitmap, or as text. The styled
/// <c>BbQrCode</c> component renders it as SVG.
/// </para>
/// <para>
/// The grid does not include the quiet zone, which is the light margin a reader needs to find the
/// code. Four modules on every side is the specified minimum; leave that space when rendering.
/// </para>
/// </remarks>
public sealed class QrMatrix
{
    private readonly bool[] modules;

    internal QrMatrix(int size, bool[] modules, int version, QrErrorCorrection errorCorrection, int mask, QrEncodingMode mode)
    {
        Size = size;
        Version = version;
        ErrorCorrection = errorCorrection;
        Mask = mask;
        Mode = mode;
        this.modules = modules;
    }

    /// <summary>
    /// Gets the width and height of the grid in modules, excluding the quiet zone.
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// Gets the QR version, 1 to 40. Each step up adds four modules to each side.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// Gets the error correction level the code was built with.
    /// </summary>
    public QrErrorCorrection ErrorCorrection { get; }

    /// <summary>
    /// Gets the mask pattern applied to the data region, 0 to 7.
    /// </summary>
    public int Mask { get; }

    /// <summary>
    /// Gets the mode the text was packed with.
    /// </summary>
    public QrEncodingMode Mode { get; }

    /// <summary>
    /// Gets whether the module at the given position is dark.
    /// </summary>
    /// <param name="x">The column, 0 at the left. Out-of-range reads as light.</param>
    /// <param name="y">The row, 0 at the top. Out-of-range reads as light.</param>
    /// <returns><see langword="true"/> when the module is dark.</returns>
    /// <remarks>
    /// Out-of-range positions read as light rather than throwing, so a renderer can walk a grid
    /// that includes the quiet zone without bounds checks of its own.
    /// </remarks>
    public bool this[int x, int y] =>
        x >= 0 && x < Size && y >= 0 && y < Size && modules[(y * Size) + x];
}
