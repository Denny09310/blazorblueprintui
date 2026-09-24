using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Components;

/// <summary>
/// Draws a route (a polyline) over a <see cref="BbMapLibre"/> map from an ordered list of
/// geographic <see cref="Points"/>. Unlike markers, a route is drawn natively by the map
/// engine as a GeoJSON line layer, so it pans, zooms and clips with the map automatically and
/// is restored when a theme switch restyles the map. Because it is painted to the map's
/// canvas, colors must be concrete CSS color values - canvas paints cannot resolve CSS
/// variables such as <c>var(--color-primary)</c>.
/// </summary>
public class BbMapLibreRoute : ComponentBase, IAsyncDisposable
{
    private const double CoordinateTolerance = 0.0000001;
    private const double StyleTolerance = 0.000001;

    private readonly string routeId = Guid.NewGuid().ToString("N");
    private bool registered;
    private BbMapCoordinate[]? lastPoints;
    private string? lastColor;
    private double lastWidth;
    private double lastOpacity;

    [CascadingParameter]
    private BbMapLibre? Map { get; set; }

    /// <summary>
    /// The geographic points that make up the route, in the order they are to be drawn.
    /// </summary>
    [Parameter]
    public IReadOnlyList<BbMapCoordinate> Points { get; set; } = Array.Empty<BbMapCoordinate>();

    /// <summary>
    /// The CSS color of the route line. Defaults to <c>#3b82f6</c>. Canvas paints cannot
    /// resolve CSS variables, so use a concrete color.
    /// </summary>
    [Parameter]
    public string Color { get; set; } = "#3b82f6";

    /// <summary>
    /// The line width in pixels. Defaults to 3.
    /// </summary>
    [Parameter]
    public double Width { get; set; } = 3;

    /// <summary>
    /// The line opacity, from 0 (fully transparent) to 1 (fully opaque). Defaults to 1.
    /// </summary>
    [Parameter]
    public double Opacity { get; set; } = 1;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Map is null)
        {
            return;
        }

        if (firstRender)
        {
            await Map.RegisterRouteAsync(routeId, Points, Color, Width, Opacity);

            registered = true;
            CaptureSnapshot();
        }
        else if (RouteChanged())
        {
            await Map.UpdateRouteAsync(routeId, Points, Color, Width, Opacity);

            CaptureSnapshot();
        }
    }

    private bool RouteChanged()
    {
        if (lastPoints is not { } previousPoints || previousPoints.Length != Points.Count)
        {
            return true;
        }

        for (var i = 0; i < Points.Count; i++)
        {
            var previous = previousPoints[i];
            var current = Points[i];

            if (Math.Abs(previous.Latitude - current.Latitude) > CoordinateTolerance ||
                Math.Abs(previous.Longitude - current.Longitude) > CoordinateTolerance)
            {
                return true;
            }
        }

        return !string.Equals(Color, lastColor, StringComparison.Ordinal) ||
               Math.Abs(Width - lastWidth) > StyleTolerance ||
               Math.Abs(Opacity - lastOpacity) > StyleTolerance;
    }

    private void CaptureSnapshot()
    {
        lastPoints = Points.ToArray();
        lastColor = Color;
        lastWidth = Width;
        lastOpacity = Opacity;
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (Map is not null && registered)
        {
            try
            {
                await Map.UnregisterRouteAsync(routeId);
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