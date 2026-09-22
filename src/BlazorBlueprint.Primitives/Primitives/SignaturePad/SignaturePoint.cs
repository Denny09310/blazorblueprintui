namespace BlazorBlueprint.Primitives.SignaturePad;

/// <summary>
/// One captured pen position.
/// </summary>
/// <param name="X">Distance from the left edge of the pad, in CSS pixels.</param>
/// <param name="Y">Distance from the top edge of the pad, in CSS pixels.</param>
/// <param name="T">
/// When the sample was taken, in milliseconds since the page loaded. The gaps between timestamps
/// are what give the stroke its taper, so a replayed signature needs them to look like the one
/// that was drawn.
/// </param>
public readonly record struct SignaturePoint(double X, double Y, double T);
