using BlazorBlueprint.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Components;

/// <summary>
/// Holds a navigation while there is unsaved work, and asks before letting it go.
/// </summary>
/// <remarks>
/// <para>
/// Set <see cref="Enabled"/> when a form becomes dirty and clear it once the work is saved. While
/// it is set, two different departures are covered:
/// </para>
/// <list type="bullet">
/// <item>
/// A navigation inside the application is held and this component's own dialog is shown, so the
/// wording and the buttons match the rest of the interface.
/// </item>
/// <item>
/// Closing the tab, reloading, or following a link out of the application arms the browser's own
/// prompt. Browsers deliberately ignore a custom message there and show their own wording; that
/// cannot be changed from script, which is exactly why the in-application case is handled
/// separately.
/// </item>
/// </list>
/// <para>
/// The component renders nothing until a navigation is actually held. Place one near the form it
/// guards; several on a page each guard their own work, and the first that is enabled prompts.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BbExitPrompt Enabled="@(editContext?.IsModified() == true)" /&gt;
///
/// &lt;BbExitPrompt Enabled="@hasChanges"
///               Message="Your draft is not saved yet."
///               OnPrompted="() => analytics.Track(&quot;exit-prompt&quot;)" /&gt;
/// </code>
/// </example>
public partial class BbExitPrompt : ComponentBase, IAsyncDisposable
{
    private readonly string instanceId = $"bb-exit-prompt-{Guid.NewGuid():N}";
    private IDisposable? navigationRegistration;
    private IJSObjectReference? module;
    private string? pending;
    private bool armedInBrowser;

    /// <summary>
    /// Gets or sets whether there is unsaved work to guard. Nothing is intercepted while this is
    /// <c>false</c>.
    /// </summary>
    [Parameter]
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the dialog title. Defaults to the localized <c>ExitPrompt.Title</c>.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the dialog body. Defaults to the localized <c>ExitPrompt.Message</c>.
    /// Not used for the browser's own prompt, which shows its own wording.
    /// </summary>
    [Parameter]
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the label of the button that cancels the navigation.
    /// </summary>
    [Parameter]
    public string? StayLabel { get; set; }

    /// <summary>
    /// Gets or sets the label of the button that continues the navigation and abandons the work.
    /// </summary>
    [Parameter]
    public string? LeaveLabel { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when a navigation has been held and the dialog shown.
    /// </summary>
    [Parameter]
    public EventCallback OnPrompted { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the navigation is allowed to continue.
    /// </summary>
    [Parameter]
    public EventCallback OnLeft { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the navigation is cancelled and the work kept.
    /// </summary>
    [Parameter]
    public EventCallback OnStayed { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Inject]
    private IBbLocalizer Localizer { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnInitialized() =>
        navigationRegistration = Navigation.RegisterLocationChangingHandler(HandleLocationChangingAsync);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender) => await SyncBrowserPromptAsync();

    /// <summary>
    /// Arms or disarms the browser's own prompt when <see cref="Enabled"/> changes.
    /// Called after rendering so interop is available; unchanged state preserves the listener.
    /// </summary>
    private async Task SyncBrowserPromptAsync()
    {
        if (Enabled == armedInBrowser)
        {
            return;
        }

        try
        {
            module ??= await JsModules.GetAsync(
                JS, "./_content/BlazorBlueprint.Components/js/exit-prompt.js");
            await module.InvokeVoidAsync(Enabled ? "arm" : "disarm", instanceId);
            armedInBrowser = Enabled;
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException
            or TaskCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // Prerendering, or a circuit that went away. In-application navigation is still
            // guarded, because that path never leaves C#.
        }
    }

    /// <summary>
    /// Refuses an in-application navigation and remembers where it was going.
    /// </summary>
    /// <remarks>
    /// The handler returns straight away rather than waiting for the answer. Holding the router
    /// open until a button is pressed and then navigating from inside the handler moved the URL
    /// without rendering the new page — the reissued navigation was re-entrant within the
    /// pipeline still processing the refused one. Refusing, then navigating from the button's own
    /// handler, keeps the two apart.
    /// </remarks>
    private ValueTask HandleLocationChangingAsync(LocationChangingContext context)
    {
        if (!Enabled || pending is not null)
        {
            return ValueTask.CompletedTask;
        }

        context.PreventNavigation();
        pending = context.TargetLocation;

        return new ValueTask(PromptAsync());
    }

    private async Task PromptAsync()
    {
        await InvokeAsync(StateHasChanged);

        if (OnPrompted.HasDelegate)
        {
            await OnPrompted.InvokeAsync();
        }
    }

    private Task HandleOpenChangedAsync(bool open) => open ? Task.CompletedTask : StayAsync();

    private async Task StayAsync()
    {
        if (pending is null)
        {
            return;
        }

        pending = null;
        StateHasChanged();

        if (OnStayed.HasDelegate)
        {
            await OnStayed.InvokeAsync();
        }
    }

    private async Task LeaveAsync()
    {
        var target = pending;

        if (target is null)
        {
            return;
        }

        // The guard comes down before the navigation is reissued, so this same handler does not
        // hold the reissue as well.
        Enabled = false;
        pending = null;
        await SyncBrowserPromptAsync();
        StateHasChanged();

        if (OnLeft.HasDelegate)
        {
            await OnLeft.InvokeAsync();
        }

        Navigation.NavigateTo(target);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        navigationRegistration?.Dispose();

        if (module is not null && armedInBrowser)
        {
            try
            {
                await module.InvokeVoidAsync("disarm", instanceId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException
                or TaskCanceledException or ObjectDisposedException)
            {
                // The circuit is already gone; the listener went with it.
            }
        }

        GC.SuppressFinalize(this);
    }
}
