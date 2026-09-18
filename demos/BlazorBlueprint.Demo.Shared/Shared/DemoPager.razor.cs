using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace BlazorBlueprint.Demo.Shared;

/// <summary>
/// Header controls that step to the previous and next component demo.
/// </summary>
/// <remarks>
/// The sequence is <see cref="ComponentCatalog.DemoPages"/>, the same flattened list the sidebar,
/// the component homepage and the command search already walk, so "next" always means the next row
/// the reader can see in the sidebar. The pager renders nothing off that list — on the homepage, a
/// guide or a primitive page there is no meaningful neighbour to offer.
/// </remarks>
public partial class DemoPager : IDisposable
{
    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    private ComponentCatalog.Entry? _previous;
    private ComponentCatalog.Entry? _next;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        NavigationManager.LocationChanged += OnLocationChanged;
        UpdateNeighbours();
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        UpdateNeighbours();
        StateHasChanged();
    }

    private void UpdateNeighbours()
    {
        var pages = ComponentCatalog.DemoPages;
        var current = "/" + NavigationManager.ToBaseRelativePath(NavigationManager.Uri)
            .Split('?')[0].Split('#')[0].TrimEnd('/');

        var index = -1;
        for (var i = 0; i < pages.Count; i++)
        {
            if (string.Equals(pages[i].Url, current, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        _previous = index > 0 ? pages[index - 1] : null;
        _next = index >= 0 && index < pages.Count - 1 ? pages[index + 1] : null;
    }

    private void Go(ComponentCatalog.Entry? entry)
    {
        if (entry is not null)
        {
            NavigationManager.NavigateTo(entry.Url);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
        GC.SuppressFinalize(this);
    }
}
