using System.Text.RegularExpressions;

namespace BlazorBlueprint.Tests.Conventions;

/// <summary>
/// An element that writes its own <c>style</c> and then splats the raw
/// <c>AdditionalAttributes</c> throws that style away whenever the consumer passes one of their own.
/// <para>
/// Razor applies attributes in source order and applies an <c>@attributes</c> splat where the
/// directive sits, so the consumer's <c>style</c> arrives second and replaces everything written
/// before it — the flex sizing that makes a panel resizable, the grid template, the
/// <c>display: none</c> that hides a closed menu. Splatting <c>InlineStyleMerge.WithoutStyle(...)</c>
/// alongside a merged value keeps both.
/// </para>
/// <para>
/// Checked as text because both versions compile and render; only the order differs, and the loss
/// shows up solely when a consumer supplies a style.
/// </para>
/// </summary>
public partial class InlineStyleSplatTests
{
    [Fact]
    public void AnElementWithItsOwnStyleDoesNotSplatTheRawAttributes()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ComponentSources.Where(f => f.Extension == ".razor"))
        {
            var text = File.ReadAllText(file.FullName);

            foreach (Match match in StyleThenSplat().Matches(text))
            {
                var line = text.Take(match.Index).Count(c => c == '\n') + 1;
                offenders.Add($"{SourceTree.RelativePath(file)}:{line}");
            }
        }

        Assert.True(offenders.Count == 0,
            "These elements write a style and then splat AdditionalAttributes over it, so a "
            + "consumer style silently replaces the component's own. Render "
            + "InlineStyleMerge.Merge(...) into the style attribute and splat "
            + $"InlineStyleMerge.WithoutStyle(AdditionalAttributes).{Environment.NewLine}"
            + string.Join(Environment.NewLine, offenders));
    }

    [GeneratedRegex(@"<[^<>]*style=""@[^""]*""[^<>]*@attributes=""AdditionalAttributes""[^<>]*>", RegexOptions.Singleline)]
    private static partial Regex StyleThenSplat();
}
