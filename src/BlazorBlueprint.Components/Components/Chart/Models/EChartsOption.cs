using System.Text.Json.Serialization;

namespace BlazorBlueprint.Components;

internal sealed class EChartsOption
{
    [JsonPropertyName("animation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Animation { get; set; } = true;

    [JsonPropertyName("xAxis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsAxisOption? XAxis { get; set; }

    [JsonPropertyName("yAxis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsAxisOption? YAxis { get; set; }

    [JsonPropertyName("grid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsGridOption? Grid { get; set; }

    [JsonPropertyName("tooltip")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsTooltipOption? Tooltip { get; set; }

    [JsonPropertyName("legend")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsLegendOption? Legend { get; set; }

    [JsonPropertyName("series")]
    public List<EChartsSeriesOption> Series { get; set; } = [];

    [JsonPropertyName("color")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object>? Color { get; set; }

    [JsonPropertyName("radar")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsRadarOption? Radar { get; set; }

    /// <summary>
    /// The chart's titles. ECharts accepts an array here, which is what lets a chart keep its own
    /// heading while a series borrows a second title to label something the series type has no
    /// label of its own for — the text in the middle of a radial bar, for instance.
    /// </summary>
    /// <remarks>
    /// Left null rather than empty when nothing has been added, so a chart with no title emits no
    /// <c>title</c> key at all.
    /// </remarks>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<EChartsTitleOption>? Titles { get; set; }

    [JsonPropertyName("polar")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsPolarOption? Polar { get; set; }

    [JsonPropertyName("angleAxis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsAngleAxisOption? AngleAxis { get; set; }

    [JsonPropertyName("radiusAxis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsRadiusAxisOption? RadiusAxis { get; set; }

    [JsonPropertyName("visualMap")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EChartsVisualMapOption? VisualMap { get; set; }

    /// <summary>Adds a title, creating the list on first use.</summary>
    internal void AddTitle(EChartsTitleOption title) =>
        (Titles ??= []).Add(title);
}
