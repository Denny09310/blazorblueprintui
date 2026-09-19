using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorBlueprint.Components;

/// <summary>
/// A floating action button: a raised button that pins to a corner of the screen and carries
/// the one main action of that screen.
/// </summary>
/// <remarks>
/// <para>
/// Use one per screen. A second floating button competes with the first and neither reads as the
/// main action; a regular <see cref="BbButton"/> is the right control for everything else.
/// </para>
/// <para>
/// The button is built on <see cref="BbButton"/>, so it works as an <c>AsChild</c> trigger for an
/// overlay. That is how a speed dial is composed: put the FAB inside a
/// <c>BbDropdownMenuTrigger AsChild="true"</c> and the menu opens from the button.
/// </para>
/// <para>
/// Features:
/// - Square with the theme's corner radius by default; <see cref="Shape"/> makes it a circle
/// - Icon only, or widened by a label through <see cref="ChildContent"/>
/// - Five placements, pinned to the viewport or to the nearest positioned ancestor
/// - Safe-area padding so the button clears a phone's home indicator
/// - Logical placement properties, so the corner follows the reading direction
/// </para>
/// <para>
/// An icon-only FAB carries no text, so it needs <see cref="AriaLabel"/> to have an accessible
/// name.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BbFab AriaLabel="New message" OnClick="Compose"&gt;
///     &lt;Icon&gt;&lt;LucideIcon Name="plus" Size="24" /&gt;&lt;/Icon&gt;
/// &lt;/BbFab&gt;
///
/// &lt;BbFab Placement="FabPlacement.BottomStart" OnClick="Compose"&gt;
///     &lt;Icon&gt;&lt;LucideIcon Name="plus" Size="20" /&gt;&lt;/Icon&gt;
///     &lt;ChildContent&gt;New task&lt;/ChildContent&gt;
/// &lt;/BbFab&gt;
/// </code>
/// </example>
public partial class BbFab : ComponentBase
{
    /// <summary>
    /// Gets or sets the visual style variant.
    /// </summary>
    [Parameter]
    public FabVariant Variant { get; set; } = FabVariant.Primary;

    /// <summary>
    /// Gets or sets the button size.
    /// </summary>
    [Parameter]
    public FabSize Size { get; set; } = FabSize.Default;

    /// <summary>
    /// Gets or sets where the button pins itself.
    /// </summary>
    [Parameter]
    public FabPlacement Placement { get; set; } = FabPlacement.BottomEnd;

    /// <summary>
    /// Gets or sets the corner shape. Defaults to <see cref="FabShape.Rounded"/>, which follows
    /// the theme's corner radius. Use <see cref="FabShape.Circle"/> for a full circle, or a pill
    /// once the button carries a label.
    /// </summary>
    [Parameter]
    public FabShape Shape { get; set; } = FabShape.Rounded;

    /// <summary>
    /// Gets or sets whether the button pins to the viewport. When <c>false</c> it pins to the
    /// nearest positioned ancestor instead, which is what a card or a panel needs.
    /// Ignored for <see cref="FabPlacement.Inline"/>.
    /// </summary>
    [Parameter]
    public bool Fixed { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the button keeps clear of the device safe area, so it is not covered
    /// by a home indicator or a notch. Ignored for <see cref="FabPlacement.Inline"/>.
    /// </summary>
    [Parameter]
    public bool SafeArea { get; set; } = true;

    /// <summary>
    /// Gets or sets the icon to render inside the button.
    /// </summary>
    [Parameter]
    public RenderFragment? Icon { get; set; }

    /// <summary>
    /// Gets or sets the label. Supplying one widens the button, so the action can name itself.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets the accessible name. Required for an icon-only button, which has no text for
    /// a screen reader to read.
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// Gets or sets whether the button is disabled.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the button is clicked.
    /// </summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes to apply to the button. They win over the component's
    /// own classes, so this is the way to change the offset from the edge.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets additional HTML attributes to apply to the button element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Whether a label was supplied, which widens the button past its icon.</summary>
    private bool IsExtended => ChildContent is not null;

    private ButtonVariant MappedVariant => Variant switch
    {
        FabVariant.Secondary => ButtonVariant.Secondary,
        FabVariant.Surface => ButtonVariant.Outline,
        FabVariant.Destructive => ButtonVariant.Destructive,
        _ => ButtonVariant.Default
    };

    private string CssClass => ClassNames.cn(
        "bb:shadow-lg bb:transition-shadow bb:duration-200",
        "bb:motion-reduce:transition-none bb:hover:shadow-xl",
        // rounded-md is the theme radius, the same class BbButton uses.
        Shape == FabShape.Circle ? "bb:rounded-full" : "bb:rounded-md",
        (Size, IsExtended) switch
        {
            (FabSize.Small, false) => "bb:h-10 bb:w-10 bb:p-0",
            (FabSize.Small, true) => "bb:h-10 bb:px-4",
            (FabSize.Large, false) => "bb:h-16 bb:w-16 bb:p-0",
            (FabSize.Large, true) => "bb:h-16 bb:px-8 bb:text-base",
            (_, false) => "bb:h-14 bb:w-14 bb:p-0",
            (_, true) => "bb:h-14 bb:px-6"
        },
        PlacementCssClass,
        SafeAreaCssClass,
        Class
    );

    private string? PlacementCssClass => Placement switch
    {
        FabPlacement.Inline => null,
        FabPlacement.BottomStart => ClassNames.cn(AnchorCssClass, "bb:bottom-6 bb:start-6"),
        FabPlacement.BottomCenter => ClassNames.cn(AnchorCssClass, "bb:bottom-6 bb:left-1/2 bb:-translate-x-1/2"),
        FabPlacement.TopEnd => ClassNames.cn(AnchorCssClass, "bb:top-6 bb:end-6"),
        FabPlacement.TopStart => ClassNames.cn(AnchorCssClass, "bb:top-6 bb:start-6"),
        _ => ClassNames.cn(AnchorCssClass, "bb:bottom-6 bb:end-6")
    };

    /// <summary>Sits above a bottom navigation bar, below the overlay layer.</summary>
    private string AnchorCssClass => Fixed ? "bb:fixed bb:z-40" : "bb:absolute bb:z-40";

    private string? SafeAreaCssClass => !SafeArea || Placement == FabPlacement.Inline
        ? null
        : Placement switch
        {
            FabPlacement.TopEnd or FabPlacement.TopStart => "bb:mt-[env(safe-area-inset-top)]",
            _ => "bb:mb-[env(safe-area-inset-bottom)]"
        };
}
