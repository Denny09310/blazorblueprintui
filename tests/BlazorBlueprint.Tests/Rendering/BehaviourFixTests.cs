using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Extensions;
using BlazorBlueprint.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Tests.Rendering;

/// <summary>
/// Behavioural defects that a rendered check catches: a value that comes back as the wrong type, a
/// colour that resolves the same for every series, an arrow key that wraps past the end of an int.
/// </summary>
public class BehaviourFixTests
{
    private enum Size
    {
        Small,
        Medium,
        Large
    }

    /// <summary>
    /// The change handler converted through Convert.ChangeType, which cannot produce an enum: it
    /// threw, the exception was swallowed, and every choice came back as the enum's default.
    /// </summary>
    [Fact]
    public async Task ChoosingAnEnumOptionReportsThatOption()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            Size? chosen = null;

            await renderer.MountAsync<BbNativeSelect<Size>>(new()
            {
                [nameof(BbNativeSelect<Size>.Value)] = Size.Small,
                [nameof(BbNativeSelect<Size>.ValueChanged)] =
                    EventCallback.Factory.Create<Size>(this, value => chosen = value)
            });

            await renderer.DispatchAsync("onchange", new ChangeEventArgs { Value = "Large" });

            Assert.Equal(Size.Large, chosen);
        });
    }

    /// <summary>
    /// The select rendered an empty value until the user had picked something themselves, so an
    /// initial Value was never shown as the selected option.
    /// </summary>
    [Fact]
    public async Task AnInitialValueIsRenderedAsSelected()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<BbNativeSelect<Size>>(new()
            {
                [nameof(BbNativeSelect<Size>.Value)] = Size.Medium
            });

            Assert.Contains("value=\"Medium\"", renderer.Markup(), StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// An unconfigured key fell back to palette index 0, so a chart whose Config named only some of
    /// its series drew every other one in --chart-1.
    /// </summary>
    [Fact]
    public void AnUnconfiguredSeriesHasNoColourOfItsOwn()
    {
        var config = ChartConfig.Create(("visitors", new ChartSeriesConfig { Color = "var(--chart-2)" }));

        Assert.Equal("var(--chart-2)", config.GetColor("visitors"));
        Assert.Null(config.GetColor("revenue"));

        // The indexed overload still hands out a palette colour when the caller asks for one.
        Assert.Equal("var(--chart-1)", config.GetColor("revenue", 0));
    }

    /// <summary>
    /// The step was unchecked, so one ArrowUp at int.MaxValue wrapped to a negative number and the
    /// clamp that follows dragged it all the way down to Min.
    /// </summary>
    [Fact]
    public async Task SteppingUpAtTheTopOfTheRangeDoesNotWrapToTheBottom()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var value = int.MaxValue;

            var input = await renderer.MountAsync<BbNumericInput<int>>(new()
            {
                [nameof(BbNumericInput<int>.Value)] = value,
                [nameof(BbNumericInput<int>.Min)] = 1,
                [nameof(BbNumericInput<int>.ValueChanged)] =
                    EventCallback.Factory.Create<int>(this, v => value = v)
            });

            await input.JsOnKeyDown("ArrowUp");

            Assert.Equal(int.MaxValue, value);
        });
    }

    /// <summary>
    /// Overlay options were added, not merged, so whichever registration ran last won.
    /// AddBlazorBlueprintComponents calls AddBlazorBlueprintPrimitives itself with no
    /// configuration — which meant configuring the primitives first and the components second
    /// silently threw the configuration away.
    /// </summary>
    [Fact]
    public void OverlayOptionsSurviveWhicheverRegistrationRunsFirst()
    {
        var primitivesFirst = new ServiceCollection();
        primitivesFirst.AddBlazorBlueprintPrimitives(o => o.DefaultStrategy = OverlayRenderingStrategy.Native);
        primitivesFirst.AddBlazorBlueprintComponents();

        var componentsFirst = new ServiceCollection();
        componentsFirst.AddBlazorBlueprintComponents();
        componentsFirst.AddBlazorBlueprintPrimitives(o => o.DefaultStrategy = OverlayRenderingStrategy.Native);

        var throughComponents = new ServiceCollection();
        throughComponents.AddBlazorBlueprintComponents(
            configureOverlays: o => o.DefaultStrategy = OverlayRenderingStrategy.Native);

        foreach (var services in new[] { primitivesFirst, componentsFirst, throughComponents })
        {
            using var provider = services.BuildServiceProvider();
            Assert.Equal(
                OverlayRenderingStrategy.Native,
                provider.GetRequiredService<OverlayRenderingOptions>().DefaultStrategy);
        }
    }

    /// <summary>
    /// BbSelect declared Open and OpenChanged and then bound the primitive to a private field
    /// instead, so neither parameter reached it: <c>@bind-Open</c> could not open the list, and a
    /// user opening it themselves was never reported.
    /// </summary>
    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public async Task SelectPassesItsOpenStateToThePrimitive(bool open, string expected)
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<BbSelect<string>>(new()
            {
                [nameof(BbSelect<string>.Open)] = open,
                [nameof(BbSelect<string>.ChildContent)] = (RenderFragment)(builder =>
                {
                    builder.OpenComponent<BbSelectTrigger<string>>(0);
                    builder.CloseComponent();
                })
            });

            Assert.Contains($"aria-expanded=\"{expected}\"", renderer.Markup(), StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// The resize handle was a plain div with a pointer handler: no role, no tab stop, no keys —
    /// while the documentation described all three. Resizing was unreachable without a pointer.
    /// </summary>
    [Fact]
    public async Task ResizeHandleIsASeparatorThatReportsItsPanel()
    {
        var markup = await ResizableMarkup();

        Assert.Contains("role=\"separator\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-orientation=\"vertical\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-valuenow=\"40\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-valuemin=\"20\"", markup, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"0\"", markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Arrow keys move the handle, Home and End take the panel to its limits, and everything else —
    /// Tab above all — keeps its default action.
    /// </summary>
    [Theory]
    [InlineData("ArrowRight", 45d)]
    [InlineData("ArrowLeft", 35d)]
    [InlineData("PageUp", 60d)]
    [InlineData("Home", 20d)]
    [InlineData("Tab", 40d)]
    public async Task ResizeHandleRespondsToTheKeyboard(string key, double expected)
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            IReadOnlyList<double>? sizes = null;

            await renderer.MountAsync<BbResizablePanelGroup>(new()
            {
                [nameof(BbResizablePanelGroup.OnResizeEnd)] =
                    EventCallback.Factory.Create<PanelResizeEventArgs>(this, e => sizes = e.Sizes),
                [nameof(BbResizablePanelGroup.ChildContent)] = TwoPanels
            });

            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = key });

            if (key == "Tab")
            {
                Assert.Null(sizes);
                return;
            }

            Assert.NotNull(sizes);
            Assert.Equal(expected, sizes![0]);

            // The pair always adds up to what it started with; the space comes from the neighbour.
            Assert.Equal(100d, sizes[0] + sizes[1]);
        });
    }

    private static async Task<string> ResizableMarkup()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        var markup = string.Empty;

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<BbResizablePanelGroup>(new()
            {
                [nameof(BbResizablePanelGroup.ChildContent)] = TwoPanels
            });
            markup = renderer.Markup();
        });

        return markup;
    }

    /// <summary>A 40/60 split with one handle between the panels.</summary>
    private static readonly RenderFragment TwoPanels = builder =>
    {
        builder.OpenComponent<BbResizablePanel>(0);
        builder.AddAttribute(1, nameof(BbResizablePanel.DefaultSize), 40d);
        builder.AddAttribute(2, nameof(BbResizablePanel.MinSize), 20d);
        builder.CloseComponent();

        builder.OpenComponent<BbResizableHandle>(3);
        builder.CloseComponent();

        builder.OpenComponent<BbResizablePanel>(4);
        builder.AddAttribute(5, nameof(BbResizablePanel.DefaultSize), 60d);
        builder.AddAttribute(6, nameof(BbResizablePanel.MinSize), 20d);
        builder.CloseComponent();
    };

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddBlazorBlueprintComponents();
        return services;
    }
}
