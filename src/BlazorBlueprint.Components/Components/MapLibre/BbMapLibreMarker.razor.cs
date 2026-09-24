using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Components;

/// <summary>
/// A colored dot pinned to a geographic position on top of a <see cref="BbMapLibre"/> map.
/// The interop layer projects the coordinate to pixels and moves the element with CSS
/// transforms as the camera moves, so Blazor keeps ownership of its markup.
/// When <see cref="ChildContent"/> is provided, clicking the dot toggles a popup card
/// anchored above the marker; the popup travels with the marker as the map moves.
/// </summary>
public partial class BbMapLibreMarker : ComponentBase, IAsyncDisposable
{
    private const double CoordinateTolerance = 0.0000001;

    private readonly string markerId = Guid.NewGuid().ToString("N");
    private bool registered;
    private bool popupOpen;
    private bool lastPopupOpen;
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

    /// <summary>
    /// Popup content shown above the dot when it is clicked. When omitted the marker
    /// behaves as a plain dot with no popup interaction.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private static string MarkerStyle =>
        "visibility:hidden; transform:translate(0,0);";

    private static string PopupStyle =>
        "visibility:hidden;";

    private string DotStyle =>
        $"background-color:{Color};";

    // The wrapper stacks above the other markers while its popup is open so the
    // popup never renders underneath a neighbouring dot.
    private string zIndexClass => popupOpen && ChildContent is not null ? "bb:z-50" : "bb:z-[1]";

    private void TogglePopup()
    {
        if (ChildContent is null)
        {
            return;
        }

        popupOpen = !popupOpen;
    }

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

        if (popupOpen != lastPopupOpen)
        {
            lastPopupOpen = popupOpen;
            await Map.SetMarkerPopupOpenAsync(markerId, popupOpen);
        }
    }

    private bool CoordinatesChanged() =>
        lastLatitude is not { } latitude || lastLongitude is not { } longitude ||
         Math.Abs(latitude - Latitude) > CoordinateTolerance ||
         Math.Abs(longitude - Longitude) > CoordinateTolerance;

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