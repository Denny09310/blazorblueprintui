using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorBlueprint.Components;

/// <summary>
/// A compact pill that represents an attribute, a filter or a choice, and can be selected,
/// dismissed, or both.
/// </summary>
/// <remarks>
/// <para>
/// A chip differs from a <see cref="BbBadge"/> in that it is interactive: it carries a selected
/// state, a dismiss button, or a click callback. A badge only labels.
/// </para>
/// <para>
/// Features:
/// - Five variants and three sizes, each with a selected treatment
/// - Optional dismiss button with a localized accessible name
/// - Optional check mark on the selected state
/// - Selection driven locally through <c>@bind-Selected</c>, or by a parent
///   <see cref="BbChipSet{TValue}"/> through <see cref="Value"/>
/// - Renders as a plain span when it is not interactive, so a static chip is not announced
///   as a button
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BbChip Dismissible="true" OnDismiss="Remove"&gt;Design&lt;/BbChip&gt;
///
/// &lt;BbChip Selectable="true" @bind-Selected="showArchived"&gt;Archived&lt;/BbChip&gt;
/// </code>
/// </example>
public partial class BbChip : ComponentBase, IDisposable
{
    private bool? localSelected;

    /// <summary>
    /// Gets or sets the visual style variant. Falls back to the parent
    /// <see cref="BbChipSet{TValue}"/>, then to <see cref="ChipVariant.Default"/>.
    /// </summary>
    [Parameter]
    public ChipVariant? Variant { get; set; }

    /// <summary>
    /// Gets or sets the chip size. Falls back to the parent <see cref="BbChipSet{TValue}"/>,
    /// then to <see cref="ChipSize.Default"/>.
    /// </summary>
    [Parameter]
    public ChipSize? Size { get; set; }

    /// <summary>
    /// Gets or sets the content of the chip, normally a short label.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets leading content, normally an icon or an avatar.
    /// </summary>
    [Parameter]
    public RenderFragment? Icon { get; set; }

    /// <summary>
    /// Gets or sets the value this chip contributes to a parent <see cref="BbChipSet{TValue}"/>.
    /// A chip inside a selecting set needs a value to take part in the selection.
    /// </summary>
    [Parameter]
    public object? Value { get; set; }

    /// <summary>
    /// Gets or sets whether the chip toggles its own selected state when clicked.
    /// Not needed inside a <see cref="BbChipSet{TValue}"/>, which drives selection itself.
    /// </summary>
    [Parameter]
    public bool Selectable { get; set; }

    /// <summary>
    /// Gets or sets whether the chip is selected. Supports two-way binding with
    /// <c>@bind-Selected</c>. Ignored while a parent <see cref="BbChipSet{TValue}"/> owns the
    /// selection.
    /// </summary>
    [Parameter]
    public bool Selected { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the chip's own selected state changes.
    /// Use with <c>@bind-Selected</c>.
    /// </summary>
    [Parameter]
    public EventCallback<bool> SelectedChanged { get; set; }

    /// <summary>
    /// Gets or sets whether a check mark is shown while the chip is selected. Falls back to the
    /// parent <see cref="BbChipSet{TValue}"/>, then to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// The check mark widens the chip as it appears, which reflows a wrapped row of chips.
    /// Reserve it for sets where the selected state needs to survive a colour-blind reading.
    /// </remarks>
    [Parameter]
    public bool? ShowCheckMark { get; set; }

    /// <summary>
    /// Gets or sets whether the chip shows a dismiss button. Falls back to the parent
    /// <see cref="BbChipSet{TValue}"/>, then to <c>false</c>.
    /// </summary>
    [Parameter]
    public bool? Dismissible { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the dismiss button is activated. Removing the chip
    /// from the markup is the consumer's job. Inside a <see cref="BbChipSet{TValue}"/> the set's
    /// own dismiss callback also runs.
    /// </summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnDismiss { get; set; }

    /// <summary>
    /// Gets or sets the accessible name of the dismiss button.
    /// Defaults to the localized <c>Chip.Dismiss</c> string.
    /// </summary>
    [Parameter]
    public string? DismissLabel { get; set; }

    /// <summary>
    /// Gets or sets whether the chip is disabled. A disabled chip cannot be selected or dismissed.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the chip body is clicked. Runs after any selection
    /// change, and makes the chip interactive on its own.
    /// </summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes to apply to the chip.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets additional HTML attributes to apply to the root element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    [CascadingParameter]
    private ChipSetContext? SetContext { get; set; }

    // --- Effective state ---

    private ChipVariant EffectiveVariant => Variant ?? SetContext?.Variant ?? ChipVariant.Default;

    private ChipSize EffectiveSize => Size ?? SetContext?.Size ?? ChipSize.Default;

    private bool EffectiveDismissible => Dismissible ?? SetContext?.Dismissible ?? false;

    private bool EffectiveShowCheckMark => ShowCheckMark ?? SetContext?.ShowCheckMark ?? false;

    private bool IsDisabled => Disabled || (SetContext?.Disabled ?? false);

    private string EffectiveDismissLabel => DismissLabel ?? Localizer["Chip.Dismiss"];

    /// <summary>Whether a parent set owns this chip's selected state.</summary>
    private bool IsSetDriven =>
        SetContext is { SelectionMode: not ChipSelectionMode.None } && Value is not null;

    private bool IsSelectable => IsSetDriven || Selectable || SelectedChanged.HasDelegate;

    private bool IsInteractive => IsSelectable || OnClick.HasDelegate;

    private bool IsSelected => IsSetDriven
        ? SetContext!.IsSelected(Value)
        : SelectedChanged.HasDelegate ? Selected : localSelected ?? Selected;

    private string StateAttribute => IsSelected ? "on" : "off";

    private string? PressedAttribute => IsSelectable ? (IsSelected ? "true" : "false") : null;

    // --- Lifecycle ---

    /// <inheritdoc />
    protected override void OnInitialized() => SetContext?.Register(this);

    /// <summary>
    /// Re-renders the chip after the parent set changed the selection. The cascaded context is
    /// fixed, so the set notifies its chips instead of the framework doing it.
    /// </summary>
    internal void NotifySetStateChanged() => StateHasChanged();

    /// <inheritdoc />
    public void Dispose()
    {
        SetContext?.Unregister(this);
        GC.SuppressFinalize(this);
    }

    // --- Events ---

    private async Task HandleActivateAsync(MouseEventArgs args)
    {
        if (IsDisabled)
        {
            return;
        }

        if (IsSetDriven)
        {
            await SetContext!.ToggleAsync(Value);
        }
        else if (IsSelectable)
        {
            var next = !IsSelected;
            localSelected = next;

            if (SelectedChanged.HasDelegate)
            {
                await SelectedChanged.InvokeAsync(next);
            }
        }

        if (OnClick.HasDelegate)
        {
            await OnClick.InvokeAsync(args);
        }
    }

    private async Task HandleDismissAsync(MouseEventArgs args)
    {
        if (IsDisabled)
        {
            return;
        }

        if (OnDismiss.HasDelegate)
        {
            await OnDismiss.InvokeAsync(args);
        }

        if (SetContext is not null && Value is not null)
        {
            await SetContext.DismissAsync(Value);
        }
    }

    // --- CSS ---

    private string RootCssClass => ClassNames.cn(
        "bb:inline-flex bb:max-w-full bb:items-center bb:rounded-full bb:border bb:align-middle",
        "bb:font-medium bb:transition-colors",
        EffectiveSize switch
        {
            ChipSize.Small => "bb:h-6 bb:text-xs",
            ChipSize.Large => "bb:h-10 bb:text-sm",
            _ => "bb:h-8 bb:text-sm"
        },
        VariantCssClass,
        HoverCssClass,
        IsDisabled ? "bb:opacity-50" : null,
        Class
    );

    private string VariantCssClass => (EffectiveVariant, IsSelected) switch
    {
        (ChipVariant.Default, true) => "bb:border-transparent bb:bg-primary bb:text-primary-foreground",
        (ChipVariant.Default, false) => "bb:border-transparent bb:bg-secondary bb:text-secondary-foreground",
        (ChipVariant.Secondary, true) => "bb:border-primary/30 bb:bg-primary/15 bb:text-primary",
        (ChipVariant.Secondary, false) => "bb:border-transparent bb:bg-muted bb:text-muted-foreground",
        (ChipVariant.Outline, true) => "bb:border-primary bb:bg-accent bb:text-accent-foreground",
        (ChipVariant.Outline, false) => "bb:border-input bb:bg-transparent bb:text-foreground",
        (ChipVariant.Destructive, true) => "bb:border-transparent bb:bg-destructive bb:text-destructive-foreground",
        (ChipVariant.Destructive, false) => "bb:border-transparent bb:bg-destructive/10 bb:text-destructive",
        (ChipVariant.Soft, true) => "bb:border-transparent bb:bg-primary bb:text-primary-foreground",
        (ChipVariant.Soft, false) => "bb:border-transparent bb:bg-primary/10 bb:text-primary",
        _ => "bb:border-transparent bb:bg-secondary bb:text-secondary-foreground"
    };

    private string? HoverCssClass => !IsInteractive || IsDisabled
        ? null
        : (EffectiveVariant, IsSelected) switch
        {
            (ChipVariant.Default, true) => "bb:hover:bg-primary/90",
            (ChipVariant.Default, false) => "bb:hover:bg-secondary/80",
            (ChipVariant.Secondary, true) => "bb:hover:bg-primary/25",
            (ChipVariant.Secondary, false) => "bb:hover:bg-muted/70",
            (ChipVariant.Outline, true) => "bb:hover:bg-accent/80",
            (ChipVariant.Outline, false) => "bb:hover:bg-accent bb:hover:text-accent-foreground",
            (ChipVariant.Destructive, true) => "bb:hover:bg-destructive/90",
            (ChipVariant.Destructive, false) => "bb:hover:bg-destructive/20",
            (ChipVariant.Soft, true) => "bb:hover:bg-primary/90",
            (ChipVariant.Soft, false) => "bb:hover:bg-primary/20",
            _ => null
        };

    private string BodyCssClass => ClassNames.cn(
        "bb:inline-flex bb:min-w-0 bb:items-center bb:gap-1.5 bb:self-stretch bb:rounded-full",
        "bb:outline-none bb:focus-visible:ring-2 bb:focus-visible:ring-ring bb:focus-visible:ring-offset-2",
        "bb:focus-visible:ring-offset-background",
        (EffectiveSize, EffectiveDismissible) switch
        {
            (ChipSize.Small, true) => "bb:ps-2 bb:pe-1",
            (ChipSize.Small, false) => "bb:px-2",
            (ChipSize.Large, true) => "bb:ps-4 bb:pe-2",
            (ChipSize.Large, false) => "bb:px-4",
            (_, true) => "bb:ps-3 bb:pe-1.5",
            (_, false) => "bb:px-3"
        },
        IsInteractive && !IsDisabled ? "bb:cursor-pointer" : null
    );

    private string DismissCssClass => ClassNames.cn(
        "bb:inline-flex bb:shrink-0 bb:items-center bb:justify-center bb:rounded-full bb:transition-colors",
        "bb:outline-none bb:focus-visible:ring-2 bb:focus-visible:ring-ring",
        EffectiveSize switch
        {
            ChipSize.Small => "bb:me-1 bb:h-4 bb:w-4",
            ChipSize.Large => "bb:me-2 bb:h-6 bb:w-6",
            _ => "bb:me-1.5 bb:h-5 bb:w-5"
        },
        IsDisabled ? null : "bb:cursor-pointer bb:hover:bg-foreground/10"
    );

    private string IconCssClass => EffectiveSize switch
    {
        ChipSize.Small => "bb:h-3 bb:w-3",
        ChipSize.Large => "bb:h-4 bb:w-4",
        _ => "bb:h-3.5 bb:w-3.5"
    };

    private string CheckMarkCssClass => ClassNames.cn("bb:shrink-0", IconCssClass);

    private string DismissIconCssClass => IconCssClass;
}
