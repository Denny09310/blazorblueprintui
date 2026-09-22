using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Components;

/// <summary>
/// One tab in a <see cref="BbTabsList"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Closable"/>, <see cref="Renamable"/> and <see cref="Reorderable"/> are all
/// <see langword="null"/> by default and fall back to whatever the list says. Set one here to
/// override the list for this tab alone — a pinned tab nobody may close is the usual reason.
/// </para>
/// <para>
/// None of the three writes to anything. Each asks through a callback and the caller changes its
/// own collection, for the same reason the Gantt does: only the caller knows where the order and
/// the names are actually kept.
/// </para>
/// </remarks>
public partial class BbTabsTrigger : IDisposable
{
    private bool isRenaming;
    private string renameText = string.Empty;
    private string renameStartedFrom = string.Empty;
    private bool focusRenameInput;
    private ElementReference renameInput;

    /// <summary>
    /// The value that identifies this tab.
    /// Used to match with TabsContent components.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Display text for this tab.
    /// </summary>
    /// <remarks>
    /// Used for the responsive Select fallback, as the starting text when a rename begins, and as
    /// the tab's own content where no <see cref="ChildContent"/> is given. Falls back to
    /// <see cref="Value"/>. A tab with <see cref="Renamable"/> on wants this set — renaming markup
    /// is not a thing that can be done.
    /// </remarks>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>
    /// Whether this tab trigger is disabled.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Whether this tab can be closed. Null follows the list.
    /// </summary>
    [Parameter]
    public bool? Closable { get; set; }

    /// <summary>
    /// Whether this tab can be renamed in place. Null follows the list.
    /// </summary>
    [Parameter]
    public bool? Renamable { get; set; }

    /// <summary>
    /// Whether this tab can be dragged to a new position. Null follows the list.
    /// </summary>
    [Parameter]
    public bool? Reorderable { get; set; }

    /// <summary>
    /// The child content to render within the tab trigger.
    /// </summary>
    /// <remarks>Leave it unset and the tab shows <see cref="Label"/>.</remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Additional CSS classes to apply to the tab trigger.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets additional HTML attributes to apply to the root element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// The parent Components-layer BbTabsList.
    /// </summary>
    [CascadingParameter]
    public BbTabsList? ComponentsList { get; set; }

    /// <summary>Gets the text the tab shows and a rename starts from.</summary>
    internal string EffectiveLabel => Label ?? Value;

    private bool IsClosable => (Closable ?? ComponentsList?.Closable ?? false) && ComponentsList?.HasCloseHandler == true;

    private bool IsRenamable => (Renamable ?? ComponentsList?.Renamable ?? false) && ComponentsList?.HasRenameHandler == true;

    private bool IsReorderable => (Reorderable ?? ComponentsList?.Reorderable ?? false) && ComponentsList?.HasMoveHandler == true;

    /// <inheritdoc />
    protected override void OnParametersSet() =>
        ComponentsList?.RegisterResponsiveTrigger(Value, EffectiveLabel);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!focusRenameInput)
        {
            return;
        }

        focusRenameInput = false;

        try
        {
            await renameInput.FocusAsync();
            // Select the whole name, because a rename usually replaces it rather than edits it.
            await (ComponentsList?.SelectRenameTextAsync(renameInput) ?? Task.CompletedTask);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
        {
            // Circuit disconnected or the element has already gone, ignore.
        }
        catch (InvalidOperationException)
        {
            // No JavaScript to focus through — prerendering, or a host with no browser at all.
            // The editor is still open and usable; it just does not take focus by itself.
        }
    }

    private string CssClass => ClassNames.cn(
        "bb:inline-flex bb:items-center bb:justify-center bb:whitespace-nowrap bb:rounded-sm bb:px-3 bb:py-1.5",
        "bb:text-sm bb:font-medium bb:ring-offset-background bb:transition-all",
        "bb:focus-visible:outline-none bb:focus-visible:ring-2 bb:focus-visible:ring-ring bb:focus-visible:ring-offset-2",
        "bb:disabled:pointer-events-none bb:disabled:opacity-50",
        "bb:data-[state=active]:bg-background bb:data-[state=active]:text-foreground bb:data-[state=active]:shadow-sm",
        // The close icon sits inside the tab, so the tab needs room for it on the end side only.
        IsClosable ? "bb:gap-1 bb:pe-1.5" : null,
        IsReorderable ? "bb:cursor-grab bb:active:cursor-grabbing" : null,
        Class
    );

    // A span rather than a button: role="tab" must not contain another interactive element.
    // It is aria-hidden because Delete already gives a screen reader the same action, announced
    // on the tab itself rather than as a second stop inside it.
    private static string CloseCssClass => ClassNames.cn(
        "bb:inline-flex bb:size-4 bb:shrink-0 bb:cursor-pointer bb:items-center bb:justify-center",
        "bb:rounded-xs bb:opacity-60 bb:transition-opacity",
        "bb:hover:bg-muted-foreground/20 bb:hover:opacity-100"
    );

    // Sized to match the tab it replaces so the list does not jump when an edit opens.
    private string RenameCssClass => ClassNames.cn(
        "bb:inline-flex bb:h-full bb:rounded-sm bb:bg-background bb:px-3 bb:py-1.5",
        "bb:text-sm bb:font-medium bb:text-foreground bb:shadow-sm",
        "bb:outline-none bb:ring-2 bb:ring-ring",
        // Wide enough for a short sheet name without stretching the list. A longer name scrolls
        // inside the box rather than pushing every other tab sideways while it is being typed.
        "bb:w-32 bb:max-w-full",
        Class
    );

    private async Task HandleDoubleClickAsync()
    {
        if (IsRenamable)
        {
            await BeginRenameAsync();
        }
    }

    private Task BeginRenameAsync()
    {
        if (!IsRenamable || Disabled || isRenaming)
        {
            return Task.CompletedTask;
        }

        isRenaming = true;
        renameStartedFrom = EffectiveLabel;
        renameText = renameStartedFrom;
        focusRenameInput = true;
        StateHasChanged();

        return Task.CompletedTask;
    }

    private async Task HandleRenameKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await CommitRenameAsync();
            return;
        }

        if (args.Key == "Escape")
        {
            // Cleared first so the blur that follows the re-render cannot commit what was typed.
            isRenaming = false;
            renameText = string.Empty;
            StateHasChanged();
        }
    }

    private async Task CommitRenameAsync()
    {
        if (!isRenaming)
        {
            return;
        }

        isRenaming = false;
        var typed = renameText.Trim();
        renameText = string.Empty;

        // An empty box is a cancel, not a request for a nameless tab.
        if (typed.Length == 0 || string.Equals(typed, renameStartedFrom, StringComparison.Ordinal))
        {
            StateHasChanged();
            return;
        }

        var refused = ComponentsList is not null
            && await ComponentsList.RequestRenameAsync(Value, renameStartedFrom, typed);

        if (refused)
        {
            // Reopened with what was typed still in it. Retyping a long name because one character
            // clashed is the kind of small cruelty that makes people stop renaming things.
            isRenaming = true;
            renameText = typed;
            focusRenameInput = true;
        }

        StateHasChanged();
    }

    private Task HandleCloseClickAsync() => RequestCloseAsync();

    private Task RequestCloseAsync() =>
        IsClosable && !Disabled
            ? ComponentsList?.RequestCloseAsync(Value) ?? Task.CompletedTask
            : Task.CompletedTask;

    private Task HandleKeyboardMoveAsync(int delta) =>
        IsReorderable && !Disabled
            ? ComponentsList?.RequestMoveAsync(Value, delta) ?? Task.CompletedTask
            : Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        ComponentsList?.UnregisterResponsiveTrigger(Value);
        GC.SuppressFinalize(this);
    }
}
