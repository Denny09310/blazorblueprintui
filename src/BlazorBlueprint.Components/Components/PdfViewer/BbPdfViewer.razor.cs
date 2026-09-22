using BlazorBlueprint.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Components;

public partial class BbPdfViewer : ComponentBase, IAsyncDisposable
{
    [Parameter, EditorRequired]
    public string? Url { get; set; }

    private ElementReference _canvas;
    private IJSObjectReference? _jsModule;

    private bool _jsInitialized;
    private bool _parametersChanged;
    private string? _lastKnownUrl;

    // === Lifecycle Methods ===

    protected override Task OnParametersSetAsync()
    {
        _parametersChanged = true;

        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await InitializeJsAsync();
        }

        if (_jsInitialized && _parametersChanged)
        {
            _parametersChanged = false;

            await LoadPdfAsync();
        }
    }

    private async Task InitializeJsAsync()
    {
        if (_jsInitialized)
        {
            return;
        }

        try
        {
            _jsModule = await JsModules.GetAsync(
                JS,
                "./_content/BlazorBlueprint.Components/js/pdfjs-interop.js");

            _jsInitialized = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Failed to initialize PdfViewer JS: {ex.Message}");
        }
    }

    private async Task LoadPdfAsync()
    {
        if (_jsModule is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Url))
        {
            _lastKnownUrl = null;

            await _jsModule.InvokeVoidAsync(
                "clear",
                _canvas);

            return;
        }

        if (string.Equals(Url, _lastKnownUrl, StringComparison.Ordinal))
        {
            return;
        }

        _lastKnownUrl = Url;

        try
        {
            await _jsModule.InvokeVoidAsync(
                "load",
                _canvas,
                Url);
        }
        catch (Exception ex) when (
            ex is JSDisconnectedException
            or JSException
            or TaskCanceledException
            or ObjectDisposedException)
        {
            // Safe to ignore during disposal/disconnect.
        }
    }

    // === Dispose ===

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_jsModule is null || !_jsInitialized)
        {
            return;
        }

        try
        {
            await _jsModule.InvokeVoidAsync(
                "dispose",
                _canvas);

            await _jsModule.DisposeAsync();
        }
        catch (Exception ex) when (
            ex is JSDisconnectedException
            or JSException
            or TaskCanceledException
            or ObjectDisposedException
            or InvalidOperationException)
        {
            // Expected during circuit disconnect/prerendering/disposal.
        }
    }
}