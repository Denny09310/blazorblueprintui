using BlazorBlueprint.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Components;

/// <summary>
/// A floating button that appears once the page has been scrolled, and returns it to the top.
/// </summary>
/// <remarks>
/// <para>
/// It renders nothing until the scroll passes <see cref="VisibleAt"/>, so it costs no space on a
/// short page and never covers content there is no reason to scroll away from.
/// </para>
/// <para>
/// By default it watches the document. Pass a <see cref="Selector"/> to watch a scrolling panel
/// instead — a dialog body, a data grid's viewport — and the button scrolls that element.
/// </para>
/// <para>
/// It is built on <see cref="BbFab"/>, so placement, shape, size and safe-area handling behave
/// the same way, and a right-to-left page puts it in the mirrored corner.
/// </para>
/// <para>
/// The scroll itself is smooth unless the visitor asks for reduced motion, which the browser
/// reports and this honours.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BbScrollToTop /&gt;
///
/// &lt;BbScrollToTop Selector="#main-content" VisibleAt="600" /&gt;
/// </code>
/// </example>
public partial class BbScrollToTop : ComponentBase, IAsyncDisposable
{
    private readonly string instanceId = $"bb-scroll-to-top-{Guid.NewGuid():N}";
    private DotNetObjectReference<BbScrollToTop>? selfRef;
    private IJSObjectReference? module;
    private bool visible;
    private bool observed;
    private string? observedSelector;
    private int observedThreshold;

    /// <summary>
    /// Gets or sets a CSS selector for the scrolling element to watch. When not set, the document
    /// is watched.
    /// </summary>
    [Parameter]
    public string? Selector { get; set; }

    /// <summary>
    /// Gets or sets how far the container must be scrolled, in pixels, before the button appears.
    /// </summary>
    [Parameter]
    public int VisibleAt { get; set; } = 300;

    /// <summary>
    /// Gets or sets whether the scroll is animated. Ignored when the visitor has asked for
    /// reduced motion.
    /// </summary>
    [Parameter]
    public bool Smooth { get; set; } = true;

    /// <summary>
    /// Gets or sets the button's visual style variant.
    /// </summary>
    [Parameter]
    public FabVariant Variant { get; set; } = FabVariant.Surface;

    /// <summary>
    /// Gets or sets the button size.
    /// </summary>
    [Parameter]
    public FabSize Size { get; set; } = FabSize.Small;

    /// <summary>
    /// Gets or sets the button's corner shape.
    /// </summary>
    [Parameter]
    public FabShape Shape { get; set; } = FabShape.Rounded;

    /// <summary>
    /// Gets or sets where the button pins itself.
    /// </summary>
    [Parameter]
    public FabPlacement Placement { get; set; } = FabPlacement.BottomEnd;

    /// <summary>
    /// Gets or sets whether the button pins to the viewport rather than to the nearest positioned
    /// ancestor.
    /// </summary>
    [Parameter]
    public bool Fixed { get; set; } = true;

    /// <summary>
    /// Gets or sets the accessible name. Defaults to the localized <c>ScrollToTop.Label</c>.
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// Gets or sets the icon. Defaults to an upward arrow.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked after the container is scrolled back to the top.
    /// </summary>
    [Parameter]
    public EventCallback OnScrolledToTop { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes for the button.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets additional HTML attributes for the button.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Inject]
    private IBbLocalizer Localizer { get; set; } = default!;

    private string? CssClass => ClassNames.cn(Class);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (observed && observedSelector == Selector && observedThreshold == VisibleAt)
        {
            return;
        }


        try
        {
            module ??= await JsModules.GetAsync(
                JS, "./_content/BlazorBlueprint.Components/js/scroll-to-top.js");
            selfRef ??= DotNetObjectReference.Create(this);
            await module.InvokeAsync<bool>("observe", instanceId, Selector, VisibleAt, selfRef);
            observed = true;
            observedSelector = Selector;
            observedThreshold = VisibleAt;
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException
            or TaskCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // Prerendering, or a circuit that went away. The button simply stays hidden.
        }
    }

    /// <summary>
    /// Called from JavaScript when the watched container crosses the threshold.
    /// </summary>
    /// <param name="isVisible">Whether the container is scrolled past the threshold.</param>
    [JSInvokable]
    public Task JsSetVisible(bool isVisible)
    {
        if (visible == isVisible)
        {
            return Task.CompletedTask;
        }

        visible = isVisible;
        return InvokeAsync(StateHasChanged);
    }

    private async Task ScrollAsync()
    {
        if (module is null)
        {
            return;
        }

        try
        {
            if (!await module.InvokeAsync<bool>("scrollToTop", Selector, Smooth))
        {
            return;
        }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException
            or TaskCanceledException or ObjectDisposedException)
        {
            return;
        }

        if (OnScrolledToTop.HasDelegate)
        {
            await OnScrolledToTop.InvokeAsync();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", instanceId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException
                or TaskCanceledException or ObjectDisposedException)
            {
                // The circuit is already gone; the listener went with it.
            }
        }

        selfRef?.Dispose();
        GC.SuppressFinalize(this);
    }
}
