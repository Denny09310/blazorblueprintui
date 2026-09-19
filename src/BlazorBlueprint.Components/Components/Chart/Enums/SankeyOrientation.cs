namespace BlazorBlueprint.Components;

/// <summary>
/// Specifies the direction a sankey diagram flows in.
/// </summary>
public enum SankeyOrientation
{
    /// <summary>
    /// Nodes are arranged in columns and the flow runs across the chart.
    /// </summary>
    Horizontal,

    /// <summary>
    /// Nodes are arranged in rows and the flow runs down the chart.
    /// </summary>
    Vertical
}
