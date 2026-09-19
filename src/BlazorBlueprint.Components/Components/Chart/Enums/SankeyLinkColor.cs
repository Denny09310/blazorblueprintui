namespace BlazorBlueprint.Components;

/// <summary>
/// Specifies how a sankey diagram colours the ribbons between its nodes.
/// </summary>
public enum SankeyLinkColor
{
    /// <summary>
    /// The ribbon fades from the source node's colour to the target node's colour.
    /// </summary>
    Gradient,

    /// <summary>
    /// The ribbon takes the colour of the node it leaves.
    /// </summary>
    Source,

    /// <summary>
    /// The ribbon takes the colour of the node it enters.
    /// </summary>
    Target
}
