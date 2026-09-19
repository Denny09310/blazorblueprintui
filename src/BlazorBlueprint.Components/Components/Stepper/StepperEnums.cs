namespace BlazorBlueprint.Components;

/// <summary>
/// Defines the layout of a <see cref="BbStepper"/>.
/// </summary>
public enum StepperOrientation
{
    /// <summary>Steps run across the top, content below.</summary>
    Horizontal,

    /// <summary>Steps run down the leading side, content beside them.</summary>
    Vertical
}

/// <summary>
/// Defines the visual state of a single step in a <see cref="BbStepper"/>.
/// </summary>
/// <remarks>
/// A step derives its state from its position against the active step. Set
/// <see cref="BbStep.State"/> to override that, which is how an error or a skipped step is shown.
/// </remarks>
public enum StepState
{
    /// <summary>The step is still ahead.</summary>
    Pending,

    /// <summary>The step is the current one.</summary>
    Active,

    /// <summary>The step is behind the active step, or was marked done.</summary>
    Completed,

    /// <summary>The step needs attention before the run can finish.</summary>
    Error,

    /// <summary>An optional step that was passed over.</summary>
    Skipped
}
