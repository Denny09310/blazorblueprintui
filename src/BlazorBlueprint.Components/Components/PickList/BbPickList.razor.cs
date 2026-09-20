using System.Linq.Expressions;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace BlazorBlueprint.Components;

/// <summary>
/// Two lists and the buttons that move options between them.
/// </summary>
/// <remarks>
/// <para>
/// Pick rows and press a button to move them, rather than dragging. On a list long enough to need
/// a scrollbar that is the difference between a usable control and a frustrating one — and unlike
/// a drag, it works from the keyboard.
/// </para>
/// <para>
/// There is one binding, not two. <see cref="Options"/> holds every option and
/// <see cref="Values"/> holds the ones picked; the available pane is whatever is left. Two bound
/// collections could drift out of step with each other — the same option in both panes, or in
/// neither — and nothing here can be asked to reconcile that.
/// </para>
/// </remarks>
/// <typeparam name="TValue">The type of the option values.</typeparam>
public partial class BbPickList<TValue> : ComponentBase
{
    private IEnumerable<TValue>? sourceSelection;
    private IEnumerable<TValue>? targetSelection;
    private string? sourceSearch;
    private string? targetSearch;

    private EditContext? editContext;
    private FieldIdentifier fieldIdentifier;

    [CascadingParameter] private EditContext? CascadedEditContext { get; set; }

    /// <summary>
    /// Gets or sets every option, in the order the available pane should show them.
    /// </summary>
    [Parameter]
    public IEnumerable<SelectOption<TValue>>? Options { get; set; }

    /// <summary>
    /// Gets or sets the picked values, in the order the selected pane shows them.
    /// </summary>
    /// <remarks>
    /// Order is kept as moved, not as listed in <see cref="Options"/>, because a pick list is
    /// often building an ordered thing — a set of columns, a playlist, a sequence of steps.
    /// </remarks>
    [Parameter]
    public IEnumerable<TValue>? Values { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the picked values change. Use with
    /// <c>@bind-Values</c>.
    /// </summary>
    [Parameter]
    public EventCallback<IEnumerable<TValue>?> ValuesChanged { get; set; }

    /// <summary>
    /// Gets or sets an expression identifying the bound values, for validation inside an
    /// <c>EditForm</c>.
    /// </summary>
    [Parameter]
    public Expression<Func<IEnumerable<TValue>?>>? ValuesExpression { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked after options move between the panes.
    /// </summary>
    [Parameter]
    public EventCallback<PickListMoveEventArgs<TValue>> OnMove { get; set; }

    /// <summary>
    /// Gets or sets the label above the pane of options not yet picked.
    /// </summary>
    [Parameter]
    public string? SourceLabel { get; set; }

    /// <summary>
    /// Gets or sets the label above the pane of picked options.
    /// </summary>
    [Parameter]
    public string? TargetLabel { get; set; }

    /// <summary>
    /// Gets or sets the message shown when the available pane is empty.
    /// </summary>
    [Parameter]
    public string? SourceEmptyMessage { get; set; }

    /// <summary>
    /// Gets or sets the message shown when the selected pane is empty.
    /// </summary>
    [Parameter]
    public string? TargetEmptyMessage { get; set; }

    /// <summary>
    /// Gets or sets whether each pane has a search box.
    /// </summary>
    [Parameter]
    public bool ShowSearch { get; set; }

    /// <summary>
    /// Gets or sets whether each pane has a select-all checkbox.
    /// </summary>
    [Parameter]
    public bool ShowSelectAll { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the move-everything buttons are shown.
    /// </summary>
    /// <remarks>
    /// With a search active they move what the search has left visible, not the hidden rest. A
    /// button that quietly moved rows the person could not see would be worse than no button.
    /// </remarks>
    [Parameter]
    public bool ShowMoveAll { get; set; } = true;

    /// <summary>
    /// Gets or sets how the two panes are arranged.
    /// </summary>
    [Parameter]
    public PickListOrientation Orientation { get; set; } = PickListOrientation.Horizontal;

    /// <summary>
    /// Gets or sets a predicate marking individual options as unavailable. A disabled option
    /// cannot be picked, and the move-everything buttons leave it where it is.
    /// </summary>
    [Parameter]
    public Func<SelectOption<TValue>, bool>? OptionDisabled { get; set; }

    /// <summary>
    /// Gets or sets a template for an option's content, used in both panes.
    /// </summary>
    [Parameter]
    public RenderFragment<SelectOption<TValue>>? ItemTemplate { get; set; }

    /// <summary>
    /// Gets or sets the height of each pane's scrolling area as a CSS length. Default is
    /// <c>16rem</c>.
    /// </summary>
    [Parameter]
    public string Height { get; set; } = "16rem";

    /// <summary>
    /// Gets or sets whether the whole control is disabled.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes for the root element.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets additional HTML attributes for the root element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsVertical => Orientation == PickListOrientation.Vertical;

    private List<SelectOption<TValue>> allOptions = [];
    private List<TValue> picked = [];

    /// <summary>
    /// The options not yet picked, in <see cref="Options"/> order.
    /// </summary>
    private List<SelectOption<TValue>> SourceOptions { get; set; } = [];

    /// <summary>
    /// The picked options, in the order they were moved.
    /// </summary>
    private List<SelectOption<TValue>> TargetOptions { get; set; } = [];

    private bool HasSourceSelection => sourceSelection?.Any() == true;

    private bool HasTargetSelection => targetSelection?.Any() == true;

    private string EffectiveSourceLabel => SourceLabel ?? Localizer["PickList.Available"];

    private string EffectiveTargetLabel => TargetLabel ?? Localizer["PickList.Selected"];

    private string RootClass => ClassNames.cn(
        "bb:w-full",
        Disabled ? "bb:opacity-60" : null,
        Class);

    // items-start, not items-center: the panes rarely hold the same number of rows, and centring
    // them leaves the two labels at different heights with nothing lining up.
    private string PaneWrapperClass => IsVertical
        ? "bb:flex bb:flex-col bb:gap-3"
        : "bb:flex bb:flex-col bb:gap-3 bb:sm:flex-row bb:sm:items-start";

    private string PaneClass => IsVertical ? "bb:w-full" : "bb:flex-1 bb:min-w-0";

    private string ButtonsClass => IsVertical
        ? "bb:flex bb:flex-row bb:items-center bb:justify-center bb:gap-2"
        // Stacks into a row on a narrow screen, where the panes are stacked too and a column of
        // left/right arrows between them would point the wrong way. The buttons are the one thing
        // that does sit centred, against the taller of the two panes.
        : "bb:flex bb:flex-row bb:sm:flex-col bb:items-center bb:justify-center bb:gap-2 bb:sm:self-center bb:sm:pt-7";

    protected override void OnParametersSet()
    {
        allOptions = [.. Options?.Where(o => o is not null) ?? []];
        var known = allOptions.Select(o => o.Value).ToHashSet();

        // A bound value with no matching option cannot be shown or moved, so it is dropped rather
        // than silently disappearing from the pane while still counting as picked.
        picked = [.. (Values ?? []).Where(known.Contains).Distinct()];

        var pickedSet = picked.ToHashSet();
        SourceOptions = [.. allOptions.Where(o => !pickedSet.Contains(o.Value))];

        // A lookup by value would need TValue constrained to notnull, which would rule out a
        // nullable option value for no good reason. The scan is bounded by the picked count times
        // the option count, and it only runs when the parameters change.
        TargetOptions = new List<SelectOption<TValue>>(picked.Count);
        foreach (var value in picked)
        {
            var match = allOptions.Find(o => EqualityComparer<TValue>.Default.Equals(o.Value, value));
            if (match is not null)
            {
                TargetOptions.Add(match);
            }
        }

        if (CascadedEditContext is not null && ValuesExpression is not null)
        {
            editContext = CascadedEditContext;
            fieldIdentifier = FieldIdentifier.Create(ValuesExpression);
        }
    }

    /// <summary>
    /// What each pane actually has on screen.
    /// </summary>
    /// <remarks>
    /// The pane filters its own copy, so the whole option list is not what the person is looking
    /// at. Move-everything has to act on what they can see, which means applying the same search
    /// here — hence the bound search term and the shared <see cref="ListBoxSearch"/> rule.
    /// </remarks>
    private List<SelectOption<TValue>> VisibleSource =>
        [.. SourceOptions.Where(o => ListBoxSearch.Matches(o.Text, sourceSearch))];

    private List<SelectOption<TValue>> VisibleTarget =>
        [.. TargetOptions.Where(o => ListBoxSearch.Matches(o.Text, targetSearch))];

    private Task MoveSelectedToTargetAsync() => MoveAsync(Chosen(sourceSelection, SourceOptions), toTarget: true);

    private Task MoveSelectedToSourceAsync() => MoveAsync(Chosen(targetSelection, TargetOptions), toTarget: false);

    private Task MoveAllToTargetAsync() => MoveAsync(Movable(VisibleSource), toTarget: true);

    private Task MoveAllToSourceAsync() => MoveAsync(Movable(VisibleTarget), toTarget: false);

    private List<TValue> Chosen(IEnumerable<TValue>? selection, List<SelectOption<TValue>> pane)
    {
        if (selection is null)
        {
            return [];
        }

        var chosen = selection.ToHashSet();
        // Walk the pane rather than the selection, so the moved values keep the pane's order
        // instead of whatever order they happened to be clicked in.
        return [.. pane.Where(o => chosen.Contains(o.Value) && !IsDisabled(o)).Select(o => o.Value)];
    }

    private List<TValue> Movable(List<SelectOption<TValue>> pane) =>
        [.. pane.Where(o => !IsDisabled(o)).Select(o => o.Value)];

    private bool IsDisabled(SelectOption<TValue> option) => OptionDisabled?.Invoke(option) ?? false;

    private async Task MoveAsync(List<TValue> values, bool toTarget)
    {
        if (Disabled || values.Count == 0)
        {
            return;
        }

        if (toTarget)
        {
            picked.AddRange(values.Where(v => !picked.Contains(v)));
        }
        else
        {
            var leaving = values.ToHashSet();
            picked.RemoveAll(leaving.Contains);
        }

        // The moved rows are no longer in the pane they were chosen from, so a selection pointing
        // at them would light up whatever landed in their place.
        sourceSelection = null;
        targetSelection = null;

        Values = picked.Count > 0 ? [.. picked] : null;
        await ValuesChanged.InvokeAsync(Values);

        if (editContext is not null && fieldIdentifier.FieldName is not null)
        {
            editContext.NotifyFieldChanged(fieldIdentifier);
        }

        await OnMove.InvokeAsync(new PickListMoveEventArgs<TValue>(values, toTarget));
    }
}
