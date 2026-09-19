namespace BlazorBlueprint.Components;

/// <summary>
/// Defines how a <see cref="BbImage"/> fills the space it is given.
/// </summary>
public enum ImageFit
{
    /// <summary>No object-fit is applied; the image keeps its own dimensions. The default.</summary>
    None,

    /// <summary>Covers the box and crops the overflow. The usual choice for a fixed-size thumbnail.</summary>
    Cover,

    /// <summary>Fits inside the box, leaving space rather than cropping.</summary>
    Contain,

    /// <summary>Stretches to the box, which distorts the image.</summary>
    Fill,

    /// <summary>Like <see cref="Contain"/>, but never scales the image up past its natural size.</summary>
    ScaleDown
}
