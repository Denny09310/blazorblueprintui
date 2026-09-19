namespace BlazorBlueprint.Primitives;

/// <summary>
/// The writing direction a <see cref="BbDirectionProvider"/> applies to its content.
/// </summary>
public enum TextDirection
{
    /// <summary>
    /// Take the direction from the current culture. The default, so an application that already
    /// sets its culture per request needs no other configuration.
    /// </summary>
    Auto,

    /// <summary>Left to right, as in English.</summary>
    LeftToRight,

    /// <summary>Right to left, as in Arabic or Hebrew.</summary>
    RightToLeft
}
