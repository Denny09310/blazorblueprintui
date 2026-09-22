using BlazorBlueprint.Primitives.DataGrid;
using BlazorBlueprint.Primitives.Pivot;
using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// One value of a pivot table: what to work out for the items that land in a cell.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <remarks>
/// Declare two values and every cell shows two numbers, which is why the column headings gain a
/// last row naming them.
/// </remarks>
public class BbPivotValue<TItem> : ComponentBase, IDisposable
{
    private bool registered;
    private string? snapshot;

    /// <summary>
    /// Gets or sets the pivot table this value belongs to. Supplied by the parent.
    /// </summary>
    [CascadingParameter]
    public BbPivotDataGrid<TItem>? Parent { get; set; }

    /// <summary>
    /// Gets or sets a stable identifier, unique among the values of one table.
    /// </summary>
    /// <remarks>Defaults to <see cref="Title"/>.</remarks>
    [Parameter]
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the heading shown for this value.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the function to apply to the items in a cell.
    /// </summary>
    /// <remarks>
    /// Ignored when <see cref="Aggregate"/> is set. <see cref="AggregateFunction.Count"/> is the one
    /// function that works without a <see cref="Value"/>.
    /// </remarks>
    [Parameter]
    public AggregateFunction Function { get; set; } = AggregateFunction.Sum;

    /// <summary>
    /// Gets or sets the function that reads the number to aggregate from an item.
    /// </summary>
    [Parameter]
    public Func<TItem, object?>? Value { get; set; }

    /// <summary>
    /// Gets or sets a function that works the cell's value out itself.
    /// </summary>
    /// <remarks>
    /// Called once per cell and once per total, and a total is given every item under it rather
    /// than the cells it covers — which is what lets a ratio or a median come out right where a
    /// sum of sums would not.
    /// </remarks>
    [Parameter]
    public Func<IEnumerable<TItem>, object?>? Aggregate { get; set; }

    /// <summary>
    /// Gets or sets the format string used to print the value, such as <c>C0</c> or <c>N2</c>.
    /// </summary>
    [Parameter]
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets the template for a cell holding this value.
    /// </summary>
    /// <remarks>
    /// Used for every cell including the totals. The context says which it is, so one template can
    /// cover both and style a total differently.
    /// </remarks>
    [Parameter]
    public RenderFragment<PivotCellContext<TItem>>? Template { get; set; }

    /// <summary>
    /// Gets or sets the template for this value's own heading.
    /// </summary>
    [Parameter]
    public RenderFragment<BbPivotValue<TItem>>? HeaderTemplate { get; set; }

    /// <summary>
    /// Gets or sets whether this value takes part in the table.
    /// </summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Gets the identifier this value is known by.
    /// </summary>
    internal string EffectiveKey => string.IsNullOrEmpty(Key) ? Title : Key;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Parent is null)
        {
            throw new InvalidOperationException(
                $"{nameof(BbPivotValue<TItem>)} has to be declared inside a {nameof(BbPivotDataGrid<TItem>)}'s Values.");
        }

        Parent.Register(this);
        registered = true;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Only the settings that change what the table looks like, and none of the delegates: a
        // lambda written in markup is a new object on every render, so comparing them would report
        // a change every time and the parent would rebuild for ever.
        var current = $"{EffectiveKey}|{Title}|{Visible}|{Function}|{Format}|{Aggregate is not null}";

        if (registered && snapshot is not null && snapshot != current)
        {
            Parent?.Invalidate();
        }

        snapshot = current;
    }

    /// <summary>
    /// Builds the headless measure this declaration stands for.
    /// </summary>
    internal PivotMeasure<TItem> ToMeasure()
    {
        if (Aggregate is not null)
        {
            return new PivotMeasure<TItem>(EffectiveKey, Title, Aggregate) { Format = Format };
        }

        return new PivotMeasure<TItem>(EffectiveKey, Title, Function, Value) { Format = Format };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Takes the value back out of its table.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && registered)
        {
            Parent?.Unregister(this);
            registered = false;
        }
    }
}
