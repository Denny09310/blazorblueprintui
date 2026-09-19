namespace BlazorBlueprint.Components;

/// <summary>
/// Specifies how a rose (Nightingale) chart maps a value onto a sector.
/// </summary>
/// <remarks>
/// Maps to the ECharts <c>roseType</c> option.
/// </remarks>
public enum RoseMode
{
    /// <summary>
    /// The central angle shows each value's share of the total and the radius shows the value,
    /// so a sector encodes the same number twice.
    /// </summary>
    Radius,

    /// <summary>
    /// Every sector takes an equal central angle and only the radius varies, so the sectors
    /// are comparable by length alone.
    /// </summary>
    Area
}
