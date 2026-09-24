using BlazorBlueprint.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Components;

public partial class BbMapLibre : ComponentBase, IAsyncDisposable
{
    private ElementReference _mapEl;
    private IJSObjectReference? _jsModule;
    private bool _jsInitialized;

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
        if (_jsInitialized)
        {
            return;
        }

        _jsModule = await JsModules.GetAsync(JS, "./_content/BlazorBlueprint.Components/js/maplibre-gl-interop.js");

        Map = await _jsModule.InvokeAsync<IJSObjectReference>("initialize", _mapEl);

        _jsInitialized = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsModule != null && _jsInitialized)
        {
            try
            {
                // call js-side dispose
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

        GC.SuppressFinalize(this);
    }
}