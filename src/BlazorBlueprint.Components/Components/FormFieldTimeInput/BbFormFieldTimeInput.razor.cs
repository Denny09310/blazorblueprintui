using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// A form field wrapper for <see cref="BbTimeInput"/> that adds a label, helper text and
/// validation messages around the segmented time input.
/// </summary>
public partial class BbFormFieldTimeInput : FormFieldBase
{
    /// <summary>
    /// Gets or sets the selected time of day, or <c>null</c> while the segments are incomplete.
    /// </summary>
    [Parameter]
    public TimeSpan? Value { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the time changes.
    /// </summary>
    [Parameter]
    public EventCallback<TimeSpan?> ValueChanged { get; set; }

    /// <summary>
    /// Gets or sets an expression that identifies the bound value for EditForm integration.
    /// </summary>
    [Parameter]
    public Expression<Func<TimeSpan?>>? ValueExpression { get; set; }

    /// <summary>
    /// Gets or sets the culture that decides the separators and the 12/24-hour default.
    /// Defaults to the current culture.
    /// </summary>
    [Parameter]
    public CultureInfo? Culture { get; set; }

    /// <summary>
    /// Gets or sets an explicit 12- or 24-hour clock, overriding the culture's own choice.
    /// </summary>
    [Parameter]
    public TimeFormat? Format { get; set; }

    /// <summary>
    /// Gets or sets the earliest selectable time, inclusive.
    /// </summary>
    [Parameter]
    public TimeSpan? MinTime { get; set; }

    /// <summary>
    /// Gets or sets the latest selectable time, inclusive.
    /// </summary>
    [Parameter]
    public TimeSpan? MaxTime { get; set; }

    /// <summary>
    /// Gets or sets the increment the minute segment steps by.
    /// </summary>
    [Parameter]
    public int MinuteStep { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether a seconds segment is shown.
    /// </summary>
    [Parameter]
    public bool ShowSeconds { get; set; }

    /// <summary>
    /// Gets or sets whether a clock picker button is shown beside the segments.
    /// </summary>
    [Parameter]
    public bool ShowPicker { get; set; }

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
    /// Gets or sets additional CSS classes applied to the inner time input.
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(TimeSpan? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
