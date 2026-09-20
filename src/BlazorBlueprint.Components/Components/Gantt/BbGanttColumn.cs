using System.Globalization;
using BlazorBlueprint.Primitives.Gantt;
using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// One column of the task list beside the timeline, declared inside a <c>BbGantt</c>'s
/// <c>Columns</c>.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <remarks>
/// <para>
/// Columns carry a width in pixels rather than a share of the space. A Gantt needs to know how wide
/// its task list is before it can place the first bar, and a width that is only settled once the
/// browser has laid the table out is a width the chart cannot draw against.
/// </para>
/// <para>
/// The first column declared carries the expander and the indent, unless another sets
/// <see cref="IsTree"/>.
/// </para>
/// </remarks>
public class BbGanttColumn<TItem> : ComponentBase, IDisposable
{
    private bool registered;
    private string? snapshot;

    /// <summary>
    /// Gets or sets the chart this column belongs to. Supplied by the parent.
    /// </summary>
    [CascadingParameter]
    public BbGantt<TItem>? Parent { get; set; }

    /// <summary>
    /// Gets or sets a stable identifier, unique among the columns of one chart.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="Title"/>. Set it where two columns share a title, or where a dragged
    /// width has to survive a title change.
    /// </remarks>
    [Parameter]
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the heading.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how wide the column is drawn, in pixels.
    /// </summary>
    [Parameter]
    public int Width { get; set; } = 140;

    /// <summary>
    /// Gets or sets the reader for the cell's value. Ignored where <see cref="ChildContent"/> is set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This reads the task as it was written down, and it is what a sort compares. That matters for
    /// a summary: its bar shows the span of its children, while this shows whatever dates the
    /// summary itself carries. Use <see cref="ChildContent"/> where the column has to agree with
    /// the bar beside it.
    /// </para>
    /// <para>
    /// Leave both unset on the tree column and the task's name is shown.
    /// </para>
    /// </remarks>
    [Parameter]
    public Func<TItem, object?>? Value { get; set; }

    /// <summary>
    /// Gets or sets the format the value is written with, as a standard .NET format string.
    /// </summary>
    [Parameter]
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets the cell's content, which replaces <see cref="Value"/>.
    /// </summary>
    /// <remarks>
    /// The context is the row — the task after roll-up — so <c>context.Start</c> is the date on the
    /// bar and <c>context.Item</c> is the task it came from. Sorting still reads
    /// <see cref="Value"/>, which has to run before there are any rows to sort.
    /// </remarks>
    [Parameter]
    public RenderFragment<GanttRow<TItem>>? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets which side of the column the content sits on.
    /// </summary>
    [Parameter]
    public GanttColumnAlign Align { get; set; } = GanttColumnAlign.Start;

    /// <summary>
    /// Gets or sets whether this column carries the expander and the indent.
    /// </summary>
    /// <remarks>
    /// Only one column can. Left unset, the first column declared takes the job.
    /// </remarks>
    [Parameter]
    public bool IsTree { get; set; }

    /// <summary>
    /// Gets or sets whether the heading sorts the tasks.
    /// </summary>
    /// <remarks>
    /// Sorting reorders tasks under the same parent and never moves one out of its branch, because
    /// a child that outranks its parent is not a plan any more.
    /// </remarks>
    [Parameter]
    public bool Sortable { get; set; }

    /// <summary>
    /// Gets or sets how this column's values are ordered. Defaults to the value's own order.
    /// </summary>
    [Parameter]
    public IComparer<object?>? Comparer { get; set; }

    /// <summary>
    /// Gets the identifier this column is known by.
    /// </summary>
    internal string EffectiveKey => string.IsNullOrEmpty(Key) ? Title : Key;

    /// <summary>
    /// Gets the width the column is drawn at, which a drag on its edge overrides.
    /// </summary>
    internal int EffectiveWidth => Parent?.WidthOf(this) ?? Width;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Parent is null)
        {
            throw new InvalidOperationException(
                $"{nameof(BbGanttColumn<TItem>)} has to be declared inside a {nameof(BbGantt<TItem>)}'s Columns.");
        }

        Parent.Register(this);
        registered = true;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Only the settings that change what the chart looks like, and none of the delegates: a
        // lambda written in markup is a new object on every render, so comparing them would report
        // a change every time and the parent would rebuild for ever.
        var current = $"{EffectiveKey}|{Title}|{Width}|{Align}|{IsTree}|{Sortable}|{Format}";

        if (registered && snapshot is not null && snapshot != current)
        {
            Parent?.Invalidate();
        }

        snapshot = current;
    }

    /// <summary>
    /// Reads the cell's text for a row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The text, or null where the column has no reader.</returns>
    internal string? Text(GanttRow<TItem> row)
    {
        if (Value is null)
        {
            return IsTree ? row.Text : null;
        }

        var value = Value(row.Item);
        if (value is null)
        {
            return null;
        }

        return Format is { Length: > 0 } format && value is IFormattable formattable
            ? formattable.ToString(format, CultureInfo.CurrentCulture)
            : value.ToString();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Takes the column back out of its chart.
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
