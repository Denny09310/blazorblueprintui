using BlazorBlueprint.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Components;

public partial class BbMapLibre : ComponentBase, IAsyncDisposable
{
    private IJSObjectReference? jsModule;
    private string mapId = Guid.NewGuid().ToString("N");
    private bool jsInitialized;

    public IJSObjectReference? Map { get; private set; }

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

    private async Task InitializeJsAsync()
    {
        if (jsInitialized)
        {
            return;
        }

        try
        {
            jsModule = await JsModules.GetAsync(JS, "./_content/BlazorBlueprint.Components/js/maplibre-gl-interop.js");

            Map = await jsModule.InvokeAsync<IJSObjectReference>("initializeMapLibre", mapId);

            jsInitialized = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to initialize MapLibre JS: {ex.Message}");
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
    }
}