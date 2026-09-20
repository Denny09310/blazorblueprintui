namespace BlazorBlueprint.Components;

/// <summary>
/// A captured signature, however it was given.
/// </summary>
public sealed record SignatureValue
{
    /// <summary>
    /// Gets how the signature was given.
    /// </summary>
    public SignatureKind Kind { get; init; } = SignatureKind.None;

    /// <summary>
    /// Gets the signature as SVG markup, or <c>null</c> when there is none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ink is <c>currentColor</c>, so the same markup renders dark on paper and light on a
    /// dark page. This is the form to store.
    /// </para>
    /// <para>
    /// A typed signature is a single <c>&lt;text&gt;</c> element with a font stack rather than an
    /// outline, so it renders in whatever handwriting font the viewing machine has. Where that
    /// matters, store a rendered image alongside it.
    /// </para>
    /// </remarks>
    public string? Svg { get; init; }

    /// <summary>
    /// Gets the typed name, when <see cref="Kind"/> is <see cref="SignatureKind.Typed"/>.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets whether nothing has been signed.
    /// </summary>
    public bool IsEmpty => Kind == SignatureKind.None;

    /// <summary>
    /// An unsigned value.
    /// </summary>
    public static SignatureValue Empty { get; } = new();
}
