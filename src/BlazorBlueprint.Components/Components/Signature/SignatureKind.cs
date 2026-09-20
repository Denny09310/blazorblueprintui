namespace BlazorBlueprint.Components;

/// <summary>
/// How a signature was given.
/// </summary>
public enum SignatureKind
{
    /// <summary>
    /// Nothing has been signed.
    /// </summary>
    None,

    /// <summary>
    /// Drawn by hand with a pointer, a finger or a pen.
    /// </summary>
    Drawn,

    /// <summary>
    /// Typed as a name. The route for anyone who cannot draw one.
    /// </summary>
    Typed
}
