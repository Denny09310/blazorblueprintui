namespace BlazorBlueprint.Components;

/// <summary>
/// Combines a component's own inline style with one the consumer passed through
/// <c>AdditionalAttributes</c>.
/// <para>
/// Razor applies attributes in source order, and an <c>@attributes</c> splat is applied where the
/// directive sits. So an element written as <c>style="@Merged" @attributes="AdditionalAttributes"</c>
/// computes the merge and then throws it away: the consumer's raw <c>style</c> arrives second and
/// replaces it. Splatting <see cref="WithoutStyle"/> instead keeps the merged value.
/// </para>
/// </summary>
internal static class InlineStyleMerge
{
    /// <summary>
    /// Returns the component's style followed by the consumer's, so the consumer's declarations win
    /// on conflict and are additive otherwise. Either side may be absent.
    /// </summary>
    public static string? Merge(string? componentStyle, IReadOnlyDictionary<string, object>? attributes)
    {
        var consumerStyle = ConsumerStyle(attributes);

        if (string.IsNullOrEmpty(componentStyle))
        {
            return string.IsNullOrEmpty(consumerStyle) ? null : consumerStyle;
        }

        return string.IsNullOrEmpty(consumerStyle)
            ? componentStyle
            : $"{componentStyle}; {consumerStyle}";
    }

    /// <summary>
    /// The same attributes with <c>style</c> removed, for splatting alongside a merged style.
    /// </summary>
    public static Dictionary<string, object>? WithoutStyle(Dictionary<string, object>? attributes)
    {
        if (attributes is null || !attributes.ContainsKey("style"))
        {
            return attributes;
        }

        var filtered = new Dictionary<string, object>(attributes, StringComparer.Ordinal);
        filtered.Remove("style");
        return filtered.Count > 0 ? filtered : null;
    }

    private static string? ConsumerStyle(IReadOnlyDictionary<string, object>? attributes) =>
        attributes is not null && attributes.TryGetValue("style", out var style)
            ? style?.ToString()
            : null;
}
