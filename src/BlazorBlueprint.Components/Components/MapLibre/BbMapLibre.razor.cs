using BlazorBlueprint.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Components;

public partial class BbMapLibre : ComponentBase, IAsyncDisposable
{
    private const double CoordinateTolerance = 0.0000001;
    private const double ZoomTolerance = 0.0001;

    private IJSObjectReference? jsModule;
    private string mapId = Guid.NewGuid().ToString("N");
    private bool jsInitialized;
    private DotNetObjectReference<BbMapLibre>? dotNetRef;
    private double? lastLatitude;
    private double? lastLongitude;
    private double? lastZoom;
    private readonly List<(string markerId, double lng, double lat)> pendingMarkers = new();

    public IJSObjectReference? Map { get; private set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public BbMapCoordinate? Center { get; set; }

    [Parameter]
    public EventCallback<BbMapCoordinate> CenterChanged { get; set; }

    [Parameter]
    public double? Zoom { get; set; }

    [Parameter]
    public EventCallback<double> ZoomChanged { get; set; }

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

            Map = await jsModule.InvokeAsync<IJSObjectReference>("initializeMapLibre", mapId, dotNetRef, options);

            lastLatitude = Center?.Latitude;
            lastLongitude = Center?.Longitude;
            lastZoom = Zoom;

            jsInitialized = true;

            foreach (var (markerId, lng, lat) in pendingMarkers)
            {
                await jsModule.InvokeVoidAsync("registerMarker", mapId, markerId, lng, lat);
            }

            pendingMarkers.Clear();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to initialize MapLibre JS: {ex.Message}");
        }
    }

    /// <summary>
    /// Registers a marker with the map. Registration is deferred until the map's JS is ready.
    /// </summary>
    internal async Task RegisterMarkerAsync(string markerId, double lng, double lat)
    {
        if (!jsInitialized || jsModule is null)
        {
            pendingMarkers.Add((markerId, lng, lat));
            return;
        }

        await jsModule.InvokeVoidAsync("registerMarker", mapId, markerId, lng, lat);
    }

    /// <summary>
    /// Moves an already registered marker to a new position.
    /// </summary>
    internal async Task UpdateMarkerAsync(string markerId, double lng, double lat)
    {
        if (!jsInitialized || jsModule is null)
        {
            for (var i = 0; i < pendingMarkers.Count; i++)
            {
                if (pendingMarkers[i].markerId == markerId)
                {
                    pendingMarkers[i] = (markerId, lng, lat);
                    break;
                }
            }

            return;
        }

        await jsModule.InvokeVoidAsync("updateMarker", mapId, markerId, lng, lat);
    }

    /// <summary>
    /// Removes a marker from the map.
    /// </summary>
    internal async Task UnregisterMarkerAsync(string markerId)
    {
        pendingMarkers.RemoveAll(marker => marker.markerId == markerId);

        if (!jsInitialized || jsModule is null)
        {
            return;
        }

        await jsModule.InvokeVoidAsync("unregisterMarker", mapId, markerId);
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
    }

    /// <summary>
    /// Receives the camera position from the map after a user pan, zoom or rotation.
    /// </summary>
    [JSInvokable]
    public async Task OnMapViewChanged(double latitude, double longitude, double zoom)
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
    }

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
