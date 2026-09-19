using System.Globalization;

namespace BlazorBlueprint.Components;

/// <summary>One accepted link, with its value already converted to a number.</summary>
internal readonly record struct SankeyLink(string Source, string Target, double Value);

/// <summary>The acyclic node-and-link graph a sankey series is drawn from.</summary>
internal sealed class SankeyGraph
{
    /// <summary>Node names in the order they were first seen, which is also their palette order.</summary>
    internal List<string> Nodes { get; } = [];

    /// <summary>The links that survived, in data order.</summary>
    internal List<SankeyLink> Links { get; } = [];

    /// <summary>Nodes with at least one link leaving them.</summary>
    internal HashSet<string> WithOutgoing { get; } = new(StringComparer.Ordinal);

    /// <summary>Nodes with at least one link entering them.</summary>
    internal HashSet<string> WithIncoming { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Turns three parallel columns — a source name, a target name and a value per row — into the
/// directed acyclic graph an ECharts sankey series needs.
/// </summary>
/// <remarks>
/// <para>
/// <strong>A sankey is a DAG.</strong> ECharts throws rather than drawing when the links form a
/// cycle, and a thrown layout leaves the whole chart blank rather than showing the parts that were
/// fine. A link that would close a cycle — including a link from a node to itself — is therefore
/// left out, and the rest of the diagram is drawn.
/// </para>
/// <para>
/// A row missing either name is dropped, because a link needs both ends. A row whose value is not a
/// number is dropped rather than coerced to zero, because a zero-width ribbon reads as a real flow
/// that happens to be tiny rather than as data that could not be read.
/// </para>
/// </remarks>
internal static class SankeyGraphBuilder
{
    internal static SankeyGraph Build(
        IReadOnlyList<string> sources,
        IReadOnlyList<string> targets,
        IReadOnlyList<object?> values)
    {
        var graph = new SankeyGraph();
        var seenNodes = new HashSet<string>(StringComparer.Ordinal);
        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        var rowCount = Math.Min(sources.Count, Math.Min(targets.Count, values.Count));

        for (var i = 0; i < rowCount; i++)
        {
            var source = sources[i];
            var target = targets[i];

            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            {
                continue;
            }

            if (!TryConvertValue(values[i], out var value))
            {
                continue;
            }

            // A self-link is a one-node cycle, and any link whose target already reaches its source
            // closes a longer one.
            if (string.Equals(source, target, StringComparison.Ordinal) || CanReach(edges, target, source))
            {
                continue;
            }

            if (seenNodes.Add(source))
            {
                graph.Nodes.Add(source);
            }

            if (seenNodes.Add(target))
            {
                graph.Nodes.Add(target);
            }

            if (!edges.TryGetValue(source, out var outgoing))
            {
                outgoing = [];
                edges[source] = outgoing;
            }

            outgoing.Add(target);
            graph.WithOutgoing.Add(source);
            graph.WithIncoming.Add(target);
            graph.Links.Add(new SankeyLink(source, target, value));
        }

        return graph;
    }

    /// <summary>
    /// Walks the links accepted so far to decide whether <paramref name="to"/> is already reachable
    /// from <paramref name="from"/>.
    /// </summary>
    private static bool CanReach(Dictionary<string, List<string>> edges, string from, string to)
    {
        if (edges.Count == 0)
        {
            return false;
        }

        var visited = new HashSet<string>(StringComparer.Ordinal) { from };
        var pending = new Stack<string>();
        pending.Push(from);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            if (string.Equals(current, to, StringComparison.Ordinal))
            {
                return true;
            }

            if (!edges.TryGetValue(current, out var outgoing))
            {
                continue;
            }

            foreach (var next in outgoing)
            {
                if (visited.Add(next))
                {
                    pending.Push(next);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Converts an extracted value to a number, rejecting anything that is not one.
    /// </summary>
    private static bool TryConvertValue(object? raw, out double value)
    {
        value = 0;

        switch (raw)
        {
            case null:
                return false;
            case double d:
                value = d;
                break;
            case IConvertible convertible:
                try
                {
                    value = convertible.ToDouble(CultureInfo.InvariantCulture);
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
                {
                    return false;
                }

                break;
            default:
                return false;
        }

        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
