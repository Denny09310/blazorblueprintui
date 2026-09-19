using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorBlueprint.Components;

/// <summary>
/// A styled anchor for a link inside text.
/// </summary>
/// <remarks>
/// <para>
/// This is the inline counterpart to <see cref="BbButton"/>. A button with
/// <see cref="ButtonVariant.Link"/> looks like a link but keeps a button's height and padding, so
/// it breaks the rhythm of a paragraph; this renders a bare anchor that sits on the text baseline.
/// Use the button variant for an action, and this for navigation.
/// </para>
/// <para>
/// Features:
/// - Four colour treatments, including one that inherits the surrounding text colour
/// - Underline always, on hover, or never
/// - A focus ring that follows the text across a line break
/// - Automatic <c>rel="noopener noreferrer"</c> for a link that opens in a new tab
/// - An optional external-link icon with a screen-reader note
/// </para>
/// <para>
/// An underline is the only cue that survives a colour-blind reading, so
/// <see cref="LinkUnderline.None"/> is worth avoiding in prose.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// Read the &lt;BbLink Href="/guides/rtl"&gt;right-to-left guide&lt;/BbLink&gt; first.
///
/// &lt;BbLink Href="https://example.com" Target="_blank" ShowExternalIcon="true"&gt;Example&lt;/BbLink&gt;
/// </code>
/// </example>
public partial class BbLink : ComponentBase
{
    /// <summary>
    /// Gets or sets the link target URL.
    /// </summary>
    [Parameter, EditorRequired]
    public string? Href { get; set; }

    /// <summary>
    /// Gets or sets the anchor's <c>target</c>, such as <c>_blank</c>.
    /// </summary>
    [Parameter]
    public string? Target { get; set; }

    /// <summary>
    /// Gets or sets the anchor's <c>rel</c>. When not set and <see cref="Target"/> is
    /// <c>_blank</c>, it defaults to <c>noopener noreferrer</c>, which stops the opened page
    /// reaching back into this one.
    /// </summary>
    [Parameter]
    public string? Rel { get; set; }

    /// <summary>
    /// Gets or sets the colour treatment.
    /// </summary>
    [Parameter]
    public LinkVariant Variant { get; set; } = LinkVariant.Default;

    /// <summary>
    /// Gets or sets when the underline is drawn.
    /// </summary>
    [Parameter]
    public LinkUnderline Underline { get; set; } = LinkUnderline.Hover;

    /// <summary>
    /// Gets or sets whether an external-link icon follows the text. It carries a screen-reader
    /// note, so the destination is announced rather than only drawn.
    /// </summary>
    [Parameter]
    public bool ShowExternalIcon { get; set; }

    /// <summary>
    /// Gets or sets whether the link is disabled. A disabled link drops its <c>href</c>, so it is
    /// neither followed nor reachable by keyboard.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the link is clicked.
    /// </summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>
    /// Gets or sets the link text.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes to apply to the anchor.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets additional HTML attributes to apply to the anchor.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject]
    private IBbLocalizer Localizer { get; set; } = default!;

    /// <summary>
    /// A link that opens elsewhere gets <c>noopener noreferrer</c> unless the consumer set its
    /// own <c>rel</c>.
    /// </summary>
    private string? EffectiveRel => Rel ?? (Target == "_blank" ? "noopener noreferrer" : null);

    private async Task HandleClickAsync(MouseEventArgs args)
    {
        if (Disabled)
        {
            return;
        }

        if (OnClick.HasDelegate)
        {
            await OnClick.InvokeAsync(args);
        }
    }

    private string CssClass => ClassNames.cn(
        // decoration-from-font keeps the underline clear of a descender; the ring is drawn round
        // the text rather than a box, so a link that wraps mid-sentence still reads as one link.
        "bb:inline bb:items-baseline bb:gap-1 bb:rounded-sm bb:underline-offset-4",
        "bb:decoration-from-font bb:transition-colors",
        "bb:outline-none bb:focus-visible:ring-2 bb:focus-visible:ring-ring bb:focus-visible:ring-offset-2",
        Variant switch
        {
            LinkVariant.Inherit => "bb:text-inherit",
            LinkVariant.Muted => "bb:text-muted-foreground bb:hover:text-foreground",
            LinkVariant.Destructive => "bb:text-destructive bb:hover:text-destructive/80",
            _ => "bb:text-primary bb:hover:text-primary/80"
        },
        Underline switch
        {
            LinkUnderline.Always => "bb:underline",
            LinkUnderline.None => "bb:no-underline",
            _ => "bb:no-underline bb:hover:underline bb:focus-visible:underline"
        },
        Disabled ? "bb:pointer-events-none bb:cursor-not-allowed bb:opacity-50" : "bb:cursor-pointer",
        Class
    );
}
