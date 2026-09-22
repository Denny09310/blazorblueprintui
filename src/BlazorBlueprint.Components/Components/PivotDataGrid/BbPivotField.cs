using BlazorBlueprint.Primitives.Pivot;
using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// One grouping level of a pivot table, declared inside its <c>Rows</c> or <c>Columns</c>.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <remarks>
/// Declare two fields on an axis and the second nests inside the first. Which axis a field belongs
/// to comes from the fragment it is written in, so the same declaration moves between rows and
/// columns by being moved in the markup.
/// </remarks>
public class BbPivotField<TItem> : ComponentBase, IDisposable
{
    private bool registered;
    private string? snapshot;

    /// <summary>
    /// Gets or sets the pivot table this field belongs to. Supplied by the parent.
    /// </summary>
    [CascadingParameter]
    public BbPivotDataGrid<TItem>? Parent { get; set; }

    /// <summary>
    /// Gets or sets which axis this field was declared on. Supplied by the parent.
    /// </summary>
    [CascadingParameter(Name = "PivotAxis")]
    public PivotAxis Axis { get; set; }

    /// <summary>
    /// Gets or sets a stable identifier, unique among the fields of one table.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="Title"/>. Set it where two fields share a title, or where the field
    /// picker's state has to survive a title change.
    /// </remarks>
    [Parameter]
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the heading shown for this level.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the function that reads the value to group by.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Func<TItem, object?> Value { get; set; } = default!;

    /// <summary>
    /// Gets or sets how a group's value becomes its heading. Defaults to the value's own text.
    /// </summary>
    /// <remarks>
    /// Use this to bucket or to format — a date into a quarter, a number into a band — rather than
    /// grouping on the formatted string, so the groups still sort by their real value.
    /// </remarks>
    [Parameter]
    public Func<object?, string>? Label { get; set; }

    /// <summary>
    /// Gets or sets how the groups of this level are ordered. Defaults to the value's own order.
    /// </summary>
    [Parameter]
    public IComparer<object?>? Comparer { get; set; }

    /// <summary>
    /// Gets or sets whether the groups of this level run in reverse.
    /// </summary>
    [Parameter]
    public bool Descending { get; set; }

    /// <summary>
    /// Gets or sets whether this field takes part in the table.
    /// </summary>
    /// <remarks>
    /// The field picker turns this on and off, so a field hidden here is still declared and can be
    /// brought back without touching the markup.
    /// </remarks>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Gets the identifier this field is known by.
    /// </summary>
    internal string EffectiveKey => string.IsNullOrEmpty(Key) ? Title : Key;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Parent is null)
        {
            throw new InvalidOperationException(
                $"{nameof(BbPivotField<TItem>)} has to be declared inside a {nameof(BbPivotDataGrid<TItem>)}'s Rows or Columns.");
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
        var current = $"{EffectiveKey}|{Title}|{Visible}|{Descending}|{Axis}";

        if (registered && snapshot is not null && snapshot != current)
        {
            Parent?.Invalidate();
        }

        snapshot = current;
    }

    /// <summary>
    /// Builds the headless field this declaration stands for.
    /// </summary>
    internal PivotField<TItem> ToField() =>
        new(EffectiveKey, Title, Value)
        {
            Label = Label,
            Comparer = Comparer,
            Descending = Descending,
        };

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Takes the field back out of its table.
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
