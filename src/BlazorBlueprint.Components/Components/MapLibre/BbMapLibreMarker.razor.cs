using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Components;

/// <summary>
/// A colored dot pinned to a geographic position on top of a <see cref="BbMapLibre"/> map.
/// The interop layer projects the coordinate to pixels and moves the element with CSS
/// transforms as the camera moves, so Blazor keeps ownership of its markup.
/// </summary>
public partial class BbMapLibreMarker : ComponentBase, IAsyncDisposable
{
    private const double CoordinateTolerance = 0.0000001;
    private const int DotSize = 12;

    private readonly string markerId = Guid.NewGuid().ToString("N");
    private bool registered;
    private double? lastLatitude;
    private double? lastLongitude;

    [CascadingParameter]
    private BbMapLibre? Map { get; set; }

    [Parameter]
    public double Latitude { get; set; }

    [Parameter]
    public double Longitude { get; set; }

    [Parameter]
    public string Color { get; set; } = "var(--color-primary)";

    private string MarkerStyle =>
        $"position:absolute; top:0; left:0; z-index:1; width:{DotSize}px; height:{DotSize}px; " +
        $"border-radius:9999px; background-color:{Color}; margin:-{DotSize / 2}px 0 0 -{DotSize / 2}px; " +
        "transform:translate(0,0); visibility:hidden;";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Map is null)
        {
            return;
        }

        if (firstRender)
        {
            await Map.RegisterMarkerAsync(markerId, Longitude, Latitude);

            lastLatitude = Latitude;
            lastLongitude = Longitude;
            registered = true;
        }
        else if (CoordinatesChanged())
        {
            lastLatitude = Latitude;
            lastLongitude = Longitude;

            await Map.UpdateMarkerAsync(markerId, Longitude, Latitude);
        }
    }

    private bool CoordinatesChanged() =>
        (lastLatitude is not { } latitude || lastLongitude is not { } longitude ||
         Math.Abs(latitude - Latitude) > CoordinateTolerance ||
         Math.Abs(longitude - Longitude) > CoordinateTolerance);

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (Map is not null && registered)
        {
            try
            {
                await Map.UnregisterMarkerAsync(markerId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // Expected during circuit disconnect in Blazor Server - safe to ignore
            }
            catch (InvalidOperationException)
            {
                // JS interop not available (prerendering) - safe to ignore
            }
        }
    }
}