namespace BlazorBlueprint.Components;

/// <summary>
/// The non-generic bridge a <see cref="BbChipSet{TValue}"/> cascades to its chips.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BbChip"/> is deliberately non-generic so a chip can be used on its own, without a
/// set and without a type argument. The set keeps its own <c>TValue</c> and exposes the three
/// operations a chip needs through this object, boxing the value on the way in and casting it
/// back on the way out.
/// </para>
/// <para>
/// The object identity stays stable for the lifetime of the set, so it is cascaded as fixed.
/// Chips therefore register themselves here and the set re-renders them explicitly through
/// <see cref="NotifyStateChanged"/> when the selection changes.
/// </para>
/// </remarks>
internal sealed class ChipSetContext
{
    private readonly List<BbChip> chips = [];

    /// <summary>The variant a chip falls back to when it declares none of its own.</summary>
    internal ChipVariant Variant { get; set; } = ChipVariant.Default;

    /// <summary>The size a chip falls back to when it declares none of its own.</summary>
    internal ChipSize Size { get; set; } = ChipSize.Default;

    /// <summary>Whether chips show a dismiss button unless they opt out.</summary>
    internal bool Dismissible { get; set; }

    /// <summary>Whether selected chips show a check mark unless they opt out.</summary>
    internal bool ShowCheckMark { get; set; }

    /// <summary>Whether every chip in the set is disabled.</summary>
    internal bool Disabled { get; set; }

    /// <summary>How many chips the set can select at once.</summary>
    internal ChipSelectionMode SelectionMode { get; set; } = ChipSelectionMode.Single;

    /// <summary>Reports whether the set currently holds the given chip value.</summary>
    internal Func<object?, bool> IsSelected { get; set; } = static _ => false;

    /// <summary>Adds or removes the given chip value from the set's selection.</summary>
    internal Func<object?, Task> ToggleAsync { get; set; } = static _ => Task.CompletedTask;

    /// <summary>Drops the given chip value from the selection and raises the set's dismiss callback.</summary>
    internal Func<object?, Task> DismissAsync { get; set; } = static _ => Task.CompletedTask;

    /// <summary>Tracks a chip so a selection change can re-render it.</summary>
    internal void Register(BbChip chip)
    {
        if (!chips.Contains(chip))
        {
            chips.Add(chip);
        }
    }

    /// <summary>Stops tracking a disposed chip.</summary>
    internal void Unregister(BbChip chip) => chips.Remove(chip);

    /// <summary>Re-renders every registered chip after the selection changed.</summary>
    internal void NotifyStateChanged()
    {
        foreach (var chip in chips)
        {
            chip.NotifySetStateChanged();
        }
    }
}
