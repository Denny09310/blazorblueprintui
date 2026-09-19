using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// A rose (Nightingale) series component for composable charts.
/// </summary>
/// <remarks>
/// <para>
/// A rose is a pie whose sectors also vary in radius, so it inherits every parameter
/// <see cref="BbPie"/> offers — the donut hole, the labels, the leader lines, the child
/// <see cref="BbCenterLabel"/> — and adds <see cref="Mode"/> on top. Deriving from
/// <see cref="BbPie"/> rather than repeating it keeps the two in step: a fix to the pie's label
/// or centre-label handling reaches the rose as well.
/// </para>
/// <para>
/// <see cref="RoseMode.Radius"/>, the default, keeps the pie's proportional angles and adds radius,
/// so the sector encodes the value twice and small values stay visible. <see cref="RoseMode.Area"/>
/// gives every sector the same angle and varies the radius alone, which is the honest choice when
/// the categories are a fixed set — twelve months, seven days — rather than parts of a whole.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BbRoseChart Data="@data"&gt;
///     &lt;BbChartTooltip /&gt;
///     &lt;BbRose DataKey="visitors" NameKey="browser" Mode="RoseMode.Area" /&gt;
/// &lt;/BbRoseChart&gt;
/// </code>
/// </example>
public class BbRose : BbPie
{
    /// <summary>
    /// Gets or sets how a value is mapped onto a sector.
    /// </summary>
    /// <remarks>
    /// Maps to the ECharts <c>roseType</c> option.
    /// </remarks>
    [Parameter]
    public RoseMode Mode { get; set; } = RoseMode.Radius;

    /// <inheritdoc />
    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var series = base.BuildSeriesCore();

        series.RoseType = Mode == RoseMode.Area ? "area" : "radius";

        return series;
    }
}
