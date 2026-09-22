using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using BlazorBlueprint.Primitives.Services;

namespace BlazorBlueprint.Components;

/// <summary>
/// Shows where a person is in a sequence of steps, and optionally the content of the active step.
/// Compose it with <see cref="BbStep"/> children.
/// </summary>
/// <remarks>
/// <para>
/// The stepper is a plain progress indicator: it holds no form, validates nothing, and renders no
/// navigation buttons. Drive it from your own controls through <c>@bind-ActiveStep</c>, or let a
/// person move through it by setting <see cref="Clickable"/>. For a multi-step form with per-step
/// validation and built-in Back and Next buttons, use <see cref="BbFormWizard"/> instead.
/// </para>
/// <para>
/// Features:
/// - Horizontal and vertical layouts
/// - Derived step state, with a per-step override for an error or a skipped step
/// - Optional step content, rendered for the active step only
/// - Step position and state announced to a screen reader
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BbStepper @bind-ActiveStep="step" Clickable="true"&gt;
///     &lt;BbStep Title="Cart" /&gt;
///     &lt;BbStep Title="Address" /&gt;
///     &lt;BbStep Title="Payment" /&gt;
/// &lt;/BbStepper&gt;
/// </code>
/// </example>
public partial class BbStepper : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    private ElementReference root;
    private IJSObjectReference? orderModule;
    private DotNetObjectReference<BbStepper>? selfRef;
    private bool disposed;

    /// <summary>Synchronizes step indicators with the current child order in the DOM.</summary>
    [JSInvokable]
    public Task SynchronizeOrder(string[] ids) => InvokeAsync(() =>
    {
        if (disposed)
        {
            return;
        }
        var owners = stepOwners.Keys.ToDictionary(owner => owner.StepId, StringComparer.Ordinal);
        if (ids.Length != owners.Count || ids.Distinct(StringComparer.Ordinal).Count() != owners.Count
            || ids.Any(id => !owners.ContainsKey(id)))
        {
            return;
        }
        var ordered = ids.Select(id => owners[id]).ToList();
        if (ordered.Select((owner, index) => stepOwners[owner] == index).All(same => same))
        {
            return;
        }
        var infos = ordered.Select(owner => steps[stepOwners[owner]]).ToArray();
        steps.Clear();
        steps.AddRange(infos);
        for (var i = 0; i < ordered.Count; i++)
        {
            stepOwners[ordered[i]] = i;
            ordered[i].SetIndex(i);
        }
        StateHasChanged();
    });

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }
        try
        {
            orderModule = await JsModules.GetAsync(JSRuntime, "./_content/BlazorBlueprint.Components/js/stepper.js");
            selfRef = DotNetObjectReference.Create(this);
            await orderModule.InvokeVoidAsync("initialize", root, selfRef);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or InvalidOperationException or TaskCanceledException or ObjectDisposedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        if (orderModule is not null)
        {
            try { await orderModule.InvokeVoidAsync("dispose", root); }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException) { }
        }
        selfRef?.Dispose();
        GC.SuppressFinalize(this);
    }

    // --- Registration (instance-keyed, so a re-render does not duplicate a step) ---
    private readonly List<StepInfo> steps = [];
    private readonly Dictionary<BbStep, int> stepOwners = [];

    // --- State ---
    private int activeStep;
    private bool initialized;
    private int lastRenderedStepCount = -1;

    /// <summary>
    /// Gets or sets the zero-based index of the active step. Supports two-way binding with
    /// <c>@bind-ActiveStep</c>. Without a binding this is the initial step and the component owns
    /// the value from then on.
    /// </summary>
    [Parameter]
    public int ActiveStep { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the active step changes.
    /// </summary>
    [Parameter]
    public EventCallback<int> ActiveStepChanged { get; set; }

    /// <summary>
    /// Gets or sets the layout of the indicator.
    /// </summary>
    [Parameter]
    public StepperOrientation Orientation { get; set; } = StepperOrientation.Horizontal;

    /// <summary>
    /// Gets or sets whether a step can be activated by clicking it. Defaults to <c>false</c>,
    /// which renders the indicator as static text.
    /// </summary>
    [Parameter]
    public bool Clickable { get; set; }

    /// <summary>
    /// Gets or sets the steps and their content.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes for the root element.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes for the step indicator list.
    /// </summary>
    [Parameter]
    public string? IndicatorClass { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes for the step content area.
    /// </summary>
    [Parameter]
    public string? ContentClass { get; set; }

    /// <summary>
    /// Gets or sets additional HTML attributes to apply to the root element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // --- Internal API, used by BbStep through its cascaded parameter ---

    /// <summary>Gets the zero-based index of the active step.</summary>
    internal int ActiveStepIndex => EffectiveActiveStep;

    /// <summary>
    /// The active index held inside the registered range.
    /// </summary>
    /// <remarks>
    /// Steps register while the child content renders, which is after <c>OnParametersSet</c> has
    /// already run and after the indicator above them has rendered. So an index cannot be clamped
    /// when it arrives — at that moment the step count is still zero — and it is clamped on every
    /// read instead.
    /// </remarks>
    private int EffectiveActiveStep => steps.Count == 0
        ? Math.Max(activeStep, 0)
        : Math.Clamp(activeStep, 0, steps.Count - 1);

    /// <summary>Gets the number of registered steps.</summary>
    internal int StepCount => steps.Count;

    /// <summary>
    /// Registers a step and returns its index. Keyed on the component instance, because
    /// <c>OnParametersSet</c> can run more than once per render cycle.
    /// </summary>
    internal int RegisterStep(BbStep owner, StepInfo info)
    {
        if (stepOwners.TryGetValue(owner, out var existing))
        {
            steps[existing] = info;
            return existing;
        }

        var index = steps.Count;
        steps.Add(info);
        stepOwners[owner] = index;
        return index;
    }

    /// <summary>Unregisters a disposed step and keeps the remaining indices contiguous.</summary>
    internal void UnregisterStep(BbStep owner)
    {
        if (!stepOwners.TryGetValue(owner, out var removed))
        {
            return;
        }

        steps.RemoveAt(removed);
        stepOwners.Remove(owner);

        foreach (var pair in stepOwners.Where(pair => pair.Value > removed).ToList())
        {
            stepOwners[pair.Key] = pair.Value - 1;
        }

    }

    /// <summary>Gets the visual state of the step at the given index.</summary>
    internal StepState GetStepState(int index)
    {
        if (index >= 0 && index < steps.Count && steps[index].State is { } explicitState)
        {
            return explicitState;
        }

        var active = EffectiveActiveStep;

        if (index == active)
        {
            return StepState.Active;
        }

        return index < active ? StepState.Completed : StepState.Pending;
    }

    /// <summary>Whether the step at the given index can be activated by a click.</summary>
    internal bool CanNavigateToStep(int index) =>
        Clickable
        && index >= 0
        && index < steps.Count
        && index != EffectiveActiveStep
        && !steps[index].Disabled;

    /// <summary>Activates the step at the given index, if it is navigable.</summary>
    internal async Task GoToStepAsync(int index)
    {
        if (!CanNavigateToStep(index))
        {
            return;
        }

        activeStep = index;

        if (ActiveStepChanged.HasDelegate)
        {
            await ActiveStepChanged.InvokeAsync(activeStep);
        }

        StateHasChanged();
    }

    // --- Lifecycle ---

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!initialized || ActiveStepChanged.HasDelegate)
        {
            activeStep = ActiveStep;
            initialized = true;
        }
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        // Steps register while the child content renders, which is after the indicator above it.
        // One more render brings the indicator in line with the steps that exist.
        if (steps.Count != lastRenderedStepCount)
        {
            lastRenderedStepCount = steps.Count;
            StateHasChanged();
        }
    }

    // --- Accessibility ---

    private string StepAnnouncement(int index, StepState state)
    {
        var position = Localizer["Stepper.StepPosition", index + 1, steps.Count];

        var word = state switch
        {
            StepState.Completed => Localizer["Stepper.Completed"],
            StepState.Error => Localizer["Stepper.Error"],
            StepState.Skipped => Localizer["Stepper.Skipped"],
            _ => null
        };

        return word is null ? position : position + ", " + word;
    }

    // --- CSS ---

    private bool IsVertical => Orientation == StepperOrientation.Vertical;

    private bool HasStepContent => steps.Exists(step => step.HasContent);

    private string CssClass => ClassNames.cn(
        IsVertical ? "bb:flex bb:gap-8" : "bb:flex bb:flex-col",
        Class
    );

    private string IndicatorCssClass => ClassNames.cn(
        "bb:m-0 bb:list-none bb:p-0",
        IsVertical ? "bb:flex bb:shrink-0 bb:flex-col" : "bb:flex bb:w-full bb:items-start",
        IndicatorClass
    );

    private string ItemCssClass(bool isLast) => ClassNames.cn(
        "bb:flex",
        IsVertical ? "bb:flex-col" : "bb:items-start",
        !IsVertical && !isLast ? "bb:flex-1" : null
    );

    private string MarkerStaticCssClass => ClassNames.cn(
        "bb:flex",
        IsVertical ? "bb:flex-row bb:items-center bb:gap-3" : "bb:shrink-0 bb:flex-col bb:items-center bb:gap-2"
    );

    private string MarkerButtonCssClass => ClassNames.cn(
        MarkerStaticCssClass,
        "bb:cursor-pointer bb:rounded-md bb:outline-none",
        "bb:focus-visible:ring-2 bb:focus-visible:ring-ring bb:focus-visible:ring-offset-2"
    );

    private static string MarkerCircleCssClass(StepState state) => ClassNames.cn(
        "bb:flex bb:h-8 bb:w-8 bb:shrink-0 bb:items-center bb:justify-center bb:rounded-full",
        "bb:border-2 bb:text-sm bb:font-medium bb:transition-colors",
        state switch
        {
            StepState.Active => "bb:border-primary bb:bg-primary bb:text-primary-foreground",
            StepState.Completed => "bb:border-primary bb:bg-primary bb:text-primary-foreground",
            StepState.Error => "bb:border-destructive bb:bg-destructive bb:text-destructive-foreground",
            StepState.Skipped => "bb:border-muted-foreground/50 bb:text-muted-foreground",
            _ => "bb:border-muted-foreground/25 bb:text-muted-foreground"
        }
    );

    private string LabelCssClass => ClassNames.cn(
        "bb:flex bb:flex-col",
        IsVertical ? "bb:items-start" : "bb:items-center"
    );

    private static string TitleCssClass(StepState state) => ClassNames.cn(
        "bb:whitespace-nowrap bb:text-sm bb:font-medium bb:transition-colors",
        state switch
        {
            StepState.Active => "bb:text-foreground",
            StepState.Error => "bb:text-destructive",
            _ => "bb:text-muted-foreground"
        }
    );

    private string ConnectorCssClass(StepState state) => ClassNames.cn(
        "bb:transition-colors",
        IsVertical ? "bb:ms-[15px] bb:h-8 bb:w-0.5" : "bb:mx-2 bb:mt-4 bb:h-0.5 bb:flex-1",
        state == StepState.Completed ? "bb:bg-primary" : "bb:bg-border"
    );

    private string ContentCssClass => ClassNames.cn(
        IsVertical ? "bb:min-w-0 bb:flex-1" : HasStepContent ? "bb:mt-6" : null,
        ContentClass
    );
}
