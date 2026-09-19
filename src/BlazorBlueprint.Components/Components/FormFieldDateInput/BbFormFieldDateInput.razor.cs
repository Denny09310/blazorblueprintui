using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// A form field wrapper for <see cref="BbDateInput"/> that adds a label, helper text and
/// validation messages around the segmented date input.
/// </summary>
public partial class BbFormFieldDateInput : FormFieldBase
{
    /// <summary>
    /// Gets or sets the selected date, or <c>null</c> while the segments are incomplete.
    /// </summary>
    [Parameter]
    public DateTime? Value { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the date changes.
    /// </summary>
    [Parameter]
    public EventCallback<DateTime?> ValueChanged { get; set; }

    /// <summary>
    /// Gets or sets an expression that identifies the bound value for EditForm integration.
    /// </summary>
    [Parameter]
    public Expression<Func<DateTime?>>? ValueExpression { get; set; }

    /// <summary>
    /// Gets or sets the culture that decides the segment order and separators.
    /// Defaults to the current culture.
    /// </summary>
    [Parameter]
    public CultureInfo? Culture { get; set; }

    /// <summary>
    /// Gets or sets the earliest selectable date, inclusive.
    /// </summary>
    [Parameter]
    public DateTime? MinDate { get; set; }

    /// <summary>
    /// Gets or sets the latest selectable date, inclusive.
    /// </summary>
    [Parameter]
    public DateTime? MaxDate { get; set; }

    /// <summary>
    /// Gets or sets whether a calendar picker button is shown beside the segments.
    /// </summary>
    [Parameter]
    public bool ShowCalendar { get; set; }

    /// <summary>
    /// Gets or sets whether the segments are read-only.
    /// </summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Gets or sets whether the input is disabled.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets whether the field is required.
    /// </summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the form field name submitted with the value.
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes applied to the inner date input.
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(DateTime? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
