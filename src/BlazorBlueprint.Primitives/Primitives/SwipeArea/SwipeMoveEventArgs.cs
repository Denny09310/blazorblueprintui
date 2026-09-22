using System.Diagnostics.CodeAnalysis;

namespace BlazorBlueprint.Primitives.SwipeArea;

/// <summary>
/// A swipe in progress, reported while the pointer is still down.
/// </summary>
/// <param name="DeltaX">
/// Horizontal travel from where the gesture started, in CSS pixels. Negative is leftwards.
/// </param>
/// <param name="DeltaY">
/// Vertical travel from where the gesture started, in CSS pixels. Negative is upwards.
/// </param>
/// <remarks>
/// These updates are throttled to one every 50ms with a single call in flight, so a slow circuit
/// drops intermediate positions rather than queueing them. Treat each one as the current offset,
/// not as a step to accumulate.
/// </remarks>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Type represents event arguments")]
public readonly record struct SwipeMoveEventArgs(double DeltaX, double DeltaY);
