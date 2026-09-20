namespace BlazorBlueprint.Primitives.SwipeArea;

/// <summary>
/// What the browser is allowed to do with a touch that starts inside a <c>BbSwipeArea</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each value maps to the CSS <c>touch-action</c> property of the same name. This is how a swipe
/// stops fighting the page: the browser is told before the gesture begins which directions it may
/// scroll, rather than being asked to undo a scroll it has already started.
/// </para>
/// <para>
/// Calling <c>preventDefault</c> on a pointer event cannot do this reliably. Once the browser has
/// handed a touch to the compositor to scroll, the event that would cancel it arrives too late,
/// and on a passive listener it is ignored outright.
/// </para>
/// </remarks>
public enum SwipeTouchAction
{
    /// <summary>
    /// The browser keeps its normal behaviour, so the page scrolls as usual. A horizontal swipe
    /// still works with a mouse, and on touch it works until the browser claims the gesture for
    /// a scroll. The default.
    /// </summary>
    Auto,

    /// <summary>
    /// The browser keeps horizontal panning; the area takes vertical gestures. Use for a swipe
    /// up or down inside a page that scrolls sideways.
    /// </summary>
    PanX,

    /// <summary>
    /// The browser keeps vertical panning; the area takes horizontal gestures. This is the usual
    /// choice for a left/right swipe in a page that scrolls up and down.
    /// </summary>
    PanY,

    /// <summary>
    /// The browser scrolls and zooms in no direction inside the area. Use only when the area
    /// genuinely owns both axes, because it also removes the page's own scrolling from it.
    /// </summary>
    None
}
