using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// Base class for the series types that can be stacked on one another: bar, line, area, scatter
/// and radial bar.
/// </summary>
/// <remarks>
/// Stacking used to live on <see cref="SeriesBase"/>, which every series inherits — so a gauge, a
/// radar or a heatmap advertised <c>Stacked</c> and <c>StackGroup</c> even though stacking has no
/// meaning for them and nothing read the values. Declaring them here instead means the parameters
/// appear only where they do something.
/// </remarks>
public abstract class StackableSeriesBase : SeriesBase
{
    /// <summary>
    /// Gets or sets whether this series is stacked with other series.
    /// </summary>
    /// <remarks>
    /// When true, series values are stacked on top of each other.
    /// Use <see cref="StackGroup"/> to control which series stack together.
    /// </remarks>
    [Parameter]
    public bool Stacked { get; set; }

    /// <summary>
    /// Gets or sets the stack group identifier.
    /// </summary>
    /// <remarks>
    /// Series with the same StackGroup value are stacked together.
    /// Default is "stack". Only applies when <see cref="Stacked"/> is true.
    /// </remarks>
    [Parameter]
    public string StackGroup { get; set; } = "stack";
}
