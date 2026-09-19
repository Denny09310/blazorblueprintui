using System.Text.RegularExpressions;

namespace BlazorBlueprint.Tests.Conventions;

/// <summary>
/// Splatting <c>@attributes</c> onto a component that declares no <c>CaptureUnmatchedValues</c>
/// parameter throws <see cref="InvalidOperationException"/> — but only once the dictionary holds
/// an entry. A wrapper written that way renders perfectly until a consumer adds <c>data-testid</c>
/// or <c>title</c>, and then it crashes. The compiler cannot see it and no demo exercises it, so
/// it is checked here as text.
/// </summary>
public partial class AttributeSplatTargetTests
{
    [Fact]
    public void EverySplatTargetCapturesUnmatchedValues()
    {
        var components = Components();
        var offenders = new List<string>();

        foreach (var file in SourceTree.ComponentSources.Where(f => f.Extension == ".razor"))
        {
            var text = File.ReadAllText(file.FullName);

            foreach (Match match in ComponentWithSplat().Matches(text))
            {
                var tag = match.Groups["tag"].Value;

                if (!Resolve(components, file.FullName, tag, out var target) || Captures(components, target))
                {
                    continue;
                }

                var line = text.Take(match.Index).Count(c => c == '\n') + 1;
                offenders.Add($"{SourceTree.RelativePath(file)}:{line} splats onto {tag}");
            }
        }

        Assert.True(offenders.Count == 0,
            "These components forward their captured attributes to a component that cannot accept "
            + "them, so any extra HTML attribute throws at render time. Either give the target a "
            + "CaptureUnmatchedValues parameter, or splat onto an element the component actually "
            + $"renders:{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    /// <summary>Component name to its full source text, including any code-behind.</summary>
    private static Dictionary<string, List<(string Path, string Text)>> Components()
    {
        var map = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);

        foreach (var file in SourceTree.ComponentSources.Where(f => f.Extension == ".razor"))
        {
            var text = File.ReadAllText(file.FullName);
            var behind = new FileInfo(file.FullName + ".cs");

            if (behind.Exists)
            {
                text += "\n" + File.ReadAllText(behind.FullName);
            }

            var name = Path.GetFileNameWithoutExtension(file.Name);

            if (!map.TryGetValue(name, out var list))
            {
                map[name] = list = [];
            }

            list.Add((file.FullName, text));
        }

        return map;
    }

    /// <summary>
    /// Resolves a tag to one source file. A qualified tag names its namespace, which maps onto the
    /// folder; a bare tag is resolved within the same library first, matching how Razor resolves it
    /// through <c>_Imports.razor</c>.
    /// </summary>
    private static bool Resolve(
        Dictionary<string, List<(string Path, string Text)>> components,
        string fromPath,
        string tag,
        out string target)
    {
        target = string.Empty;
        var name = tag.Split('.')[^1];

        if (!components.TryGetValue(name, out var candidates))
        {
            // Not one of ours — a framework component such as NavLink.
            return false;
        }

        if (tag.Contains('.', StringComparison.Ordinal))
        {
            var folder = tag.Split('.')[^2];
            var match = candidates.Find(c => c.Path.Contains(Path.DirectorySeparatorChar + folder + Path.DirectorySeparatorChar, StringComparison.Ordinal));

            if (match.Path is null)
            {
                return false;
            }

            target = match.Path;
            return true;
        }

        var library = fromPath.Contains("BlazorBlueprint.Primitives", StringComparison.Ordinal)
            ? "BlazorBlueprint.Primitives"
            : "BlazorBlueprint.Components";
        var sameLibrary = candidates.FindAll(c => c.Path.Contains(library, StringComparison.Ordinal));
        var pick = sameLibrary.Count > 0 ? sameLibrary : candidates;

        if (pick.Count != 1)
        {
            return false;
        }

        target = pick[0].Path;
        return true;
    }

    /// <summary>
    /// Whether the component captures unmatched values, directly or through the base class it
    /// declares with <c>@inherits</c>.
    /// </summary>
    private static bool Captures(Dictionary<string, List<(string Path, string Text)>> components, string path)
    {
        var text = components.Values.SelectMany(v => v).First(c => c.Path == path).Text;

        if (text.Contains("CaptureUnmatchedValues", StringComparison.Ordinal))
        {
            return true;
        }

        var inherits = Inherits().Match(text);

        if (!inherits.Success)
        {
            return false;
        }

        var baseName = inherits.Groups["base"].Value.Split('.')[^1].Split('<')[0];
        var baseFile = BaseSource(baseName);

        return baseFile is not null && baseFile.Contains("CaptureUnmatchedValues", StringComparison.Ordinal);
    }

    private static string? BaseSource(string name)
    {
        var file = SourceTree.ComponentSources.FirstOrDefault(f => f.Name == name + ".cs" || f.Name == name + ".razor")
            ?? new DirectoryInfo(Path.Combine(SourceTree.RepoRoot.FullName, "src"))
                .EnumerateFiles(name + ".cs", SearchOption.AllDirectories)
                .FirstOrDefault();

        return file is null ? null : File.ReadAllText(file.FullName);
    }

    [GeneratedRegex(@"<(?<tag>[A-Z][\w.]*)(?<attrs>[^<>]*?@attributes[^<>]*?)/?>", RegexOptions.Singleline)]
    private static partial Regex ComponentWithSplat();

    [GeneratedRegex(@"^@inherits\s+(?<base>\S+)", RegexOptions.Multiline)]
    private static partial Regex Inherits();
}
