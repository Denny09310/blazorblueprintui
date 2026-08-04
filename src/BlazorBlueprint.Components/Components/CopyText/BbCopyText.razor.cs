using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Components;

/// <summary>
/// Displays a short, highlighted piece of text that copies a value
/// to the clipboard when clicked.
/// </summary>
public partial class BbCopyText : ComponentBase, IAsyncDisposable
{
    private IJSObjectReference? clipboardModule;
    private ElementReference anchorRef;
    private readonly string portalId = $"copytext-portal-{Guid.NewGuid():N}";
    private bool isHovered;
    private bool copied;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Inject]
    private IBbLocalizer Localizer { get; set; } = default!;

    /// <summary>
    /// Sets the value to be copied to the clipboard when clicked.
    /// </summary>
    [Parameter, EditorRequired]
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets the content displayed inside the copy text element.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Additional CSS classes to apply to the text.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets the ARIA label for the copy button.
    /// Defaults to the localized "Click to copy" text.
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// Callback invoked when the user clicks the component to copy text.
    /// </summary>
    [Parameter]
    public EventCallback<string?> OnCopied { get; set; }

    /// <summary>
    /// Gets or sets additional HTML attributes to apply to the container.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string CurrentIconName => copied ? "check" : "copy";

    private string CurrentTooltipText => copied ? Localizer["CopyText.Copied"] : Localizer["CopyText.ClickToCopy"];

    private string TooltipIconCssClass => copied && isHovered ? "h-3 w-3 text-alert-success" : "h-3 w-3 text-primary";

    private string TooltipTextCssClass => copied ? "text-alert-success" : "text-foreground";

    private string? TextCssClass => ClassNames.cn(
        "relative inline-flex gap-1 items-center cursor-pointer text-primary font-semibold",
        Class);

    // Positioning, offset and z-index now come from the floating portal, so only the visual
    // chrome is left here. The opacity/translate pair went with it: the portal mounts the
    // tooltip when it opens rather than keeping a transparent copy in the layout. With no
    // state left to vary on, this is a constant rather than a computed class string.
    private const string TooltipCssClass =
        "pointer-events-none inline-flex items-center gap-1.5 whitespace-nowrap " +
        "rounded-md border bg-popover px-2.5 py-1 text-xs font-medium shadow-md";

    private void HandleMouseEnter()
    {
        ShowTooltip();
    }

    private void HandleMouseLeave()
    {
        isHovered = false;
    }

    private void HandleFocus()
    {
        ShowTooltip();
    }

    private void HandleBlur()
    {
        isHovered = false;
    }

    private void ShowTooltip()
    {
        isHovered = true;

        if (copied)
        {
            copied = false;
        }
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " ")
        {
            await HandleClickAsync();
        }
    }

    private async Task HandleClickAsync()
    {
        if (string.IsNullOrEmpty(Value))
        {
            return;
        }

        var success = await CopyToClipboardAsync(Value);
        if (!success)
        {
            return;
        }

        copied = true;

        if (OnCopied.HasDelegate)
        {
            await OnCopied.InvokeAsync(Value);
        }
    }

    private async Task<bool> CopyToClipboardAsync(string text)
    {
        try
        {
            clipboardModule ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/BlazorBlueprint.Components/js/clipboard.js");
            return await clipboardModule.InvokeAsync<bool>("copyToClipboard", text);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (clipboardModule is not null)
        {
            try
            {
                await clipboardModule.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // Circuit already gone; nothing to clean up.
            }
        }

        GC.SuppressFinalize(this);
    }
}
