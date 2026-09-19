using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Primitives.Services;

/// <summary>
/// Resolves the effective <see cref="OverlayRenderingStrategy"/> for a component and drives
/// the browser's native overlay primitives (currently the <c>&lt;dialog&gt;</c> element).
/// </summary>
public interface INativeOverlayService
{
    /// <summary>
    /// Gets whether the browser supports the native <c>&lt;dialog&gt;</c> element's
    /// <c>showModal()</c>. Resolved once per scope and cached. Returns false when JS interop
    /// is unavailable (e.g. during prerendering). Used for diagnostics when native is requested.
    /// </summary>
    public Task<bool> IsDialogSupportedAsync();

    /// <summary>
    /// Resolves the strategy a component should render with, synchronously (safe to call during
    /// render). A non-null <paramref name="requested"/> (the component's own parameter) wins;
    /// otherwise the global default applies.
    /// </summary>
    public OverlayRenderingStrategy ResolveStrategy(OverlayRenderingStrategy? requested);

    /// <summary>
    /// Stops resolving anything to <see cref="OverlayRenderingStrategy.Native"/>, so every overlay
    /// from now on renders through the JavaScript strategy instead.
    /// </summary>
    /// <remarks>
    /// Called when a component finds that the browser has no <c>&lt;dialog&gt;.showModal()</c>.
    /// Support can only be established through JS interop, which is not available while the
    /// strategy is first resolved — so the fallback is applied on the way out rather than
    /// predicted on the way in. Previously this case only produced a log warning: the element
    /// rendered, never entered the top layer, and the user got a dialog with no modal behaviour
    /// and no focus trap at all.
    /// </remarks>
    public void FallBackToJavaScript();

    /// <summary>
    /// Opens a <c>&lt;dialog&gt;</c> element as a modal (top layer).
    /// </summary>
    public Task ShowDialogAsync(ElementReference element);

    /// <summary>
    /// Closes a <c>&lt;dialog&gt;</c> element.
    /// </summary>
    public Task CloseDialogAsync(ElementReference element, string? returnValue = null);

    /// <summary>
    /// Focuses a dialog's content after opening (falls back to first focusable element).
    /// </summary>
    public Task FocusDialogAsync(ElementReference element);

    /// <summary>
    /// Restores focus to the element that opened the dialog.
    /// </summary>
    public Task FocusTriggerAsync(ElementReference element);

    /// <summary>
    /// Wires native <c>&lt;dialog&gt;</c> lifecycle events (Escape/cancel, close, backdrop click)
    /// to a .NET instance. The returned handle must be disposed to remove the listeners.
    /// </summary>
    public Task<IAsyncDisposable> SetupDialogAsync(ElementReference element, object dotNetRef);
}
