using BlazorBlueprint.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Components;

public partial class BbMapLibre : ComponentBase, IAsyncDisposable
{
    private const double CoordinateTolerance = 0.0000001;
    private const double ZoomTolerance = 0.0001;
    private const double BearingTolerance = 0.0001;

    private readonly string mapId = Guid.NewGuid().ToString("N");

    private IJSObjectReference? jsModule;
    private bool jsInitialized;
    private DotNetObjectReference<BbMapLibre>? dotNetRef;
    private double? lastLatitude;
    private double? lastLongitude;
    private double? lastZoom;
    private double? lastBearing;

    public IJSObjectReference? Map { get; private set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Content shown inside the map container while the map is loading. When omitted, a default
    /// loading indicator is shown. Once the map is ready the child content (typically the
    /// markers) is rendered in its place.
    /// </summary>
    [Parameter]
    public RenderFragment? LoadingContent { get; set; }

    [Parameter]
    public BbMapCoordinate? Center { get; set; }

    [Parameter]
    public EventCallback<BbMapCoordinate> CenterChanged { get; set; }

    [Parameter]
    public double? Zoom { get; set; }

    [Parameter]
    public EventCallback<double> ZoomChanged { get; set; }

    /// <summary>
    /// The map's bearing (rotation) in degrees, 0 being north. Two-way bindable with
    /// <c>@bind-Bearing</c>.
    /// </summary>
    [Parameter]
    public double? Bearing { get; set; }

    [Parameter]
    public EventCallback<double> BearingChanged { get; set; }

    [Parameter]
    public string? Style { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await InitializeJsAsync();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (jsInitialized)
        {
            await SyncViewAsync();
        }
    }

    private async Task InitializeJsAsync()
    {
        if (jsInitialized)
        {
            return;
        }

        try
        {
            jsModule = await JsModules.GetAsync(JS, "./_content/BlazorBlueprint.Components/js/maplibre-gl-interop.js");

            dotNetRef = DotNetObjectReference.Create(this);

            var options = new Dictionary<string, object?>();

            if (Center is { } center)
            {
                options["center"] = new[] { center.Longitude, center.Latitude };
            }

            if (Zoom is { } zoom)
            {
                options["zoom"] = zoom;
            }

            if (Bearing is { } bearing)
            {
                options["bearing"] = bearing;
            }

            Map = await jsModule.InvokeAsync<IJSObjectReference>("initializeMapLibre", mapId, dotNetRef, options);

            lastLatitude = Center?.Latitude;
            lastLongitude = Center?.Longitude;
            lastZoom = Zoom;
            lastBearing = Bearing;

            jsInitialized = true;

            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to initialize MapLibre JS: {ex.Message}");
        }
    }

    /// <summary>
    /// Registers a marker with the map. Markers are only rendered once the map's JS is ready, so
    /// the interop module is always available here.
    /// </summary>
    internal async Task RegisterMarkerAsync(string markerId, double lng, double lat)
    {
        if (jsModule is null)
        {
            return;
        }

        await jsModule.InvokeVoidAsync("registerMarker", mapId, markerId, lng, lat);
    }

    /// <summary>
    /// Moves an already registered marker to a new position.
    /// </summary>
    internal async Task UpdateMarkerAsync(string markerId, double lng, double lat)
    {
        if (jsModule is null)
        {
            return;
        }

        await jsModule.InvokeVoidAsync("updateMarker", mapId, markerId, lng, lat);
    }

    /// <summary>
    /// Removes a marker from the map.
    /// </summary>
    internal async Task UnregisterMarkerAsync(string markerId)
    {
        if (jsModule is null)
        {
            return;
        }

        await jsModule.InvokeVoidAsync("unregisterMarker", mapId, markerId);
    }

    /// <summary>
    /// Registers a route (a line over the map) drawn from a list of geographic points. Routes
    /// are native MapLibre style layers, so the interop module is always available here.
    /// </summary>
    internal async Task RegisterRouteAsync(string routeId, IReadOnlyList<BbMapCoordinate> points, string color, double width, double opacity)
    {
        if (jsModule is null)
        {
            return;
        }

        await jsModule.InvokeVoidAsync("registerRoute", mapId, routeId, ToCoordinates(points), ToRouteOptions(color, width, opacity));
    }

    /// <summary>
    /// Updates an already registered route's path and appearance.
    /// </summary>
    internal async Task UpdateRouteAsync(string routeId, IReadOnlyList<BbMapCoordinate> points, string color, double width, double opacity)
    {
        if (jsModule is null)
        {
            return;
        }

        await jsModule.InvokeVoidAsync("updateRoute", mapId, routeId, ToCoordinates(points), ToRouteOptions(color, width, opacity));
    }

    /// <summary>
    /// Removes a route from the map.
    /// </summary>
    internal async Task UnregisterRouteAsync(string routeId)
    {
        if (jsModule is null)
        {
            return;
        }

        await jsModule.InvokeVoidAsync("unregisterRoute", mapId, routeId);
    }

    /// <summary>
    /// Tells the interop layer whether a marker's popup is open, so it is kept inside the map
    /// viewport while the dot is visible and follows the dot once it leaves the map.
    /// </summary>
    internal async Task SetMarkerPopupOpenAsync(string markerId, bool open)
    {
        if (!jsInitialized || jsModule is null)
        {
            return;
        }

        await jsModule.InvokeVoidAsync("setMarkerPopupOpen", mapId, markerId, open);
    }

    /// <summary>
    /// Zooms the map in one level.
    /// </summary>
    internal async Task ZoomInAsync()
    {
        if (jsModule is null)
        {
            return;
        }

        await jsModule.InvokeVoidAsync("zoomIn", mapId);
    }

    /// <summary>
    /// Zooms the map out one level.
    /// </summary>
    internal async Task ZoomOutAsync()
    {
        if (jsModule is null)
        {
            return;
        }

        await jsModule.InvokeVoidAsync("zoomOut", mapId);
    }

    /// <summary>
    /// Rotates the map back to north (zero bearing).
    /// </summary>
    internal async Task ResetNorthAsync()
    {
        if (jsModule is null)
        {
            return;
        }

        await jsModule.InvokeVoidAsync("resetNorth", mapId);
    }

    /// <summary>
    /// Locates the device and eases the camera to it at street level.
    /// </summary>
    internal async Task LocateUserAsync()
    {
        if (jsModule is null)
        {
            return;
        }

        await jsModule.InvokeVoidAsync("locateUser", mapId);
    }

    /// <summary>
    /// Toggles the map container between filling the viewport and its normal size.
    /// </summary>
    internal async Task ToggleFullscreenAsync()
    {
        if (jsModule is null)
        {
            return;
        }

        await jsModule.InvokeVoidAsync("toggleFullscreen", mapId);
    }

    /// <summary>
    /// Pushes parameter-driven camera changes down to the map.
    /// </summary>
    private async Task SyncViewAsync()
    {
        if (jsModule is null)
        {
            return;
        }

        if (Center is { } center &&
            (lastLatitude is not { } latitude || lastLongitude is not { } longitude ||
             Math.Abs(latitude - center.Latitude) > CoordinateTolerance ||
             Math.Abs(longitude - center.Longitude) > CoordinateTolerance))
        {
            await jsModule.InvokeVoidAsync("setCenter", mapId, center.Longitude, center.Latitude);

            lastLatitude = center.Latitude;
            lastLongitude = center.Longitude;
        }

        if (Zoom is { } zoom && (lastZoom is not { } previousZoom || Math.Abs(previousZoom - zoom) > ZoomTolerance))
        {
            await jsModule.InvokeVoidAsync("setZoom", mapId, zoom);

            lastZoom = zoom;
        }

        if (Bearing is { } bearing && (lastBearing is not { } previousBearing || Math.Abs(previousBearing - bearing) > BearingTolerance))
        {
            await jsModule.InvokeVoidAsync("setBearing", mapId, bearing);

            lastBearing = bearing;
        }
    }

    /// <summary>
    /// Serializes route points into the GeoJSON [lng, lat] pairs the interop layer expects.
    /// </summary>
    private static double[][] ToCoordinates(IReadOnlyList<BbMapCoordinate> points) =>
        points.Select(point => new[] { point.Longitude, point.Latitude }).ToArray();

    /// <summary>
    /// Bundles a route's appearance into the options object the interop layer understands.
    /// </summary>
    private static Dictionary<string, object?> ToRouteOptions(string color, double width, double opacity) =>
        new()
        {
            ["color"] = color,
            ["width"] = width,
            ["opacity"] = opacity,
        };

    /// <summary>
    /// Receives the camera position from the map after a user pan, zoom or rotation.
    /// </summary>
    [JSInvokable]
    public async Task OnMapViewChanged(double latitude, double longitude, double zoom, double bearing)
    {
        if (Center is { } center &&
            (Math.Abs(center.Latitude - latitude) > CoordinateTolerance ||
             Math.Abs(center.Longitude - longitude) > CoordinateTolerance))
        {
            await CenterChanged.InvokeAsync(new BbMapCoordinate(latitude, longitude));
        }

        if (Zoom is { } currentZoom && Math.Abs(currentZoom - zoom) > ZoomTolerance)
        {
            await ZoomChanged.InvokeAsync(zoom);
        }

        if (Bearing is { } currentBearing && Math.Abs(currentBearing - bearing) > BearingTolerance)
        {
            await BearingChanged.InvokeAsync(bearing);
        }
    }

    /// <summary>
    /// Raised when the map container enters or leaves browser fullscreen.
    /// </summary>
    internal event Action<bool>? FullscreenChanged;

    /// <summary>
    /// Receives the container's browser fullscreen state after a fullscreenchange event, so the
    /// toolbar can swap its maximize/minimize icon, including when the user exits via Esc.
    /// </summary>
    [JSInvokable]
    public void OnMapFullscreenChanged(bool isFullscreen) =>
        FullscreenChanged?.Invoke(isFullscreen);

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (jsModule != null && jsInitialized)
        {
            try
            {
                await jsModule.InvokeVoidAsync("disposeMap", mapId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // Expected during circuit disconnect in Blazor Server - safe to ignore
            }
            catch (ObjectDisposedException)
            {
                // Module already disposed - safe to ignore
            }
            catch (InvalidOperationException)
            {
                // JS interop not available (prerendering) - safe to ignore
            }
        }

        dotNetRef?.Dispose();
        dotNetRef = null;
    }
}
