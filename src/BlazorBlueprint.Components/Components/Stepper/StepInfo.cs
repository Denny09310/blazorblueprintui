namespace BlazorBlueprint.Components;

/// <summary>
/// The metadata a <see cref="BbStep"/> hands to its <see cref="BbStepper"/> when it registers,
/// so the stepper can draw the indicator without reaching into its children.
/// </summary>
internal sealed class StepInfo
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public StepState? State { get; set; }
    public bool Disabled { get; set; }
    public bool Optional { get; set; }
    public bool HasContent { get; set; }
}
