namespace BlazorBlueprint.Components;

/// <summary>
/// Specifies where a sankey diagram places the nodes that have no outgoing link.
/// </summary>
/// <remarks>
/// The values name a physical side rather than a reading-order one, because a sankey's layout is
/// computed by ECharts and does not mirror under <c>dir="rtl"</c>. Pass the other value to get the
/// other side.
/// </remarks>
public enum SankeyNodeAlign
{
    /// <summary>
    /// Terminal nodes are pushed to the far edge so the diagram fills its box.
    /// </summary>
    Justify,

    /// <summary>
    /// Every node sits as far left as its depth allows.
    /// </summary>
    Left,

    /// <summary>
    /// Every node sits as far right as its depth allows.
    /// </summary>
    Right
}
