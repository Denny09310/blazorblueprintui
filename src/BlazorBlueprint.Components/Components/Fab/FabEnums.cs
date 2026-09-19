namespace BlazorBlueprint.Components;

/// <summary>
/// Defines the visual style variant of a <see cref="BbFab"/>.
/// </summary>
public enum FabVariant
{
    /// <summary>Solid primary fill. The default, and the right choice for the screen's main action.</summary>
    Primary,

    /// <summary>Solid secondary fill for a lower-emphasis action.</summary>
    Secondary,

    /// <summary>Background fill with a border, for a floating button over busy content.</summary>
    Surface,

    /// <summary>Destructive fill. Pair it with a confirmation step.</summary>
    Destructive
}

/// <summary>
/// Defines the size of a <see cref="BbFab"/>.
/// </summary>
public enum FabSize
{
    /// <summary>40px. Use inside a card or a toolbar.</summary>
    Small,

    /// <summary>56px, the standard floating size.</summary>
    Default,

    /// <summary>64px, for a touch-first layout.</summary>
    Large
}

/// <summary>
/// Defines where a <see cref="BbFab"/> pins itself.
/// </summary>
/// <remarks>
/// Every placement except <see cref="Inline"/> takes the button out of the document flow, so it
/// does not reserve space. <see cref="BbFab.Fixed"/> decides whether it pins to the viewport or
/// to the nearest positioned ancestor.
/// </remarks>
public enum FabPlacement
{
    /// <summary>Bottom trailing corner: bottom right in a left-to-right layout. The default.</summary>
    BottomEnd,

    /// <summary>Bottom leading corner.</summary>
    BottomStart,

    /// <summary>Bottom centre.</summary>
    BottomCenter,

    /// <summary>Top trailing corner.</summary>
    TopEnd,

    /// <summary>Top leading corner.</summary>
    TopStart,

    /// <summary>No positioning. The button sits in the normal document flow.</summary>
    Inline
}

/// <summary>
/// Defines the corner shape of a <see cref="BbFab"/>.
/// </summary>
public enum FabShape
{
    /// <summary>
    /// The theme's corner radius, the same one <see cref="BbButton"/> uses. The default, so the
    /// button stays in step with the rest of the theme; a theme with no radius renders it square.
    /// </summary>
    Rounded,

    /// <summary>
    /// Fully round: a circle, or a pill once the button carries a label.
    /// </summary>
    Circle
}
