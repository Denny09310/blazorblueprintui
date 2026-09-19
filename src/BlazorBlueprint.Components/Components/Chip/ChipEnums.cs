using System.Diagnostics.CodeAnalysis;

namespace BlazorBlueprint.Components;

/// <summary>
/// Defines the visual style variant of a <see cref="BbChip"/>.
/// </summary>
/// <remarks>
/// Each variant carries two treatments: an unselected look and a selected look. Selection is
/// only rendered for a chip that participates in selection, either through
/// <see cref="BbChip.Selectable"/> or through a <see cref="BbChipSet{TValue}"/>.
/// </remarks>
public enum ChipVariant
{
    /// <summary>Neutral filled chip. Selects to the primary colour.</summary>
    Default,

    /// <summary>Muted filled chip for lower emphasis. Selects to a soft primary tint.</summary>
    Secondary,

    /// <summary>Transparent chip with a border. Selects to the accent colour.</summary>
    Outline,

    /// <summary>Error or removal palette. Selects to a solid destructive fill.</summary>
    Destructive,

    /// <summary>Soft primary tint. Selects to a solid primary fill.</summary>
    Soft
}

/// <summary>
/// Defines the size of a <see cref="BbChip"/>.
/// </summary>
public enum ChipSize
{
    /// <summary>Compact chip, 24px tall.</summary>
    Small,

    /// <summary>Standard chip, 32px tall.</summary>
    Default,

    /// <summary>Roomy chip, 40px tall.</summary>
    Large
}

/// <summary>
/// Defines how many chips a <see cref="BbChipSet{TValue}"/> can select at once.
/// </summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Single is a domain term for selection mode")]
public enum ChipSelectionMode
{
    /// <summary>Chips are not selectable. The set only groups and spaces them.</summary>
    None,

    /// <summary>One chip at a time, bound through <c>Value</c>.</summary>
    Single,

    /// <summary>Any number of chips, bound through <c>Values</c>.</summary>
    Multiple
}
