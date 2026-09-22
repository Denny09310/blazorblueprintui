namespace BlazorBlueprint.Primitives.SwipeArea;

/// <summary>
/// Which axis a <c>BbSwipeArea</c> reports swipes on.
/// </summary>
public enum SwipeAxis
{
    /// <summary>
    /// Report whichever axis the gesture travelled furthest along.
    /// </summary>
    Both,

    /// <summary>
    /// Only report <see cref="SwipeDirection.Left"/> and <see cref="SwipeDirection.Right"/>.
    /// Vertical travel is measured but never decides the direction, so a mostly-vertical drag is
    /// judged on how far it went sideways.
    /// </summary>
    Horizontal,

    /// <summary>
    /// Only report <see cref="SwipeDirection.Up"/> and <see cref="SwipeDirection.Down"/>.
    /// </summary>
    Vertical
}
