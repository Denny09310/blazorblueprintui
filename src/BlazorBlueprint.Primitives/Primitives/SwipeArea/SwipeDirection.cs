namespace BlazorBlueprint.Primitives.SwipeArea;

/// <summary>
/// The direction a finger or pointer travelled during a swipe.
/// </summary>
/// <remarks>
/// These are physical screen directions and do not flip in a right-to-left layout. A component
/// that means "forward" rather than "right" should map the direction itself, so that the mapping
/// lives next to the thing being navigated.
/// </remarks>
public enum SwipeDirection
{
    /// <summary>
    /// No direction. Reported while a gesture has not yet moved.
    /// </summary>
    None,

    /// <summary>
    /// The pointer moved towards the left of the screen.
    /// </summary>
    Left,

    /// <summary>
    /// The pointer moved towards the right of the screen.
    /// </summary>
    Right,

    /// <summary>
    /// The pointer moved towards the top of the screen.
    /// </summary>
    Up,

    /// <summary>
    /// The pointer moved towards the bottom of the screen.
    /// </summary>
    Down
}
