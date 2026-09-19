using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// An image that shows something sensible when the source cannot be loaded.
/// </summary>
/// <remarks>
/// <para>
/// A bare <c>&lt;img&gt;</c> with a dead URL leaves a broken-icon box and the alt text, which
/// looks like a bug in the page rather than missing data. This swaps in a fallback: your own
/// content, another URL, or a neutral placeholder icon.
/// </para>
/// <para>
/// The fallback keeps the image's accessible name — it renders with <c>role="img"</c> and the
/// same <see cref="Alt"/> — so a screen reader still describes what should have been there.
/// </para>
/// <para>
/// Images are lazy by default. Turn <see cref="Lazy"/> off for anything above the fold, where
/// lazy loading delays the largest paint rather than helping it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BbImage Src="@user.AvatarUrl" Alt="@user.Name" Fit="ImageFit.Cover" Class="bb:h-24 bb:w-24 bb:rounded-full"&gt;
///     &lt;Fallback&gt;&lt;span class="bb:text-sm bb:font-medium"&gt;@user.Initials&lt;/span&gt;&lt;/Fallback&gt;
/// &lt;/BbImage&gt;
/// </code>
/// </example>
public partial class BbImage : ComponentBase
{
    private bool failed;
    private bool loaded;
    private string? loadedSrc;

    /// <summary>
    /// Gets or sets the image URL.
    /// </summary>
    [Parameter, EditorRequired]
    public string? Src { get; set; }

    /// <summary>
    /// Gets or sets the alternative text. A decorative image should pass an empty string, which
    /// hides it from a screen reader.
    /// </summary>
    [Parameter, EditorRequired]
    public string? Alt { get; set; }

    /// <summary>
    /// Gets or sets the content shown when the image cannot be loaded. Takes precedence over
    /// <see cref="FallbackSrc"/>.
    /// </summary>
    [Parameter]
    public RenderFragment? Fallback { get; set; }

    /// <summary>
    /// Gets or sets a second URL to try when the first fails. A failure of this one is not
    /// retried, so a missing pair cannot loop.
    /// </summary>
    [Parameter]
    public string? FallbackSrc { get; set; }

    /// <summary>
    /// Gets or sets how the image fills its box.
    /// </summary>
    [Parameter]
    public ImageFit Fit { get; set; } = ImageFit.None;

    /// <summary>
    /// Gets or sets whether the browser defers loading until the image is near the viewport.
    /// Defaults to <c>true</c>; turn it off above the fold.
    /// </summary>
    [Parameter]
    public bool Lazy { get; set; } = true;

    /// <summary>
    /// Gets or sets the callback invoked when the image fails to load, so the failure can be
    /// logged or reported rather than only drawn.
    /// </summary>
    [Parameter]
    public EventCallback OnError { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the image finishes loading.
    /// </summary>
    [Parameter]
    public EventCallback OnLoad { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes. They apply to the image and to the fallback, so a
    /// rounded thumbnail stays rounded when it falls back.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets additional HTML attributes to apply to the image element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // A new source deserves a fresh attempt; without this a list that reuses one component
        // instance across rows would keep showing the first row's failure.
        if (!string.Equals(loadedSrc, Src, StringComparison.Ordinal))
        {
            loadedSrc = Src;
            failed = false;
            loaded = false;
        }
    }

    private async Task HandleError()
    {
        failed = true;

        if (OnError.HasDelegate)
        {
            await OnError.InvokeAsync();
        }
    }

    private async Task HandleLoad()
    {
        loaded = true;

        if (OnLoad.HasDelegate)
        {
            await OnLoad.InvokeAsync();
        }
    }

    private string CssClass => ClassNames.cn(
        "bb:block bb:max-w-full",
        Fit switch
        {
            ImageFit.Cover => "bb:object-cover",
            ImageFit.Contain => "bb:object-contain",
            ImageFit.Fill => "bb:object-fill",
            ImageFit.ScaleDown => "bb:object-scale-down",
            _ => null
        },
        Class);

    /// <summary>
    /// The fallback inherits the consumer's classes, so a fixed size or a radius survives the
    /// swap, and centres whatever it is given.
    /// </summary>
    private string FallbackCssClass => ClassNames.cn(
        "bb:flex bb:items-center bb:justify-center bb:bg-muted bb:text-muted-foreground",
        Class);
}
