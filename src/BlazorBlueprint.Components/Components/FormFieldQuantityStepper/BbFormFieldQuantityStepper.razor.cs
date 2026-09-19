using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// A form field wrapper for <see cref="BbQuantityStepper"/> that adds a label, helper text and
/// validation messages around the quantity control.
/// </summary>
public partial class BbFormFieldQuantityStepper : FormFieldBase
{
    /// <summary>
    /// Gets or sets the current quantity.
    /// </summary>
    [Parameter]
    public int Value { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the quantity changes.
    /// </summary>
    [Parameter]
    public EventCallback<int> ValueChanged { get; set; }

    /// <summary>
    /// Gets or sets an expression that identifies the bound value for EditForm integration.
    /// </summary>
    [Parameter]
    public Expression<Func<int>>? ValueExpression { get; set; }

    /// <summary>
    /// Gets or sets the smallest allowed quantity.
    /// </summary>
    [Parameter]
    public int Min { get; set; }

    /// <summary>
    /// Gets or sets the largest allowed quantity, or <c>null</c> for no upper bound.
    /// </summary>
    [Parameter]
    public int? Max { get; set; }

    /// <summary>
    /// Gets or sets the increment each press applies.
    /// </summary>
    [Parameter]
    public int Step { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether the quantity may only be read.
    /// </summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Gets or sets whether the control is disabled.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the decrease button is pressed at
    /// <see cref="Min"/>, which turns it into a remove action.
    /// </summary>
    [Parameter]
    public EventCallback OnRemove { get; set; }

    /// <summary>
    /// Gets or sets the form field name submitted with the value.
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes applied to the inner quantity stepper.
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(int value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
