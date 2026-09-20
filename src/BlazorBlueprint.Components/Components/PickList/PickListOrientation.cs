namespace BlazorBlueprint.Components;

/// <summary>
/// How the two panes of a <c>BbPickList</c> are arranged.
/// </summary>
public enum PickListOrientation
{
    /// <summary>
    /// Side by side, with the move buttons between them. The arrows point left and right.
    /// </summary>
    Horizontal,

    /// <summary>
    /// Stacked, with the move buttons between them. The arrows point up and down. Better on a
    /// narrow screen, where two panes side by side leave neither wide enough to read.
    /// </summary>
    Vertical
}
