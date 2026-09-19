using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// Groups <see cref="BbChip"/> children and owns their selection, so a row of chips can act as a
/// single-choice or multiple-choice filter.
/// </summary>
/// <typeparam name="TValue">The type of the value each chip carries.</typeparam>
/// <remarks>
/// <para>
/// Every chip that takes part supplies a <see cref="BbChip.Value"/>. The set matches values with
/// <see cref="EqualityComparer{T}.Default"/>, so a record or a primitive works without extra
/// setup and a class needs value equality to match.
/// </para>
/// <para>
/// Selection is controlled when the matching change callback is wired
/// (<c>@bind-Value</c> for <see cref="ChipSelectionMode.Single"/>, <c>@bind-Values</c> for
/// <see cref="ChipSelectionMode.Multiple"/>). Without a binding the set keeps the selection
/// itself and treats <see cref="Value"/> or <see cref="Values"/> as the initial state.
/// </para>
/// <para>
/// The set also supplies the default variant, size, dismiss button and check mark for its chips.
/// A chip that declares its own wins.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BbChipSet TValue="string" SelectionMode="ChipSelectionMode.Multiple" @bind-Values="tags"&gt;
///     &lt;BbChip Value="@("design")"&gt;Design&lt;/BbChip&gt;
///     &lt;BbChip Value="@("build")"&gt;Build&lt;/BbChip&gt;
/// &lt;/BbChipSet&gt;
/// </code>
/// </example>
public partial class BbChipSet<TValue> : ComponentBase
{
    private readonly ChipSetContext setContext = new();
    private TValue? currentValue;
    private List<TValue> currentValues = [];
    private bool initialized;

    /// <summary>
    /// Gets or sets how many chips can be selected at once. Defaults to
    /// <see cref="ChipSelectionMode.Single"/>. Use <see cref="ChipSelectionMode.None"/> for a row
    /// of chips that only displays.
    /// </summary>
    [Parameter]
    public ChipSelectionMode SelectionMode { get; set; } = ChipSelectionMode.Single;

    /// <summary>
    /// Gets or sets the selected value in <see cref="ChipSelectionMode.Single"/> mode.
    /// Supports two-way binding with <c>@bind-Value</c>.
    /// </summary>
    [Parameter]
    public TValue? Value { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the selected value changes.
    /// </summary>
    [Parameter]
    public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>
    /// Gets or sets the selected values in <see cref="ChipSelectionMode.Multiple"/> mode.
    /// Supports two-way binding with <c>@bind-Values</c>.
    /// </summary>
    [Parameter]
    public List<TValue>? Values { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the selected values change. The callback receives a
    /// new list, so the bound field can be replaced rather than mutated.
    /// </summary>
    [Parameter]
    public EventCallback<List<TValue>> ValuesChanged { get; set; }

    /// <summary>
    /// Gets or sets whether the set keeps at least one chip selected. When <c>true</c>, clicking
    /// the last selected chip does nothing. Supply an initial selection.
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the default variant for chips in this set.
    /// </summary>
    [Parameter]
    public ChipVariant Variant { get; set; } = ChipVariant.Default;

    /// <summary>
    /// Gets or sets the default size for chips in this set.
    /// </summary>
    [Parameter]
    public ChipSize Size { get; set; } = ChipSize.Default;

    /// <summary>
    /// Gets or sets whether chips in this set show a dismiss button by default.
    /// </summary>
    [Parameter]
    public bool Dismissible { get; set; }

    /// <summary>
    /// Gets or sets whether selected chips in this set show a check mark by default.
    /// </summary>
    [Parameter]
    public bool ShowCheckMark { get; set; }

    /// <summary>
    /// Gets or sets whether every chip in the set is disabled.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets whether chips wrap onto more lines. When <c>false</c> the set scrolls
    /// horizontally instead. Defaults to <c>true</c>.
    /// </summary>
    [Parameter]
    public bool Wrap { get; set; } = true;

    /// <summary>
    /// Gets or sets the callback invoked when a chip in the set is dismissed. The dismissed value
    /// is also dropped from the selection. Removing the chip from the markup is the consumer's job.
    /// </summary>
    [Parameter]
    public EventCallback<TValue> OnDismiss { get; set; }

    /// <summary>
    /// Gets or sets the chips to render.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes to apply to the set.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets additional HTML attributes to apply to the root element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        setContext.IsSelected = IsSelectedCore;
        setContext.ToggleAsync = ToggleCoreAsync;
        setContext.DismissAsync = DismissCoreAsync;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Controlled while the matching callback is wired; otherwise the incoming parameter is
        // only the initial selection and the set owns it from then on.
        if (!initialized || ValueChanged.HasDelegate)
        {
            currentValue = Value;
        }

        if (!initialized || ValuesChanged.HasDelegate)
        {
            currentValues = Values is null ? [] : [.. Values];
        }

        initialized = true;

        setContext.Variant = Variant;
        setContext.Size = Size;
        setContext.Dismissible = Dismissible;
        setContext.ShowCheckMark = ShowCheckMark;
        setContext.Disabled = Disabled;
        setContext.SelectionMode = SelectionMode;
    }

    // --- Selection ---

    private bool IsSelectedCore(object? value)
    {
        if (SelectionMode == ChipSelectionMode.None || value is not TValue typed)
        {
            return false;
        }

        return SelectionMode == ChipSelectionMode.Single
            ? EqualityComparer<TValue>.Default.Equals(currentValue, typed)
            : currentValues.Contains(typed);
    }

    private async Task ToggleCoreAsync(object? value)
    {
        if (SelectionMode == ChipSelectionMode.None || Disabled || value is not TValue typed)
        {
            return;
        }

        if (SelectionMode == ChipSelectionMode.Single)
        {
            var wasSelected = EqualityComparer<TValue>.Default.Equals(currentValue, typed);

            if (wasSelected && Required)
            {
                return;
            }

            currentValue = wasSelected ? default : typed;

            if (ValueChanged.HasDelegate)
            {
                await ValueChanged.InvokeAsync(currentValue);
            }
        }
        else
        {
            var next = new List<TValue>(currentValues);

            if (next.Contains(typed))
            {
                if (Required && next.Count == 1)
                {
                    return;
                }

                next.Remove(typed);
            }
            else
            {
                next.Add(typed);
            }

            currentValues = next;

            if (ValuesChanged.HasDelegate)
            {
                await ValuesChanged.InvokeAsync(next);
            }
        }

        setContext.NotifyStateChanged();
        StateHasChanged();
    }

    private async Task DismissCoreAsync(object? value)
    {
        if (value is not TValue typed)
        {
            return;
        }

        var changed = false;

        if (SelectionMode == ChipSelectionMode.Single
            && EqualityComparer<TValue>.Default.Equals(currentValue, typed))
        {
            currentValue = default;
            changed = true;

            if (ValueChanged.HasDelegate)
            {
                await ValueChanged.InvokeAsync(currentValue);
            }
        }
        else if (SelectionMode == ChipSelectionMode.Multiple && currentValues.Contains(typed))
        {
            var next = new List<TValue>(currentValues);
            next.Remove(typed);
            currentValues = next;
            changed = true;

            if (ValuesChanged.HasDelegate)
            {
                await ValuesChanged.InvokeAsync(next);
            }
        }

        if (OnDismiss.HasDelegate)
        {
            await OnDismiss.InvokeAsync(typed);
        }

        if (changed)
        {
            setContext.NotifyStateChanged();
            StateHasChanged();
        }
    }

    // --- CSS ---

    private string CssClass => ClassNames.cn(
        "bb:flex bb:items-center bb:gap-2",
        Wrap ? "bb:flex-wrap" : "bb:overflow-x-auto bb:[&>*]:shrink-0",
        Class
    );
}
