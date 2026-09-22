using BlazorBlueprint.Primitives.DataGrid;
using BlazorBlueprint.Primitives.Pivot;
using Xunit;

namespace BlazorBlueprint.Tests.Pivot;

/// <summary>
/// The cross-tabulation engine: headings, cells, totals and the order everything comes out in.
/// </summary>
public class PivotBuilderTests
{
    private sealed record Sale(string Country, string Category, string Quarter, decimal Amount, int Units);

    private static readonly Sale[] Sales =
    [
        new("UK", "Tools", "Q1", 100m, 2),
        new("UK", "Tools", "Q2", 200m, 3),
        new("UK", "Paint", "Q1", 50m, 1),
        new("FR", "Tools", "Q1", 300m, 4),
        new("FR", "Paint", "Q2", 70m, 7),
        new("FR", "Paint", "Q2", 30m, 3),
    ];

    private static PivotField<Sale> Country() => new("country", "Country", s => s.Country);

    private static PivotField<Sale> Category() => new("category", "Category", s => s.Category);

    private static PivotField<Sale> Quarter() => new("quarter", "Quarter", s => s.Quarter);

    private static PivotMeasure<Sale> Amount() =>
        new("amount", "Amount", AggregateFunction.Sum, s => s.Amount);

    private static PivotMeasure<Sale> Count() =>
        new("count", "Count", AggregateFunction.Count);

    private static PivotTable<Sale> Build(
        IReadOnlyList<PivotField<Sale>> rows,
        IReadOnlyList<PivotField<Sale>> columns,
        IReadOnlyList<PivotMeasure<Sale>> measures,
        PivotTotals totals = PivotTotals.None,
        IEnumerable<Sale>? source = null) =>
        PivotBuilder.Build(source ?? Sales, rows, columns, measures, totals);

    private static object? Cell(PivotTable<Sale> table, string row, string column, int measure = 0)
    {
        var rowLeaf = table.RowLeaves.Single(l => Describe(l) == row);
        var columnLeaf = table.ColumnLeaves.Single(l => Describe(l) == column);
        return table.GetValue(rowLeaf, columnLeaf, measure);
    }

    /// <summary>The leaf's full path as one string, so a test can name a nested cell.</summary>
    private static string Describe(PivotAxisNode leaf)
    {
        var parts = new List<string>();
        for (var node = leaf; node is not null; node = node.Parent)
        {
            parts.Insert(0, node.Label);
        }

        return string.Join("/", parts.Where(p => p.Length > 0));
    }

    [Fact]
    public void CrossTabulatesOneFieldAgainstAnother()
    {
        var table = Build([Country()], [Category()], [Amount()]);

        Assert.Equal(["FR", "UK"], table.RowLeaves.Select(l => l.Label));
        Assert.Equal(["Paint", "Tools"], table.ColumnLeaves.Select(l => l.Label));

        Assert.Equal(100d, Cell(table, "FR", "Paint"));
        Assert.Equal(300d, Cell(table, "FR", "Tools"));
        Assert.Equal(50d, Cell(table, "UK", "Paint"));
        Assert.Equal(300d, Cell(table, "UK", "Tools"));
    }

    [Fact]
    public void LeavesACellEmptyWhereNothingLanded()
    {
        // A pivot only grows a column for a value that appears somewhere, so Q2 has to exist for
        // one country before the other country's Q2 cell can be empty.
        var table = Build([Country()], [Quarter()], [Amount()], source:
        [
            new("UK", "Tools", "Q1", 10m, 1),
            new("FR", "Tools", "Q2", 20m, 1),
        ]);

        Assert.Equal(["Q1", "Q2"], table.ColumnLeaves.Select(l => l.Label));
        Assert.Null(Cell(table, "UK", "Q2"));
        Assert.Null(Cell(table, "FR", "Q1"));
    }

    [Fact]
    public void NestsASecondFieldInsideTheFirst()
    {
        var table = Build([Country(), Category()], [Quarter()], [Amount()]);

        Assert.Equal(
            ["FR/Paint", "FR/Tools", "UK/Paint", "UK/Tools"],
            table.RowLeaves.Select(Describe));

        Assert.Equal(100d, Cell(table, "FR/Paint", "Q2"));
        Assert.Null(Cell(table, "FR/Paint", "Q1"));
        Assert.Equal(200d, Cell(table, "UK/Tools", "Q2"));
    }

    [Fact]
    public void ReportsTheSpanOfEveryHeading()
    {
        var table = Build([Country(), Category()], [Quarter()], [Amount()]);

        var france = table.RowRoots.Single(r => r.Label == "FR");

        Assert.Equal(2, france.Span);
        Assert.All(france.Children, c => Assert.Equal(1, c.Span));
    }

    [Fact]
    public void AddsAGrandTotalColumnAtTheEndOfEveryRow()
    {
        var table = Build([Country()], [Category()], [Amount()], PivotTotals.RowTotals);

        Assert.Equal(["Paint", "Tools", "Total"], table.ColumnLeaves.Select(l => l.Label));
        Assert.True(table.ColumnLeaves[^1].IsTotal);

        Assert.Equal(400d, Cell(table, "FR", "Total"));
        Assert.Equal(350d, Cell(table, "UK", "Total"));
    }

    [Fact]
    public void AddsAGrandTotalRowUnderEveryColumn()
    {
        var table = Build([Country()], [Category()], [Amount()], PivotTotals.ColumnTotals);

        Assert.Equal(["FR", "UK", "Total"], table.RowLeaves.Select(l => l.Label));

        Assert.Equal(150d, Cell(table, "Total", "Paint"));
        Assert.Equal(600d, Cell(table, "Total", "Tools"));
    }

    [Fact]
    public void TheGrandTotalOfGrandTotalsIsEverything()
    {
        var table = Build([Country()], [Category()], [Amount()], PivotTotals.Grand);

        Assert.Equal(750d, Cell(table, "Total", "Total"));
        Assert.Equal(Sales.Sum(s => (double)s.Amount), Cell(table, "Total", "Total"));
    }

    [Fact]
    public void AddsSubtotalsUnderEachNestedGroup()
    {
        var table = Build([Country(), Category()], [Quarter()], [Amount()], PivotTotals.RowSubtotals);

        Assert.Equal(
            ["FR/Paint", "FR/Tools", "FR/Total", "UK/Paint", "UK/Tools", "UK/Total"],
            table.RowLeaves.Select(Describe));

        Assert.Equal(400d, SumRow(table, "FR/Total"));
        Assert.Equal(300d, Cell(table, "FR/Total", "Q1"));
        Assert.Equal(100d, Cell(table, "FR/Total", "Q2"));
    }

    private static double SumRow(PivotTable<Sale> table, string row)
    {
        var leaf = table.RowLeaves.Single(l => Describe(l) == row);
        return table.ColumnLeaves.Sum(c => (double?)table.GetValue(leaf, c, 0) ?? 0);
    }

    [Fact]
    public void ASubtotalCoversOnlyItsOwnGroup()
    {
        var table = Build([Country(), Category()], [Quarter()], [Amount()], PivotTotals.All);

        // France's subtotal is France's 400, not the 750 of everything.
        Assert.Equal(400d, Cell(table, "FR/Total", "Total"));
        Assert.Equal(350d, Cell(table, "UK/Total", "Total"));
        Assert.Equal(750d, Cell(table, "Total", "Total"));
    }

    [Fact]
    public void AnAverageTotalAveragesTheItemsNotTheCells()
    {
        // FR/Paint holds 70 and 30. Averaging the two cells of Q2 would give 50; averaging the
        // items gives 50 as well, so this uses a case where the two answers differ.
        var source = new Sale[]
        {
            new("UK", "Tools", "Q1", 10m, 1),
            new("UK", "Tools", "Q1", 20m, 1),
            new("UK", "Tools", "Q2", 300m, 1),
        };

        var table = PivotBuilder.Build(
            source,
            [Country()],
            [Quarter()],
            [new PivotMeasure<Sale>("avg", "Average", AggregateFunction.Average, s => s.Amount)],
            PivotTotals.RowTotals);

        // Averaging the cells would give (15 + 300) / 2 = 157.5. Averaging the items gives 110.
        Assert.Equal(110d, Cell(table, "UK", "Total"));
    }

    [Fact]
    public void ACustomAggregateSeesTheItemsBehindATotal()
    {
        var table = PivotBuilder.Build(
            Sales,
            [Country()],
            [Category()],
            [new PivotMeasure<Sale>("rate", "Per unit", items =>
            {
                var list = items.ToList();
                var units = list.Sum(s => s.Units);
                return units == 0 ? null : (double)list.Sum(s => s.Amount) / units;
            })],
            PivotTotals.RowTotals);

        // France: 400 over 14 units, not the average of its two per-unit rates.
        Assert.Equal(400d / 14, (double)Cell(table, "FR", "Total")!, 6);
    }

    [Fact]
    public void CarriesEveryMeasureInEveryCell()
    {
        var table = Build([Country()], [Category()], [Amount(), Count()]);

        Assert.Equal(2, table.Measures.Count);
        Assert.Equal(100d, Cell(table, "FR", "Paint", measure: 0));
        Assert.Equal(2, Cell(table, "FR", "Paint", measure: 1));
    }

    [Fact]
    public void CountsItemsWithoutASelector()
    {
        var table = Build([Country()], [Category()], [Count()], PivotTotals.Grand);

        Assert.Equal(6, Cell(table, "Total", "Total"));
    }

    [Fact]
    public void WorksWithRowsAndNoColumns()
    {
        var table = PivotBuilder.Build(Sales, [Country()], [], [Amount()], PivotTotals.ColumnTotals);

        Assert.Single(table.ColumnLeaves);
        Assert.Equal(["FR", "UK", "Total"], table.RowLeaves.Select(l => l.Label));
        Assert.Equal(750d, table.GetValue(table.RowLeaves[^1], table.ColumnLeaves[0], 0));
    }

    [Fact]
    public void WorksWithColumnsAndNoRows()
    {
        var table = PivotBuilder.Build(Sales, [], [Country()], [Amount()], PivotTotals.None);

        Assert.Single(table.RowLeaves);
        Assert.Equal(["FR", "UK"], table.ColumnLeaves.Select(l => l.Label));
    }

    [Fact]
    public void OrdersGroupsByTheirValueAndReversesOnRequest()
    {
        var ascending = Build([Country()], [Category()], [Amount()]);
        Assert.Equal(["FR", "UK"], ascending.RowLeaves.Select(l => l.Label));

        var descending = PivotBuilder.Build(
            Sales,
            [new PivotField<Sale>("country", "Country", s => s.Country) { Descending = true }],
            [Category()],
            [Amount()],
            PivotTotals.None);

        Assert.Equal(["UK", "FR"], descending.RowLeaves.Select(l => l.Label));
    }

    [Fact]
    public void HonoursACustomComparer()
    {
        // Longest label first, which no natural ordering would produce.
        var field = new PivotField<Sale>("category", "Category", s => s.Category)
        {
            Comparer = Comparer<object?>.Create((a, b) =>
                (b?.ToString()?.Length ?? 0).CompareTo(a?.ToString()?.Length ?? 0)),
        };

        var table = PivotBuilder.Build(Sales, [field], [Country()], [Amount()], PivotTotals.None);

        Assert.Equal(["Tools", "Paint"], table.RowLeaves.Select(l => l.Label));
    }

    [Fact]
    public void UsesTheFieldsLabelFunctionForHeadings()
    {
        var field = new PivotField<Sale>("country", "Country", s => s.Country)
        {
            Label = value => value switch { "UK" => "United Kingdom", "FR" => "France", _ => "?" },
        };

        var table = PivotBuilder.Build(Sales, [field], [Category()], [Amount()], PivotTotals.None);

        Assert.Equal(["France", "United Kingdom"], table.RowLeaves.Select(l => l.Label));

        // The key is still the raw value, so the ordering is the raw one.
        Assert.Equal("FR", table.RowLeaves[0].Key);
    }

    [Fact]
    public void GroupsNullsUnderOneHeading()
    {
        var source = new Sale[]
        {
            new("UK", null!, "Q1", 10m, 1),
            new("UK", null!, "Q2", 20m, 1),
            new("UK", "Tools", "Q1", 5m, 1),
        };

        var table = PivotBuilder.Build(source, [Country()], [Category()], [Amount()], PivotTotals.None);

        Assert.Equal(["(blank)", "Tools"], table.ColumnLeaves.Select(l => l.Label));
        Assert.Equal(30d, Cell(table, "UK", "(blank)"));
    }

    [Fact]
    public void HandsBackTheItemsBehindACellForDrillDown()
    {
        var table = Build([Country()], [Category()], [Amount()], PivotTotals.Grand);

        var france = table.RowLeaves.Single(l => l.Label == "FR");
        var paint = table.ColumnLeaves.Single(l => l.Label == "Paint");

        Assert.Equal(2, table.GetItems(france, paint).Count);
        Assert.All(table.GetItems(france, paint), s => Assert.Equal("FR", s.Country));

        var totalRow = table.RowLeaves.Single(l => l.IsTotal);
        var totalColumn = table.ColumnLeaves.Single(l => l.IsTotal);
        Assert.Equal(Sales.Length, table.GetItems(totalRow, totalColumn).Count);
    }

    [Fact]
    public void ReportsHowManyItemsWentIn()
    {
        var table = Build([Country()], [Category()], [Amount()]);

        Assert.Equal(Sales.Length, table.ItemCount);
        Assert.False(table.IsEmpty);
    }

    [Fact]
    public void AnEmptySourceGivesAnEmptyTable()
    {
        var table = PivotBuilder.Build([], [Country()], [Category()], [Amount()], PivotTotals.Grand);

        Assert.True(table.IsEmpty);
        Assert.Empty(table.RowLeaves);
        Assert.Equal(0, table.ItemCount);
    }

    [Fact]
    public void RejectsATableWithNothingToShow()
    {
        Assert.Throws<ArgumentException>(() => PivotBuilder.Build(Sales, [Country()], [Category()], []));
        Assert.Throws<ArgumentException>(() => PivotBuilder.Build(Sales, [], [], [Amount()]));
    }

    [Fact]
    public void AMeasureNeedsASelectorUnlessItOnlyCounts()
    {
        Assert.Throws<ArgumentException>(
            () => new PivotMeasure<Sale>("sum", "Sum", AggregateFunction.Sum));

        Assert.Throws<ArgumentException>(
            () => new PivotMeasure<Sale>("none", "None", AggregateFunction.None, s => s.Amount));

        // Count is the one that works without one.
        _ = new PivotMeasure<Sale>("count", "Count", AggregateFunction.Count);
    }

    [Fact]
    public void SkipsValuesThatAreNotNumbers()
    {
        var source = new Sale[]
        {
            new("UK", "Tools", "Q1", 10m, 1),
            new("UK", "Tools", "Q1", 20m, 1),
        };

        var table = PivotBuilder.Build(
            source,
            [Country()],
            [Category()],
            [new PivotMeasure<Sale>("mixed", "Mixed", AggregateFunction.Sum, s => s.Category)]);

        // Nothing convertible, so the cell is empty rather than a crash.
        Assert.Null(Cell(table, "UK", "Tools"));
    }

    [Fact]
    public void APathNamesTheHeadingAllTheWayUp()
    {
        var table = Build([Country(), Category()], [Quarter()], [Amount()]);

        var leaf = table.RowLeaves.Single(l => Describe(l) == "UK/Tools");

        Assert.Equal(["UK", "Tools"], leaf.Path());
    }

    [Fact]
    public void EveryLeafHasItsOwnDrawnPosition()
    {
        var table = Build([Country(), Category()], [Quarter()], [Amount()], PivotTotals.All);

        var indexes = table.RowLeaves.Select(l => l.LeafIndex).ToList();

        Assert.Equal(Enumerable.Range(0, table.RowLeaves.Count), indexes);
        Assert.Equal(indexes.Count, indexes.Distinct().Count());
    }
}
