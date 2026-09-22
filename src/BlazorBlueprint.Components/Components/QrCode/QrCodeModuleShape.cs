namespace BlazorBlueprint.Components;

/// <summary>
/// The shape drawn for each dark module of a QR code.
/// </summary>
/// <remarks>
/// The three finder patterns in the corners stay solid squares whichever shape is chosen, because a
/// reader finds the code by those squares before it reads anything else.
/// </remarks>
public enum QrCodeModuleShape
{
    /// <summary>Solid squares that meet their neighbours. The most reliable to scan.</summary>
    Square,

    /// <summary>Squares with rounded corners. Softer, and still joined along a run.</summary>
    Rounded,

    /// <summary>Separate dots. The most decorative and the least reliable to scan.</summary>
    Dot,
}
