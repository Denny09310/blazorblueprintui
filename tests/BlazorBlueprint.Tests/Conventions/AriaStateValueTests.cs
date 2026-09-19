using System.Text.RegularExpressions;

namespace BlazorBlueprint.Tests.Conventions;

/// <summary>
/// The ARIA state attributes take the strings <c>"true"</c> and <c>"false"</c>, never a bare
/// boolean.
/// <para>
/// Blazor treats a <see cref="bool"/> attribute value as an HTML boolean attribute: <c>true</c>
/// renders the name with an empty value, and <c>false</c> removes the attribute altogether. So
/// <c>aria-expanded="@IsOpen"</c> renders <c>aria-expanded=""</c> when open and nothing at all
/// when closed — a control that announces neither state. The markup looks right in the source and
/// wrong in the accessibility tree, which is why this is checked as text.
/// </para>
/// </summary>
public partial class AriaStateValueTests
{
    /// <summary>
    /// ARIA attributes whose value must be the literal string "true" or "false". Tri-state members
    /// (<c>aria-checked="mixed"</c>, <c>aria-current="page"</c>) are here too: those take other
    /// strings, but never a boolean.
    /// </summary>
    private static readonly string[] StateAttributes =
    [
        "aria-expanded", "aria-hidden", "aria-selected", "aria-checked", "aria-disabled",
        "aria-pressed", "aria-required", "aria-invalid", "aria-busy", "aria-readonly",
        "aria-multiselectable", "aria-multiline", "aria-modal", "aria-atomic", "aria-current",
    ];

    [Fact]
    public void AriaStateAttributesRenderAStringNotABoolean()
    {
        var offenders = new List<string>();

        foreach (var file in SourceTree.ComponentSources.Where(f => f.Extension == ".razor"))
        {
            var text = File.ReadAllText(file.FullName);
            var component = text + ReadCodeBehind(file.FullName);

            foreach (var (attribute, expression, index) in Bindings(text))
            {
                if (RendersAString(expression, component))
                {
                    continue;
                }

                var line = text.Take(index).Count(c => c == '\n') + 1;
                offenders.Add($"{SourceTree.RelativePath(file)}:{line} {attribute}=\"@{expression}\"");
            }
        }

        Assert.True(offenders.Count == 0,
            "These ARIA state attributes are bound to a boolean, so they render with an empty value "
            + "when true and vanish when false. Write the state out: "
            + $"@(condition ? \"true\" : \"false\").{Environment.NewLine}"
            + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>Every <c>aria-state="@expression"</c> in the markup, with the expression unwrapped.</summary>
    private static IEnumerable<(string Attribute, string Expression, int Index)> Bindings(string text)
    {
        foreach (var attribute in StateAttributes)
        {
            var needle = attribute + "=\"@";
            var at = text.IndexOf(needle, StringComparison.Ordinal);

            while (at >= 0)
            {
                var start = at + needle.Length;
                yield return (attribute, Expression(text, start), at);
                at = text.IndexOf(needle, start, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// Reads one Razor expression: a parenthesised block, tracking nesting and string literals so
    /// an inner <c>? "true" : "false"</c> does not end it early, or a plain member access.
    /// </summary>
    private static string Expression(string text, int start)
    {
        if (text[start] != '(')
        {
            // A member access or a method call, which ends where the attribute value ends. There
            // can be no string literal inside, or the quote would have closed the attribute.
            var end = text.IndexOf('"', start);
            return end < 0 ? text[start..] : text[start..end];
        }

        var depth = 0;
        var inString = false;

        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '"')
            {
                inString = !inString;
            }
            else if (!inString && text[i] == '(')
            {
                depth++;
            }
            else if (!inString && text[i] == ')' && --depth == 0)
            {
                return text[start..(i + 1)];
            }
        }

        return text[start..];
    }

    /// <summary>
    /// Whether the expression can only produce a string: it writes the state out as a literal, it
    /// converts explicitly, or it reads a member the component declares as a string.
    /// </summary>
    private static bool RendersAString(string expression, string component)
    {
        if (expression.Contains('"', StringComparison.Ordinal)
            || expression.Contains("ToString", StringComparison.Ordinal)
            || expression.Contains("Localizer", StringComparison.Ordinal))
        {
            return true;
        }

        var member = expression.TrimStart('(').Split('.')[^1].Trim(')', '?', ' ');

        return StringMember(member).IsMatch(component);
    }

    private static Regex StringMember(string member) =>
        new(@"\bstring\??\s+" + Regex.Escape(member) + @"\b", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static string ReadCodeBehind(string razorPath)
    {
        var behind = razorPath + ".cs";
        return File.Exists(behind) ? File.ReadAllText(behind) : string.Empty;
    }
}
