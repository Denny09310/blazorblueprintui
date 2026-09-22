using BlazorBlueprint.Primitives.DataGrid;

namespace BlazorBlueprint.Primitives.Pivot;

/// <summary>
/// One value of a pivot table: what to work out for the items that land in a cell.
/// </summary>
/// <typeparam name="TItem">The type of the source items.</typeparam>
/// <remarks>
/// A table with two measures shows two numbers in every cell, which is why the column headings gain
/// a last row naming them.
/// </remarks>
public sealed class PivotMeasure<TItem>
{
    /// <summary>
    /// Creates a measure using one of the built-in functions.
    /// </summary>
    /// <param name="key">A stable identifier, unique among the measures of one table.</param>
    /// <param name="title">The heading shown for the measure.</param>
    /// <param name="function">The function to apply.</param>
    /// <param name="selector">
    /// Reads the number to aggregate from an item. Not needed for
    /// <see cref="AggregateFunction.Count"/>, which only counts items.
    /// </param>
    public PivotMeasure(string key, string title, AggregateFunction function, Func<TItem, object?>? selector = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        if (function is AggregateFunction.None)
        {
            throw new ArgumentException(
                $"A measure needs a real function. Use {nameof(AggregateFunction.Count)} to count items, or supply a custom aggregate.",
                nameof(function));
        }

        if (selector is null && function is not AggregateFunction.Count)
        {
            throw new ArgumentException(
                $"{function} needs a selector saying which value to aggregate. Only {nameof(AggregateFunction.Count)} works without one.",
                nameof(selector));
        }

        Key = key;
        Title = title;
        Function = function;
        Selector = selector;
    }

    /// <summary>
    /// Creates a measure that works the value out itself.
    /// </summary>
    /// <param name="key">A stable identifier, unique among the measures of one table.</param>
    /// <param name="title">The heading shown for the measure.</param>
    /// <param name="aggregate">
    /// Works out the cell's value from the items that landed in it. Called once per cell and once
    /// per total, and for a total it is given every item under that total rather than the
    /// already-aggregated cells, so a ratio or a median comes out right.
    /// </param>
    public PivotMeasure(string key, string title, Func<IEnumerable<TItem>, object?> aggregate)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(aggregate);

        Key = key;
        Title = title;
        Function = AggregateFunction.None;
        Aggregate = aggregate;
    }

    /// <summary>
    /// Gets the identifier, unique among the measures of one table.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the heading shown for this measure.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the built-in function, or <see cref="AggregateFunction.None"/> for a custom aggregate.
    /// </summary>
    public AggregateFunction Function { get; }

    /// <summary>
    /// Gets the function that reads the value to aggregate, where there is one.
    /// </summary>
    public Func<TItem, object?>? Selector { get; }

    /// <summary>
    /// Gets the custom aggregate, where there is one.
    /// </summary>
    public Func<IEnumerable<TItem>, object?>? Aggregate { get; }

    /// <summary>
    /// Gets or sets the format string used to display the value, such as <c>C0</c> or <c>N2</c>.
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Works the measure out for a set of items.
    /// </summary>
    /// <param name="items">The items that landed in the cell.</param>
    /// <returns>The value, or <see langword="null"/> where there is nothing to aggregate.</returns>
    internal object? Evaluate(IEnumerable<TItem> items)
    {
        if (Aggregate is not null)
        {
            return Aggregate(items);
        }

        if (Function == AggregateFunction.Count)
        {
            return items.Count();
        }

        // Anything that will not convert to a number is skipped rather than throwing, so one bad
        // row does not take the whole table down.
        var numbers = items
            .Select(item => Selector!(item))
            .Where(value => value is not null)
            .Select(value => TryToDouble(value!, out var d) ? d : (double?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        if (numbers.Count == 0)
        {
            return null;
        }

        return Function switch
        {
            AggregateFunction.Sum => numbers.Sum(),
            AggregateFunction.Average => numbers.Average(),
            AggregateFunction.Min => numbers.Min(),
            AggregateFunction.Max => numbers.Max(),
            _ => null,
        };
    }

    private static bool TryToDouble(object value, out double result)
    {
        if (value is IConvertible)
        {
            try
            {
                result = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception error) when (error is FormatException or InvalidCastException or OverflowException)
            {
                // Falls through to the failure below.
            }
        }

        result = 0;
        return false;
    }
}
