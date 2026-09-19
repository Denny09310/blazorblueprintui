namespace BlazorBlueprint.Components;

/// <summary>
/// A sankey chart component using the declarative composition API with Apache ECharts.
/// </summary>
/// <remarks>
/// <para>
/// A sankey diagram shows how a quantity splits and recombines as it moves between stages —
/// traffic through a site, budget through departments, energy through a process. Nodes are the
/// stages, and the ribbon between two nodes is drawn in proportion to the value flowing along it.
/// </para>
/// <para>
/// Bind a collection of links rather than a collection of nodes: <see cref="BbSankey"/> reads a
/// source name, a target name and a value from each row, and derives the node list from them.
/// </para>
/// <para>
/// Usage:
/// <code>
/// &lt;BbSankeyChart Data="@links" Height="400px"&gt;
///     &lt;BbChartTooltip /&gt;
///     &lt;BbSankey SourceKey="from" TargetKey="to" DataKey="value" /&gt;
/// &lt;/BbSankeyChart&gt;
/// </code>
/// </para>
/// </remarks>
public class BbSankeyChart : BbChartBase
{
    internal override string DataSlot => "sankey-chart";

    internal override string SeriesType => "sankey";

    internal override void ApplyChartDefaults(EChartsOption option)
    {
        // A sankey places itself from its own left/top/right/bottom and has no axes to hang off.
        option.XAxis = null;
        option.YAxis = null;
        option.Grid = null;
    }
}
