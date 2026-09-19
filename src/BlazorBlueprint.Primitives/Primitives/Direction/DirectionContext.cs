using System.Globalization;

namespace BlazorBlueprint.Primitives;

/// <summary>
/// The writing direction that a <see cref="BbDirectionProvider"/> cascades to its descendants.
/// </summary>
/// <remarks>
/// <para>
/// Layout follows the direction through CSS: the library's styles use logical properties
/// (<c>margin-inline-start</c> rather than <c>margin-left</c>), so the <c>dir</c> attribute the
/// provider writes is enough to mirror them. This object exists for the parts that CSS cannot
/// decide — which way an arrow key moves, for one — and for JavaScript that needs the same answer
/// as the C# that rendered the markup.
/// </para>
/// <para>
/// A component reads it through a cascading parameter and resolves it with
/// <see cref="Resolve(DirectionContext?)"/>, so a component used without a provider still
/// behaves the way it always did: it follows the culture.
/// </para>
/// </remarks>
public sealed class DirectionContext
{
    /// <summary>The direction as configured, before <see cref="TextDirection.Auto"/> is resolved.</summary>
    public required TextDirection Direction { get; init; }

    /// <summary>
    /// Whether the resolved direction is right to left. <see cref="TextDirection.Auto"/> resolves
    /// against <see cref="CultureInfo.CurrentCulture"/>.
    /// </summary>
    public bool IsRightToLeft => Direction switch
    {
        TextDirection.RightToLeft => true,
        TextDirection.LeftToRight => false,
        _ => CultureInfo.CurrentCulture.TextInfo.IsRightToLeft
    };

    /// <summary>The value for an HTML <c>dir</c> attribute: <c>"rtl"</c> or <c>"ltr"</c>.</summary>
    public string Attribute => IsRightToLeft ? "rtl" : "ltr";

    /// <summary>
    /// Resolves a cascaded context, falling back to the current culture when no provider is
    /// present. This is the call a component should make; it never needs to test for null itself.
    /// </summary>
    /// <param name="context">The cascaded context, or <c>null</c> when there is no provider.</param>
    public static bool Resolve(DirectionContext? context) =>
        context?.IsRightToLeft ?? CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
}
