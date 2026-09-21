using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace BlazorBlueprint.Tests.Rendering;

/// <summary>
/// <see cref="BbDrawer.OpenChanged"/> in both modes.
/// </summary>
/// <remarks>
/// Left to itself — no <c>Open</c> bound — the drawer used to change its state and report nothing,
/// so a page listening to <c>OpenChanged</c> alone never heard it open or close. Dialog and Sheet
/// report in both modes, and a page has no way to tell which kind of overlay it is listening to.
/// </remarks>
public class DrawerOpenChangedTests
{
    [Fact]
    public async Task ADrawerLeftToItselfReportsOpeningAndClosing()
    {
        var (renderer, drawer, changes) = await RenderAsync(open: null);

        await Click(renderer, Trigger);
        await Click(renderer, Close);

        Assert.Equal(OpenedThenClosed, changes);
        Assert.False(drawer.IsOpen);
    }

    [Fact]
    public async Task ADrawerLeftToItselfReportsOnlyARealChange()
    {
        // Closing a drawer that is already closed is not news. Reporting it would hand the page a
        // "false" it has to recognise as nothing.
        var (renderer, _, changes) = await RenderAsync(open: null);

        await Click(renderer, Close);

        Assert.Empty(changes);
    }

    [Fact]
    public async Task AControlledDrawerReportsAndLeavesTheStateToThePage()
    {
        var (renderer, drawer, changes) = await RenderAsync(open: false);

        await Click(renderer, Trigger);

        Assert.Equal(Opened, changes);
        Assert.False(drawer.IsOpen);
    }

    private const int Trigger = 0;
    private const int Close = 1;

    private static readonly bool[] OpenedThenClosed = [true, false];
    private static readonly bool[] Opened = [true];

    private static Task Click(ComponentTestRenderer renderer, int which) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs(), which));

    private static async Task<(ComponentTestRenderer Renderer, BbDrawer Drawer, List<bool> Changes)> RenderAsync(bool? open)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddBlazorBlueprintComponents();

        var provider = services.BuildServiceProvider();
        var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        var changes = new List<bool>();

        var parameters = new Dictionary<string, object?>
        {
            [nameof(BbDrawer.OpenChanged)] = EventCallback.Factory.Create<bool>(new object(), changes.Add),
            [nameof(BbDrawer.ChildContent)] = (RenderFragment)BuildButtons,
        };

        if (open is { } value)
        {
            parameters[nameof(BbDrawer.Open)] = value;
        }

        var drawer = await renderer.Dispatcher.InvokeAsync(async () => await renderer.MountAsync<BbDrawer>(parameters));
        return (renderer, drawer, changes);
    }

    private static void BuildButtons(RenderTreeBuilder builder)
    {
        builder.OpenComponent<BbDrawerTrigger>(0);
        builder.AddComponentParameter(1, nameof(BbDrawerTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "Open")));
        builder.CloseComponent();

        builder.OpenComponent<BbDrawerClose>(2);
        builder.AddComponentParameter(3, nameof(BbDrawerClose.ChildContent), (RenderFragment)(b => b.AddContent(0, "Close")));
        builder.CloseComponent();
    }
}
