using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// A form field wrapper for <see cref="BbCascader{TItem}"/> that adds a label, helper text and
/// validation messages around the column picker.
/// </summary>
/// <typeparam name="TItem">The type of an item in the hierarchy.</typeparam>
public partial class BbFormFieldCascader<TItem> : FormFieldBase
{
    /// <summary>
    /// Gets or sets the root items of the hierarchy.
    /// </summary>
    [Parameter, EditorRequired]
    public IEnumerable<TItem> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the selector for an item's unique key.
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TItem, string> ValueField { get; set; } = default!;

    /// <summary>
    /// Gets or sets the selector for an item's display text.
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TItem, string> TextField { get; set; } = default!;

    /// <summary>
    /// Gets or sets the selector for an item's children.
    /// </summary>
    [Parameter, EditorRequired]
    public Func<TItem, IEnumerable<TItem>?> ChildrenProperty { get; set; } = default!;

    /// <summary>
    /// Gets or sets the selected node's key. Its full path is derived from <see cref="Items"/>.
    /// </summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the selected key changes.
    /// </summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>
    /// Gets or sets an expression that identifies the bound key for EditForm integration.
    /// </summary>
    [Parameter]
    public Expression<Func<string?>>? ValueExpression { get; set; }

    /// <summary>
    /// Gets or sets whether a branch may be selected while its children stay navigable.
    /// </summary>
    [Parameter]
    public bool ChangeOnSelect { get; set; }

    /// <summary>
    /// Gets or sets whether the full-path search box is shown.
    /// </summary>
    [Parameter]
    public bool ShowSearch { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a clear button is shown beside the control.
    /// </summary>
    [Parameter]
    public bool Clearable { get; set; } = true;

    /// <summary>
    /// Gets or sets the placeholder shown when nothing is selected.
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// Gets or sets whether the control is disabled.
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
    /// Gets or sets additional CSS classes applied to the inner cascader.
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <inheritdoc />
    protected override LambdaExpression? GetFieldExpression() => ValueExpression;

    private async Task HandleValueChanged(string? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        NotifyFieldChanged();
    }
}
