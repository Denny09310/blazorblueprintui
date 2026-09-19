namespace BlazorBlueprint.Components;

/// <summary>
/// A rose (Nightingale) chart component using the declarative composition API with Apache ECharts.
/// </summary>
/// <remarks>
/// <para>
/// A rose chart is a pie whose sectors also vary in radius, so a long sector reads as a large
/// value at a glance. It suits a set of categories whose values differ by enough that equal-radius
/// slices are hard to rank — a pie asks you to compare angles, a rose lets you compare lengths.
/// </para>
/// <para>
/// The series is an ECharts pie underneath, so everything <see cref="BbPie"/> offers —
/// labels, a donut hole, a <see cref="BbCenterLabel"/> — is available on <see cref="BbRose"/> too.
/// </para>
/// <para>
/// Usage:
/// <code>
/// &lt;BbRoseChart Data="@data" Height="350px"&gt;
///     &lt;BbChartTooltip /&gt;
///     &lt;BbRose DataKey="visitors" NameKey="browser" Mode="RoseMode.Radius" /&gt;
/// &lt;/BbRoseChart&gt;
/// </code>
/// </para>
/// </remarks>
public class BbRoseChart : BbChartBase
{
    internal override string DataSlot => "rose-chart";

    /// <summary>
    /// The ECharts series type, which is <c>pie</c> — a rose is a pie drawn with a varying radius,
    /// not a series type of its own. Reporting it honestly is what gives the chart the item-triggered
    /// tooltip and the centred legend layout a pie already gets.
    /// </summary>
    internal override string SeriesType => "pie";

    internal override void ApplyChartDefaults(EChartsOption option)
    {
        // Rose charts don't use XAxis, YAxis, or Grid
        option.XAxis = null;
        option.YAxis = null;
        option.Grid = null;
    }
}
