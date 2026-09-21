using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>A world map series whose country fills represent numeric values.</summary>
/// <remarks>
/// Use one map series per chart. Bind one row per country using ISO alpha-2 or alpha-3
/// codes, or the English names in the bundled Natural Earth dataset. Matching ignores
/// case and surrounding whitespace. Unknown countries are ignored; the first row wins
/// when more than one row identifies the same country. Aggregate source data first.
/// </remarks>
public class BbMap : SeriesBase
{
    /// <summary>The property containing a country code or English country name.</summary>
    [Parameter]
    public string? CountryKey { get; set; }

    /// <summary>Whether users can pan and zoom the map. Defaults to false.</summary>
    [Parameter]
    public bool Roam { get; set; }

    /// <summary>The fill for countries without a numeric value.</summary>
    [Parameter]
    public string NoDataColor { get; set; } = "var(--muted)";

    /// <summary>The color of country boundaries.</summary>
    [Parameter]
    public string BorderColor { get; set; } = "var(--background)";

    /// <summary>Whether to show country names on the map.</summary>
    [Parameter]
    public bool ShowLabel { get; set; }

    internal override EChartsSeriesOption BuildSeriesCore()
    {
        var series = new EChartsSeriesOption
        {
            Type = "map",
            Map = "bb-world",
            Name = GetResolvedName(),
            Roam = Roam,
            SelectedMode = false,
            Left = "0",
            Right = "0",
            Top = "30",
            Bottom = "55",
            ItemStyle = new EChartsItemStyleOption
            {
                AreaColor = NoDataColor,
                BorderColor = BorderColor,
                BorderWidth = 1
            },
            Label = new EChartsLabelOption { Show = ShowLabel, Color = "var(--foreground)" },
            Emphasis = new EChartsEmphasisOption
            {
                Label = new EChartsLabelOption { Show = true, Color = "var(--foreground)" },
                ItemStyle = new EChartsItemStyleOption { BorderColor = "var(--foreground)" }
            },
            Data = []
        };

        if (string.IsNullOrWhiteSpace(CountryKey) || string.IsNullOrWhiteSpace(DataKey))
        {
            return series;
        }

        var countries = DataExtractor.ExtractStringValues(ParentChart?.Data, CountryKey);
        var values = GetSeriesData();
        for (var i = 0; i < Math.Min(countries.Count, values.Count); i++)
        {
            if (string.IsNullOrWhiteSpace(countries[i]))
            {
                continue;
            }

            // Missing and non-finite values must not become zero, or break JSON serialization.
            var numeric = double.TryParse(Convert.ToString(values[i], CultureInfo.InvariantCulture),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value);
            series.Data.Add(new Dictionary<string, object?>
            {
                ["name"] = countries[i].Trim(),
                ["value"] = numeric ? value : null,
                ["bbSourceIndex"] = i
            });
        }

        return series;
    }
}
