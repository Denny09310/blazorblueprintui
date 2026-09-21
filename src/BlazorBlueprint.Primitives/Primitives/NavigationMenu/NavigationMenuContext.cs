using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Primitives.NavigationMenu;

/// <summary>
/// Root context for the navigation menu. Manages which item is open, close timers,
/// and trigger registration for keyboard navigation.
/// </summary>
public class NavigationMenuContext : IDisposable
{
    private readonly Action stateChanged;
    private readonly List<(object Owner, ElementReference Element)> triggerRefs = new();
    private CancellationTokenSource? closeTimerCts;

    /// <summary>
    /// The value of the currently open menu item, or <c>null</c> if all are closed.
    /// </summary>
    public string? ActiveItem { get; private set; }

    /// <summary>
    /// Whether keyboard navigation within dropdowns is enabled.
    /// </summary>
    public bool EnableKeyboardNavigation { get; }

    /// <summary>
    /// Number of registered triggers.
    /// </summary>
    public int TriggerCount => triggerRefs.Count;

    /// <summary>
    /// Creates a new <see cref="NavigationMenuContext"/>.
    /// </summary>
    public NavigationMenuContext(Action stateChanged, bool enableKeyboardNavigation)
    {
        this.stateChanged = stateChanged;
        EnableKeyboardNavigation = enableKeyboardNavigation;
    }

    /// <summary>
    /// Sets the active menu item and triggers a re-render.
    /// </summary>
    public void SetActiveItem(string? value)
    {
        ActiveItem = value;
        stateChanged();
    }

    /// <summary>
    /// Registers a trigger, or replaces the element reference already held for it.
    /// </summary>
    /// <param name="owner">The trigger component, used as its identity in the list.</param>
    /// <param name="triggerRef">The trigger's element, which is only real after its first render.</param>
    /// <remarks>
    /// Keyed by the component rather than by a position the caller has to remember. The previous
    /// index-based pair of methods could not express removal — taking an entry out would have
    /// shifted every index handed out after it — so triggers were never unregistered and arrow-key
    /// navigation kept stepping onto buttons that had left the page.
    /// </remarks>
    public void RegisterTrigger(object owner, ElementReference triggerRef)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var index = IndexOf(owner);

        if (index >= 0)
        {
            triggerRefs[index] = (owner, triggerRef);
            return;
        }

        triggerRefs.Add((owner, triggerRef));
    }

    /// <summary>
    /// Removes a trigger. Call it when the trigger is disposed.
    /// </summary>
    /// <param name="owner">The trigger component passed to <see cref="RegisterTrigger"/>.</param>
    public void UnregisterTrigger(object owner)
    {
        var index = IndexOf(owner);

        if (index >= 0)
        {
            triggerRefs.RemoveAt(index);
        }
    }

    /// <summary>
    /// Gets the trigger element at the specified index, in registration order.
    /// </summary>
    public ElementReference? GetTriggerAt(int index)
    {
        if (index >= 0 && index < triggerRefs.Count)
        {
            return triggerRefs[index].Element;
        }

        return null;
    }

    /// <summary>
    /// The trigger's current position in the list, or -1 when it is not registered.
    /// </summary>
    /// <param name="owner">The trigger component passed to <see cref="RegisterTrigger"/>.</param>
    /// <remarks>
    /// Read this when you need a trigger's index — do not remember one. A trigger removed from the
    /// page takes its entry with it, and every index after it shifts.
    /// </remarks>
    public int TriggerIndexOf(object owner) => IndexOf(owner);

    private int IndexOf(object owner) =>
        triggerRefs.FindIndex(entry => ReferenceEquals(entry.Owner, owner));

    /// <summary>
    /// Starts a shared close timer. After the delay, closes all menus.
    /// Cancel with <see cref="CancelCloseTimer"/>.
    /// </summary>
    public async void StartCloseTimer()
    {
        CancelCloseTimer();
        closeTimerCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(150, closeTimerCts.Token);
            SetActiveItem(null);
        }
        catch (TaskCanceledException)
        {
            // Timer was cancelled
        }
        catch (Exception)
        {
            // async void: nothing awaits this, so an exception that escapes has no caller to reach and
            // Blazor Server treats it as fatal — the circuit closes and the user sees the reconnect
            // overlay. Everything this method does is best-effort, and none of it is worth that.
        }
    }

    /// <summary>
    /// Cancels any pending close timer.
    /// </summary>
    public void CancelCloseTimer()
    {
        closeTimerCts?.Cancel();
        closeTimerCts?.Dispose();
        closeTimerCts = null;
    }

    /// <summary>
    /// Disposes the close timer resources.
    /// </summary>
    public void Dispose()
    {
        closeTimerCts?.Cancel();
        closeTimerCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Per-item context for a navigation menu item.
/// </summary>
public class NavigationMenuItemContext
{
    private readonly NavigationMenuContext parent;

    /// <summary>
    /// Unique value identifying this menu item.
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// Whether this item's dropdown is currently open.
    /// </summary>
    public bool IsOpen => Value is not null && parent.ActiveItem == Value;

    /// <summary>
    /// Creates a new <see cref="NavigationMenuItemContext"/>.
    /// </summary>
    public NavigationMenuItemContext(NavigationMenuContext parent, string? value)
    {
        this.parent = parent;
        Value = value;
    }

    /// <summary>Sets this item as open.</summary>
    public void Open() => parent.SetActiveItem(Value);

    /// <summary>Closes this item if open.</summary>
    public void Close()
    {
        if (IsOpen)
        {
            parent.SetActiveItem(null);
        }
    }

    /// <summary>Toggles open/closed.</summary>
    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    /// <summary>Starts the parent's shared close timer.</summary>
    public void StartCloseTimer() => parent.StartCloseTimer();

    /// <summary>Cancels the parent's close timer.</summary>
    public void CancelCloseTimer() => parent.CancelCloseTimer();
}
