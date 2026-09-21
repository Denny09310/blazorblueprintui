namespace BlazorBlueprint.Components;

/// <summary>
/// A world choropleth chart that colors countries by their data values.
/// </summary>
/// <remarks>
/// Add a <see cref="BbMap"/> to bind countries and values. A color scale is derived from
/// the data automatically; add <see cref="BbVisualMap"/> to customize its range and colors.
/// Country boundaries are bundled with the library and loaded only when a map is used.
/// </remarks>
public class BbMapChart : BbChartBase
{
    internal override string DataSlot => "map-chart";

    internal override string SeriesType => "map";

    internal override void ApplyChartDefaults(EChartsOption option)
    {
        // Maps position themselves without Cartesian axes or a grid.
    }
}
