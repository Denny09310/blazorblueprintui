using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// A sankey series component for composable charts.
/// </summary>
/// <remarks>
/// <para>
/// Bind the parent chart to a collection of <em>links</em>, not of nodes: each row supplies a source
/// name through <see cref="SourceKey"/>, a target name through <see cref="TargetKey"/> and a value
/// through <see cref="SeriesBase.DataKey"/>. The node list is derived from the links in the order the
/// names are first seen, which is also the order they take their colours from the chart palette.
/// </para>
/// <para>
/// <strong>A sankey is a directed acyclic graph.</strong> ECharts throws rather than drawing when the
/// links form a cycle, which leaves the whole chart blank. A link that would close a cycle — including
/// a link from a node to itself — is therefore dropped, and the rest of the diagram is drawn. A row
/// missing either name, or carrying a value that is not a number, is dropped for the same reason.
/// </para>
/// <para>
/// A node in the outermost column points its label at the edge of the canvas, where ECharts draws it
/// and lets it overflow. Those nodes are given the opposite <see cref="LabelPosition"/> so the text
/// turns inwards and stays readable however long the name is.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BbSankeyChart Data="@links"&gt;
///     &lt;BbChartTooltip /&gt;
///     &lt;BbSankey SourceKey="from" TargetKey="to" DataKey="value" /&gt;
/// &lt;/BbSankeyChart&gt;
/// </code>
/// </example>
public partial class BbSankey : SeriesBase
{
    /// <summary>
    /// Gets or sets the property name holding the name of the node each link leaves.
    /// </summary>
    [Parameter]
    public string? SourceKey { get; set; }

    /// <summary>
    /// Gets or sets the property name holding the name of the node each link enters.
    /// </summary>
    [Parameter]
    public string? TargetKey { get; set; }

    /// <summary>
    /// Gets or sets the direction the diagram flows in.
    /// </summary>
    [Parameter]
    public SankeyOrientation Orientation { get; set; } = SankeyOrientation.Horizontal;

    /// <summary>
    /// Gets or sets where the nodes with no outgoing link are placed.
    /// </summary>
    [Parameter]
    public SankeyNodeAlign NodeAlign { get; set; } = SankeyNodeAlign.Justify;

    /// <summary>
    /// Gets or sets the thickness of a node in pixels.
    /// </summary>
    [Parameter]
    public int NodeWidth { get; set; } = 20;

    /// <summary>
    /// Gets or sets the gap between two nodes in the same column, in pixels.
    /// </summary>
    [Parameter]
    public int NodeGap { get; set; } = 8;

    /// <summary>
    /// Gets or sets whether a visitor can drag a node to a new position.
    /// </summary>
    /// <remarks>
    /// Off by default, unlike ECharts itself: a dragged node stays where it was dropped with no way
    /// back short of a reload, so a stray drag deforms the diagram permanently.
    /// </remarks>
    [Parameter]
    public bool Draggable { get; set; }

    /// <summary>
    /// Gets or sets how the ribbon between two nodes is coloured.
    /// </summary>
    [Parameter]
    public SankeyLinkColor LinkColor { get; set; } = SankeyLinkColor.Gradient;

    /// <summary>
    /// Gets or sets the opacity of the ribbons, from 0 to 1.
    /// </summary>
    [Parameter]
    public double LinkOpacity { get; set; } = 0.4;

    /// <summary>
    /// Gets or sets how much the ribbons bow between their two nodes, from 0 (straight) to 1.
    /// </summary>
    [Parameter]
    public double Curveness { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets whether hovering a node dims everything it is not connected to.
    /// </summary>
    /// <remarks>
    /// Tracing one path through a busy diagram is the reason to draw a sankey rather than a bar
    /// chart, so this is on by default.
    /// </remarks>
    [Parameter]
    public bool FocusAdjacent { get; set; } = true;

    /// <summary>
    /// Gets or sets whether each node is labelled with its name.
    /// </summary>
    [Parameter]
    public bool ShowLabels { get; set; } = true;

    /// <summary>
    /// Gets or sets the label position relative to its node.
    /// </summary>
    [Parameter]
    public LabelPosition LabelPosition { get; set; } = LabelPosition.Right;

    /// <summary>
    /// Gets or sets the ECharts label formatter string.
    /// </summary>
    /// <remarks>
    /// Use ECharts template variables: <c>{b}</c> for the node name, <c>{c}</c> for its value.
    /// </remarks>
    [Parameter]
    public string? LabelFormatter { get; set; }

    /// <summary>
    /// Gets or sets the label text color.
    /// </summary>
    [Parameter]
    public string? LabelColor { get; set; }

    /// <summary>
    /// Gets or sets the label font size in pixels.
    /// </summary>
    [Parameter]
    public int? LabelFontSize { get; set; }

    /// <inheritdoc />
    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var series = new EChartsSeriesOption
        {
            Type = "sankey",
            Name = GetResolvedName(),
            Left = "12",
            Top = "12",
            Right = "12",
            Bottom = "12",
            Orient = Orientation == SankeyOrientation.Vertical ? "vertical" : "horizontal",
            NodeAlign = NodeAlign switch
            {
                SankeyNodeAlign.Left => "left",
                SankeyNodeAlign.Right => "right",
                _ => "justify"
            },
            NodeWidth = NodeWidth,
            NodeGap = NodeGap,
            Draggable = Draggable,
            Label = new EChartsLabelOption
            {
                Show = ShowLabels,
                Position = ToEChartsPosition(LabelPosition),
                Formatter = LabelFormatter,
                Color = LabelColor ?? "var(--foreground)",
                FontSize = LabelFontSize,
                TextBorderWidth = 0
            },
            ItemStyle = new EChartsItemStyleOption
            {
                // A sankey colours each node from the chart palette; an explicit Color on the series
                // paints all of them the same. Left unset, the inherited parameter would be offered
                // and then ignored.
                Color = GetResolvedColor(),
                BorderWidth = 0
            },
            LineStyle = new EChartsLineStyleOption
            {
                Color = LinkColor switch
                {
                    SankeyLinkColor.Source => "source",
                    SankeyLinkColor.Target => "target",
                    _ => "gradient"
                },
                Opacity = LinkOpacity,
                Curveness = Curveness
            },
            Emphasis = FocusAdjacent
                ? new EChartsEmphasisOption { Focus = "adjacency" }
                : new EChartsEmphasisOption { Disabled = true }
        };

        ApplyGraph(series);

        return series;
    }

    /// <summary>
    /// Reads the bound rows into an acyclic node-and-link graph and writes it onto the series.
    /// </summary>
    private void ApplyGraph(EChartsSeriesOption series)
    {
        if (string.IsNullOrEmpty(SourceKey) || string.IsNullOrEmpty(TargetKey) || string.IsNullOrEmpty(DataKey))
        {
            return;
        }

        var graph = SankeyGraphBuilder.Build(
            DataExtractor.ExtractStringValues(ParentChart?.Data, SourceKey),
            DataExtractor.ExtractStringValues(ParentChart?.Data, TargetKey),
            DataExtractor.ExtractValues(ParentChart?.Data, DataKey));

        series.Data = new List<object?>(graph.Nodes.Select(name => (object?)BuildNode(name, graph)));

        series.Links = new List<object?>(graph.Links.Select(link => (object?)new Dictionary<string, object?>
        {
            ["source"] = link.Source,
            ["target"] = link.Target,
            ["value"] = link.Value
        }));
    }

    /// <summary>
    /// Builds one node, turning its label around when the label would otherwise run off the edge
    /// of the chart.
    /// </summary>
    /// <remarks>
    /// A label sits outside its node, so the nodes in the outermost column point theirs at the edge
    /// of the canvas and the text is clipped — ECharts draws it and lets it overflow. Which column
    /// is outermost depends on which way the label points: a label on the right is at risk on a node
    /// with no outgoing link, a label on the left on a node with no incoming link. Those nodes get
    /// the opposite side, so the text turns inwards and stays readable however long the name is.
    /// </remarks>
    private Dictionary<string, object?> BuildNode(string name, SankeyGraph graph)
    {
        var node = new Dictionary<string, object?> { ["name"] = name };

        var flipped = LabelPosition switch
        {
            LabelPosition.Right => "left",
            LabelPosition.Bottom => "top",
            LabelPosition.Left => "right",
            LabelPosition.Top => "bottom",
            _ => null
        };

        if (flipped == null)
        {
            return node;
        }

        // A label pointing right or down is clipped on a node with nothing after it; one pointing
        // left or up is clipped on a node with nothing before it.
        var atRisk = LabelPosition is LabelPosition.Right or LabelPosition.Bottom
            ? !graph.WithOutgoing.Contains(name)
            : !graph.WithIncoming.Contains(name);

        if (atRisk)
        {
            node["label"] = new Dictionary<string, object?> { ["position"] = flipped };
        }

        return node;
    }
}
