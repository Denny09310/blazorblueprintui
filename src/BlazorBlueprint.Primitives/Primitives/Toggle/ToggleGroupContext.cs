namespace BlazorBlueprint.Primitives.Toggle;

/// <summary>
/// Context class for sharing state between ToggleGroup and ToggleGroupItem components.
/// </summary>
/// <typeparam name="TValue">The type of the value associated with toggle group items.</typeparam>
public class ToggleGroupContext<TValue>
{
    /// <summary>
    /// Gets or sets the currently selected value (single selection mode).
    /// </summary>
    public TValue? Value { get; set; }

    /// <summary>
    /// Gets or sets the currently selected values (multiple selection mode).
    /// </summary>
    public List<TValue> Values { get; set; } = new();

    /// <summary>
    /// Gets or sets the selection type (Single or Multiple).
    /// </summary>
    public ToggleGroupType Type { get; set; }

    /// <summary>
    /// Gets or sets whether the group is disabled.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when a toggle item is toggled.
    /// </summary>
    public Func<TValue, Task>? ToggleItem { get; set; }

    /// <summary>
    /// Gets or sets the orientation of the group.
    /// </summary>
    public string Orientation { get; set; } = "horizontal";

    /// <summary>
    /// Gets or sets the list of registered toggle group items.
    /// </summary>
    public List<BbToggleGroupItem<TValue>> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets the callback an item raises when it receives focus, so the group's arrow-key
    /// navigation starts from the item the user is actually on rather than the one it last moved
    /// to itself.
    /// </summary>
    public Action<BbToggleGroupItem<TValue>>? ItemFocused { get; set; }
}
