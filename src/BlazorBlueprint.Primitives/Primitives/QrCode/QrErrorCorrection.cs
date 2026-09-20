namespace BlazorBlueprint.Primitives.QrCode;

/// <summary>
/// How much of a QR code can be lost or covered and still be read.
/// </summary>
/// <remarks>
/// A higher level survives more damage but needs more modules for the same data, so the code grows
/// or the data has to shrink. <see cref="Medium"/> is the usual choice. Go higher when the code
/// will be printed small, printed on something that creases, or covered by a centre image.
/// </remarks>
public enum QrErrorCorrection
{
    /// <summary>Recovers about 7% of the codewords. The smallest code for a given string.</summary>
    Low = 0,

    /// <summary>Recovers about 15% of the codewords. The default.</summary>
    Medium = 1,

    /// <summary>Recovers about 25% of the codewords.</summary>
    Quartile = 2,

    /// <summary>Recovers about 30% of the codewords. The most damage-tolerant.</summary>
    High = 3,
}
