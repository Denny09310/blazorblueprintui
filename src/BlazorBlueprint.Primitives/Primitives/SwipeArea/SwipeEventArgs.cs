using System.Diagnostics.CodeAnalysis;

namespace BlazorBlueprint.Primitives.SwipeArea;

/// <summary>
/// A completed swipe.
/// </summary>
/// <param name="Direction">
/// The direction the pointer travelled, along whichever axis the area reports.
/// </param>
/// <param name="DeltaX">
/// Horizontal travel from where the gesture started, in CSS pixels. Negative is leftwards.
/// </param>
/// <param name="DeltaY">
/// Vertical travel from where the gesture started, in CSS pixels. Negative is upwards.
/// </param>
/// <param name="Distance">
/// Travel along the reported axis, in CSS pixels. Always positive.
/// </param>
/// <param name="Velocity">
/// Speed at the end of the gesture, in CSS pixels per millisecond, measured over the last 100ms
/// of movement rather than across the whole gesture. A swipe that stalls before the finger lifts
/// therefore reports a low velocity even when it covered a long distance.
/// </param>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Type represents event arguments")]
public readonly record struct SwipeEventArgs(
    SwipeDirection Direction,
    double DeltaX,
    double DeltaY,
    double Distance,
    double Velocity);
