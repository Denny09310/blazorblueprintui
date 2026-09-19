using BlazorBlueprint.Components;

namespace BlazorBlueprint.Tests.Charts;

/// <summary>
/// A sankey is a directed acyclic graph. ECharts throws out of its layout when the links form a
/// cycle, and a thrown layout blanks the whole chart rather than dropping the one bad ribbon — so
/// the cycle has to be caught in C#, before the option ever reaches JavaScript.
/// </summary>
/// <remarks>
/// These checks are on the builder rather than on the rendered markup because the graph never
/// appears in the DOM: it is serialized into the ECharts option and handed straight to the canvas.
/// </remarks>
public class SankeyGraphBuilderTests
{
    [Fact]
    public void AcyclicLinksAllSurvive()
    {
        var graph = Build(
            ("Search", "Landing", 10),
            ("Landing", "Pricing", 6),
            ("Pricing", "Signed up", 4));

        Assert.Equal(3, graph.Links.Count);
        Assert.Equal(["Search", "Landing", "Pricing", "Signed up"], graph.Nodes);
    }

    /// <summary>Node order is first-seen order, because that is what fixes each node's colour.</summary>
    [Fact]
    public void NodesAppearInTheOrderTheyAreFirstSeen()
    {
        var graph = Build(
            ("B", "C", 1),
            ("A", "B", 1));

        Assert.Equal(["B", "C", "A"], graph.Nodes);
    }

    [Fact]
    public void ALinkThatClosesACycleIsDropped()
    {
        var graph = Build(
            ("A", "B", 1),
            ("B", "C", 1),
            ("C", "A", 1));

        Assert.Equal(2, graph.Links.Count);
        Assert.DoesNotContain(graph.Links, link => link.Source == "C");
    }

    /// <summary>A node linked to itself is a cycle of length one, and ECharts throws on it too.</summary>
    [Fact]
    public void ASelfLinkIsDropped()
    {
        var graph = Build(
            ("A", "A", 5),
            ("A", "B", 3));

        Assert.Single(graph.Links);
        Assert.Equal("B", graph.Links[0].Target);
    }

    /// <summary>
    /// Dropping the closing link must not drop the rows after it: everything before and after the
    /// cycle still belongs in the diagram.
    /// </summary>
    [Fact]
    public void RowsAfterADroppedLinkStillSurvive()
    {
        var graph = Build(
            ("A", "B", 1),
            ("B", "A", 1),
            ("B", "C", 2));

        Assert.Equal(2, graph.Links.Count);
        Assert.Equal("C", graph.Links[1].Target);
    }

    /// <summary>Two paths reaching the same node is a diamond, not a cycle.</summary>
    [Fact]
    public void ADiamondIsNotACycle()
    {
        var graph = Build(
            ("A", "B", 1),
            ("A", "C", 1),
            ("B", "D", 1),
            ("C", "D", 1));

        Assert.Equal(4, graph.Links.Count);
    }

    [Fact]
    public void ARowMissingEitherNameIsDropped()
    {
        var graph = Build(
            ("", "B", 1),
            ("A", "", 1),
            ("A", "B", 1));

        Assert.Single(graph.Links);
    }

    /// <summary>
    /// A value that is not a number is dropped rather than coerced to zero: a zero-width ribbon
    /// reads as a real flow that happens to be tiny, not as data that could not be read.
    /// </summary>
    [Fact]
    public void ANonNumericValueIsDropped()
    {
        var graph = SankeyGraphBuilder.Build(
            ["A", "A"],
            ["B", "C"],
            [null, "not a number"]);

        Assert.Empty(graph.Links);
        Assert.Empty(graph.Nodes);
    }

    /// <summary>Values arrive from reflection boxed as whatever the bound property declared.</summary>
    [Fact]
    public void NumericValuesOfAnyClrTypeAreRead()
    {
        var graph = SankeyGraphBuilder.Build(
            ["A", "B", "C"],
            ["B", "C", "D"],
            [1, 2.5m, 3L]);

        Assert.Equal([1d, 2.5d, 3d], graph.Links.Select(link => link.Value));
    }

    /// <summary>
    /// The end columns are worked out from these sets, which is what turns an edge label around
    /// before it runs off the canvas.
    /// </summary>
    [Fact]
    public void TerminalAndSourceNodesAreReported()
    {
        var graph = Build(
            ("A", "B", 1),
            ("B", "C", 1));

        Assert.Equal(["A", "B"], graph.WithOutgoing.Order());
        Assert.Equal(["B", "C"], graph.WithIncoming.Order());
    }

    /// <summary>Uneven columns stop at the shortest, rather than reading past the end of one.</summary>
    [Fact]
    public void UnevenColumnsStopAtTheShortest()
    {
        var graph = SankeyGraphBuilder.Build(
            ["A", "B", "C"],
            ["B", "C"],
            [1, 2, 3]);

        Assert.Equal(2, graph.Links.Count);
    }

    private static SankeyGraph Build(params (string Source, string Target, object? Value)[] rows) =>
        SankeyGraphBuilder.Build(
            [.. rows.Select(row => row.Source)],
            [.. rows.Select(row => row.Target)],
            [.. rows.Select(row => row.Value)]);
}
