namespace BlazorBlueprint.Components;

/// <summary>
/// Defines the colour treatment of a <see cref="BbLink"/>.
/// </summary>
public enum LinkVariant
{
    /// <summary>The primary colour. The default, and what a link in prose should normally be.</summary>
    Default,

    /// <summary>Inherits the surrounding text colour, for a link inside a paragraph that should not stand out.</summary>
    Inherit,

    /// <summary>Muted, for a secondary link such as one in a footer.</summary>
    Muted,

    /// <summary>The destructive colour, for a link to a removal or a cancellation.</summary>
    Destructive
}

/// <summary>
/// Defines when a <see cref="BbLink"/> draws its underline.
/// </summary>
public enum LinkUnderline
{
    /// <summary>
    /// Underlined on hover and on focus only. The default: it keeps a paragraph legible while
    /// still marking the link, because the colour alone is not an accessible affordance.
    /// </summary>
    Hover,

    /// <summary>Always underlined, which is the safest choice for a link inside a block of text.</summary>
    Always,

    /// <summary>
    /// Never underlined. Only appropriate where the link is obvious from its position, such as a
    /// navigation row; in prose it leaves colour as the sole cue.
    /// </summary>
    None
}
