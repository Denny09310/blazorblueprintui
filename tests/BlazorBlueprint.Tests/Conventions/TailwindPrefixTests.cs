using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace BlazorBlueprint.Tests.Conventions;

/// <summary>
/// Every Tailwind utility the library emits carries the <c>bb:</c> prefix and lives in the
/// <c>bb-utilities</c> cascade layer, so a consumer's own Tailwind build can never produce the
/// same class name and the two stylesheets cannot fight over an element (#496).
///
/// The build side is guarded by reading the emitted stylesheet. The source side is guarded too,
/// because under a prefixed build an unprefixed token in a component's class string does not
/// collide — it silently emits nothing, which is worse.
/// </summary>
public class TailwindPrefixTests
{
    private const string Prefix = "bb:";

    /// <summary>
    /// Class names that legitimately sit in a component's class string without the prefix:
    /// hand-authored selectors from <c>blazorblueprint-input.css</c>, markers consumed by an
    /// arbitrary group variant or by consumer CSS, and typography-plugin hooks a consumer's build may style.
    /// </summary>
    private static readonly Regex Neutral = new(
        @"^(bb-[a-z0-9-]+|aspect-ratio-content|sortable-[a-z-]+|blazorblueprint-portal|destructive|aria-current-step|origin-top-center|prose|prose-sm|dark:prose-invert)$",
        RegexOptions.Compiled);

    private static readonly Regex ClassAttribute = new(
        @"(?<=[\s""])(?:[A-Za-z_@:-]*[Cc]lass[A-Za-z]*|PopoverWidth)=""",
        RegexOptions.Compiled);

    private static bool IsAllowed(string token) =>
        token.StartsWith(Prefix, StringComparison.Ordinal) || Neutral.IsMatch(token);

    // ---------------------------------------------------------------------------------------
    // Built stylesheet
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Cascade-layer priority is fixed by the order in which layer names first appear in the
    /// document. The minifier rewrites the input's explicit <c>@layer a, b, c;</c> statement into
    /// first-appearance order, so the effective order is what matters: the consumer's standard
    /// layers first, then the library's prefixed utilities, then the consumer's, then <c>bb</c>.
    /// </summary>
    [Fact]
    public void BuiltStylesheetEstablishesTheSharedLayerOrder()
    {
        var css = ReadBuiltStylesheet();

        var order = new List<string>();
        foreach (var name in TopLevelLayerNames(css))
        {
            if (!order.Contains(name))
            {
                order.Add(name);
            }
        }

        Assert.Equal(["properties", "theme", "base", "components", "bb-utilities", "utilities", "bb"], order);
    }

    [Fact]
    public void BuiltStylesheetWritesNothingIntoTheSharedUtilitiesLayer()
    {
        var css = ReadBuiltStylesheet();

        foreach (var (prelude, _) in TopLevelBlocks(css))
        {
            Assert.False(
                Regex.IsMatch(prelude, @"^@layer\s+utilities\s*$"),
                "blazorblueprint.css must not write into the shared `utilities` layer; a consumer's build shares that layer and the two collide (#496).");
        }
    }

    /// <summary>
    /// The default border colour must be carried by a selector that outranks Tailwind's preflight
    /// on specificity, not merely on document order.
    /// <para>
    /// The library and a consumer's own Tailwind build both write preflight into the shared
    /// <c>base</c> layer, and preflight's <c>border: 0 solid</c> resets <c>border-color</c> to
    /// <c>currentColor</c>. A bare <c>*</c> rule ties with it on specificity, so whichever
    /// stylesheet is linked second wins — and when it is the consumer's, every library border
    /// renders near-black (#527). Matching on the class attribute settles it at 0,1,0, which link
    /// order cannot change. It has to stay in <c>base</c>: from a later layer it would outrank the
    /// consumer's own <c>border-*</c> utilities, which is #398 again in the other direction.
    /// </para>
    /// </summary>
    [Fact]
    public void DefaultBorderColourOutranksPreflightOnSpecificity()
    {
        var css = ReadBuiltStylesheet();

        var baseBlock = TopLevelBlocks(css)
            .Where(b => Regex.IsMatch(b.Prelude, @"^@layer\s+base\s*$"))
            .Select(b => b.Body)
            .FirstOrDefault();

        Assert.False(baseBlock is null, "blazorblueprint.css has no top-level `@layer base` block.");

        // The minifier may drop the quotes and escape the colon, so accept either spelling.
        var guarded = RuleSelectors(baseBlock!).Any(selector =>
            Regex.IsMatch(selector, @"\[class\*=""?bb\\?:border""?\]"));

        Assert.True(guarded,
            "No `[class*=\"bb:border\"]` rule in @layer base. The library's default border colour "
            + "would then rest on a bare `*` selector, which ties with a consumer's own Tailwind "
            + "preflight and loses whenever their stylesheet is linked second — every border turns "
            + "near-black (#527). Keep the rule in `base`, and keep it matching on the class "
            + "attribute so specificity, not link order, decides.");
    }

    [Fact]
    public void EveryUtilityInTheBuiltStylesheetIsPrefixed()
    {
        var css = ReadBuiltStylesheet();
        var block = TopLevelBlocks(css)
            .Where(b => Regex.IsMatch(b.Prelude, @"^@layer\s+bb-utilities\s*$"))
            .Select(b => b.Body)
            .SingleOrDefault();

        Assert.False(block is null, "blazorblueprint.css has no top-level `@layer bb-utilities` block.");

        var offenders = new List<string>();
        foreach (var selector in RuleSelectors(block!))
        {
            var own = selector.StartsWith(":is(", StringComparison.Ordinal) ? selector[4..] : selector;
            if (own.StartsWith('.') && !own.StartsWith(@".bb\:", StringComparison.Ordinal))
            {
                offenders.Add(selector);
            }
        }

        Assert.True(offenders.Count == 0,
            "Unprefixed utilities in @layer bb-utilities:\n  " + string.Join("\n  ", offenders.Distinct().Take(40)));
    }

    // ---------------------------------------------------------------------------------------
    // Component sources
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void RazorClassAttributesUseThePrefix()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ComponentSources.Where(f => f.Extension == ".razor"))
        {
            var text = File.ReadAllText(file.FullName);
            foreach (Match m in ClassAttribute.Matches(text))
            {
                var line = 1 + text.AsSpan(0, m.Index).Count('\n');
                foreach (var token in PlainTokensOfAttributeValue(text, m.Index + m.Length))
                {
                    if (!IsAllowed(token))
                    {
                        offenders.Add($"{SourceTree.RelativePath(file)}:{line} `{token}`");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, Explain(offenders));
    }

    [Fact]
    public void ClassStringsPassedToCnUseThePrefix()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ComponentSources)
        {
            var text = StripLineComments(File.ReadAllText(file.FullName));
            foreach (Match m in Regex.Matches(text, @"(?<![A-Za-z0-9_])cn\("))
            {
                var argsStart = m.Index + m.Length;
                var argsEnd = MatchingParen(text, argsStart - 1);
                if (argsEnd < 0)
                {
                    continue;
                }

                var line = 1 + text.AsSpan(0, m.Index).Count('\n');
                foreach (var literal in StringLiterals(text.AsSpan(argsStart, argsEnd - argsStart).ToString()))
                {
                    foreach (var token in literal.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (token.Contains('{') || token.Contains('}'))
                        {
                            continue; // interpolation hole
                        }

                        if (!IsAllowed(token))
                        {
                            offenders.Add($"{SourceTree.RelativePath(file)}:{line} `{token}`");
                        }
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, Explain(offenders));
    }

    private static string Explain(List<string> offenders) =>
        "Tailwind utilities in library class strings must carry the `bb:` prefix (see #496). " +
        "Under the prefixed build an unprefixed token emits no CSS at all. Offenders:\n  " +
        string.Join("\n  ", offenders.Take(60)) +
        (offenders.Count > 60 ? $"\n  … and {offenders.Count - 60} more" : string.Empty);

    /// <summary>
    /// A <c>group</c>/<c>peer</c> marker must reach the DOM in its bare form as well as its
    /// prefixed one, because a consumer's Tailwind build compiles <c>group-*</c>/<c>peer-*</c> to
    /// <c>:where(.group)</c> and never sees the <c>bb</c> prefix. <c>ClassNames.cn</c> adds the bare
    /// twin, so the only way to lose it is to hand a marker literal straight to a component's
    /// <c>Class</c> parameter — a primitive concatenates <c>Class</c> verbatim, with no merge.
    /// </summary>
    [Fact]
    public void MarkerLiteralsGoThroughCn()
    {
        var markerInLiteralClass = new Regex(
            @"[Cc]lass=""(?<value>(?:[^""@]|@(?!\())*?bb:(?:group|peer)(?:/[\w-]+)?(?![\w/-])[^""]*)""",
            RegexOptions.Compiled);

        var offenders = new List<string>();

        foreach (var file in SourceTree.ComponentSources)
        {
            var text = File.ReadAllText(file.FullName);
            foreach (Match m in markerInLiteralClass.Matches(text))
            {
                var line = 1 + text.AsSpan(0, m.Index).Count('\n');
                offenders.Add($"{SourceTree.RelativePath(file)}:{line} `{m.Groups["value"].Value}`");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A bb:group/bb:peer marker in a literal Class attribute never gains its bare twin, so a " +
            "consumer's own group-*/peer-* variants silently match nothing. Wrap it in ClassNames.cn(...). " +
            "Offenders:\n  " + string.Join("\n  ", offenders));
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    private static string ReadBuiltStylesheet()
    {
        var path = Path.Combine(SourceTree.RepoRoot.FullName,
            "src", "BlazorBlueprint.Components", "wwwroot", "blazorblueprint.css");
        Assert.True(File.Exists(path), $"Built stylesheet not found at {path}; build BlazorBlueprint.Components first.");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Layer names in document order from every top-level <c>@layer</c> statement or block,
    /// including each name of a comma-separated statement.
    /// </summary>
    private static IEnumerable<string> TopLevelLayerNames(string css)
    {
        var depth = 0;
        var preludeStart = 0;

        for (var i = 0; i < css.Length; i++)
        {
            var c = css[i];
            if (c == '\\')
            {
                i++;
                continue;
            }

            if (c == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                var close = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    yield break;
                }

                i = close + 1;
                if (depth == 0)
                {
                    preludeStart = i + 1;
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                i = SkipQuoted(css, i);
                continue;
            }

            if (c is '{' or ';')
            {
                if (depth == 0)
                {
                    var prelude = css[preludeStart..i].Trim();
                    if (prelude.StartsWith("@layer", StringComparison.Ordinal))
                    {
                        foreach (var name in prelude[6..].Split(','))
                        {
                            yield return name.Trim();
                        }
                    }

                    preludeStart = i + 1;
                }

                if (c == '{')
                {
                    depth++;
                }
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    preludeStart = i + 1;
                }
            }
        }
    }

    /// <summary>Top-level <c>prelude { body }</c> blocks of a stylesheet, quote-aware.</summary>
    private static IEnumerable<(string Prelude, string Body)> TopLevelBlocks(string css)
    {
        var depth = 0;
        var preludeStart = 0;
        var bodyStart = -1;
        string? prelude = null;

        for (var i = 0; i < css.Length; i++)
        {
            var c = css[i];
            if (c == '\\')
            {
                i++; // CSS escape, e.g. the `\'` in `[class*=\'size-\']`
                continue;
            }

            if (c == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                var close = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    yield break;
                }

                i = close + 1;
                if (depth == 0)
                {
                    preludeStart = i + 1;
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                i = SkipQuoted(css, i);
                continue;
            }

            if (c == '{')
            {
                if (depth == 0)
                {
                    prelude = css[preludeStart..i].Trim();
                    bodyStart = i + 1;
                }

                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    yield return (prelude!, css[bodyStart..i]);
                    preludeStart = i + 1;
                }
            }
            else if (c == ';' && depth == 0)
            {
                preludeStart = i + 1;
            }
        }
    }

    /// <summary>Every rule selector inside a block, at any nesting depth, quote-aware.</summary>
    private static IEnumerable<string> RuleSelectors(string body)
    {
        var start = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (c == '\\')
            {
                i++;
                continue;
            }

            if (c is '"' or '\'')
            {
                i = SkipQuoted(body, i);
                continue;
            }

            if (c == '{')
            {
                var prelude = body[start..i].Trim();
                if (prelude.Length > 0 && prelude[0] != '@')
                {
                    foreach (var part in SplitSelectorList(prelude))
                    {
                        yield return part;
                    }
                }

                start = i + 1;
            }
            else if (c is '}' or ';')
            {
                start = i + 1;
            }
        }
    }

    /// <summary>
    /// Splits a selector list on its top-level commas only.
    /// </summary>
    /// <remarks>
    /// A plain <c>Split(',')</c> tears apart the argument list of a functional pseudo-class:
    /// <c>.bb\:dark\:bg-input:where(.dark, .dark *)</c> becomes two fragments, and the second one
    /// reads as a bare unprefixed <c>.dark *)</c> selector that no rule actually declares. Tailwind
    /// emits exactly that shape for a custom <c>dark</c> variant, so the naive split reported the
    /// whole dark-mode fix as a prefix violation.
    /// </remarks>
    private static IEnumerable<string> SplitSelectorList(string prelude)
    {
        var depth = 0;
        var start = 0;

        for (var i = 0; i < prelude.Length; i++)
        {
            var c = prelude[i];

            if (c == '\\')
            {
                i++;
                continue;
            }

            if (c is '(' or '[')
            {
                depth++;
            }
            else if (c is ')' or ']')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                yield return prelude[start..i].Trim();
                start = i + 1;
            }
        }

        yield return prelude[start..].Trim();
    }

    private static int SkipQuoted(string text, int openIndex)
    {
        var quote = text[openIndex];
        var i = openIndex + 1;
        while (i < text.Length && text[i] != quote)
        {
            if (text[i] == '\\')
            {
                i++;
            }

            i++;
        }

        return i;
    }

    /// <summary>
    /// The whitespace-separated tokens of a Razor attribute value that are literal text — not
    /// part of an <c>@(…)</c> or <c>@Identifier(…)</c> expression.
    /// </summary>
    private static string[] PlainTokensOfAttributeValue(string text, int valueStart)
    {
        var plain = new StringBuilder();
        var i = valueStart;
        while (i < text.Length && text[i] != '"')
        {
            if (text[i] == '@')
            {
                if (i + 1 < text.Length && text[i + 1] == '@')
                {
                    plain.Append('@');
                    i += 2;
                    continue;
                }

                var end = SkipRazorExpression(text, i);
                if (end > i)
                {
                    plain.Append(' ');
                    i = end;
                    continue;
                }
            }

            plain.Append(text[i]);
            i++;
        }

        return plain.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Returns the index just past a Razor expression starting at <paramref name="at"/> (an <c>@</c>), or <paramref name="at"/> if none.</summary>
    private static int SkipRazorExpression(string text, int at)
    {
        var i = at + 1;
        if (i < text.Length && text[i] == '(')
        {
            var end = MatchingParen(text, i);
            return end < 0 ? at : end + 1;
        }

        var ident = Regex.Match(text[i..], @"^[A-Za-z_][A-Za-z0-9_]*");
        if (!ident.Success)
        {
            return at;
        }

        i += ident.Length;
        while (i < text.Length)
        {
            if (text[i] == '.' && i + 1 < text.Length && (char.IsLetter(text[i + 1]) || text[i + 1] == '_'))
            {
                var member = Regex.Match(text[(i + 1)..], @"^[A-Za-z_][A-Za-z0-9_]*");
                i += 1 + member.Length;
                continue;
            }

            if (text[i] is '(' or '[')
            {
                var end = MatchingParen(text, i);
                if (end < 0)
                {
                    return i;
                }

                i = end + 1;
                continue;
            }

            break;
        }

        return i;
    }

    /// <summary>Index of the bracket closing the one at <paramref name="openIndex"/>, string-aware; -1 if unbalanced.</summary>
    private static int MatchingParen(string text, int openIndex)
    {
        var open = text[openIndex];
        var close = open == '(' ? ')' : ']';
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                i = SkipQuoted(text, i);
                continue;
            }

            if (c == open)
            {
                depth++;
            }
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>Bodies of the C# string literals in <paramref name="code"/> (plain, verbatim and interpolated).</summary>
    private static IEnumerable<string> StringLiterals(string code)
    {
        for (var i = 0; i < code.Length; i++)
        {
            if (code[i] != '"')
            {
                continue;
            }

            var verbatim = (i > 0 && code[i - 1] == '@') || (i > 1 && code[i - 2] == '@');
            var end = SkipQuoted(code, i);
            while (verbatim && end + 1 < code.Length && code[end + 1] == '"')
            {
                end = SkipQuoted(code, end + 1);
            }

            if (end > i + 1)
            {
                yield return code[(i + 1)..end];
            }

            i = end;
        }
    }

    private static string StripLineComments(string code)
    {
        var sb = new StringBuilder(code.Length);
        foreach (var line in code.Split('\n'))
        {
            sb.Append(line.TrimStart().StartsWith("//", StringComparison.Ordinal) ? string.Empty : line).Append('\n');
        }

        return sb.ToString();
    }
}
