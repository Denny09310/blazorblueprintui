using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// One step of a <see cref="BbStepper"/>. Supplies the label shown in the indicator, and
/// optionally the content shown while the step is active.
/// </summary>
/// <remarks>
/// A step with no <see cref="ChildContent"/> renders nothing of its own; it only contributes its
/// label to the indicator, which is all a progress-only stepper needs.
/// </remarks>
public partial class BbStep : ComponentBase, IDisposable
{
    private int index = -1;

    /// <summary>
    /// Gets or sets the parent stepper, received through a cascading parameter.
    /// </summary>
    [CascadingParameter]
    public BbStepper? Stepper { get; set; }

    /// <summary>
    /// Gets or sets the label shown in the indicator.
    /// </summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets secondary text shown under the title.
    /// </summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the Lucide icon name shown in the step marker. Without one the marker shows
    /// the step number.
    /// </summary>
    [Parameter]
    public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets an explicit state, which overrides the state derived from the step's position.
    /// Use it to mark a step as <see cref="StepState.Error"/> or <see cref="StepState.Skipped"/>.
    /// </summary>
    [Parameter]
    public StepState? State { get; set; }

    /// <summary>
    /// Gets or sets whether the step is disabled, so a click cannot activate it.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets whether the step is optional, which adds a hint under the title.
    /// </summary>
    [Parameter]
    public bool Optional { get; set; }

    /// <summary>
    /// Gets or sets the content rendered while this step is the active one.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes for the step content wrapper.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets additional HTML attributes to apply to the step content wrapper.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Gets the zero-based index of this step within the stepper.</summary>
    internal int Index => index;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Stepper is null)
        {
            throw new InvalidOperationException(
                "BbStep must be used within a BbStepper component.");
        }

        index = Stepper.RegisterStep(this, new StepInfo
        {
            Title = Title,
            Description = Description,
            Icon = Icon,
            State = State,
            Disabled = Disabled,
            Optional = Optional,
            HasContent = ChildContent is not null
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stepper?.UnregisterStep(this);
        GC.SuppressFinalize(this);
    }

    private string? CssClass => ClassNames.cn(Class);
}
