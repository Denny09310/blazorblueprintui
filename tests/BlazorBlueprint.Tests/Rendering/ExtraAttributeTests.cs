using BlazorBlueprint.Components;
using BlazorBlueprint.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Tests.Rendering;

/// <summary>
/// Every public component accepts extra HTML attributes, and a consumer reasonably expects
/// <c>data-testid</c>, <c>title</c> or <c>id</c> to be usable anywhere. Several wrappers splatted
/// their captured attributes onto a primitive root that declared no matching parameter, and Blazor
/// throws <see cref="InvalidOperationException"/> the moment such a splat carries a single entry —
/// so the component worked in every demo and crashed the first time anyone styled or tested it.
/// </summary>
public class ExtraAttributeTests
{
    /// <summary>
    /// Roots that only supply context. They render no element of their own, so the attribute has
    /// nowhere to land — but accepting it must not crash the render.
    /// </summary>
    [Theory]
    [InlineData(typeof(BbDialog))]
    [InlineData(typeof(BbSheet))]
    [InlineData(typeof(BbPopover))]
    [InlineData(typeof(BbHoverCard))]
    [InlineData(typeof(BbAlertDialog))]
    [InlineData(typeof(BlazorBlueprint.Primitives.AlertDialog.BbAlertDialogPortal))]
    public async Task ContextOnlyRootsAcceptExtraAttributes(Type component)
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            // Mounting through a dialog root gives the portal the context it requires.
            RenderFragment child = builder =>
            {
                builder.OpenComponent(0, component);
                builder.AddAttribute(1, "data-testid", "extra");
                builder.CloseComponent();
            };

            await renderer.MountAsync<BlazorBlueprint.Primitives.Dialog.BbDialog>(new()
            {
                [nameof(BlazorBlueprint.Primitives.Dialog.BbDialog.ChildContent)] = child
            });
        });
    }

    /// <summary>
    /// Components that do render an element of their own must put the attribute on it, not drop it.
    /// Each of these is a popover-backed picker whose visible root is its trigger button.
    /// </summary>
    [Theory]
    [InlineData(typeof(BbColorPicker))]
    [InlineData(typeof(BbDateRangePicker))]
    [InlineData(typeof(BbTimePicker))]
    [InlineData(typeof(BbThemeSwitcher))]
    public async Task TriggerBackedComponentsRenderExtraAttributes(Type component)
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment child = builder =>
            {
                builder.OpenComponent(0, component);
                builder.AddAttribute(1, "data-testid", "extra");
                builder.CloseComponent();
            };

            await renderer.MountAsync<CascadingValue<string>>(new()
            {
                [nameof(CascadingValue<string>.Value)] = "unused",
                [nameof(CascadingValue<string>.ChildContent)] = child
            });

            Assert.Contains("data-testid=\"extra\"", renderer.Markup(), StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Components that declared the parameter and then never used it. The attribute was accepted
    /// and quietly discarded, which is worse than refusing it: the consumer's test hook or ARIA
    /// attribute simply was not there, with nothing to say why.
    /// </summary>
    [Fact]
    public async Task TextareaRendersExtraAttributesInBothLayouts()
    {
        Assert.Equal("textarea", await ElementCarrying<BbTextarea>(new() { [nameof(BbTextarea.ShowCharacterCount)] = false }));
        Assert.Equal("textarea", await ElementCarrying<BbTextarea>(new() { [nameof(BbTextarea.ShowCharacterCount)] = true }));
    }

    [Fact]
    public async Task DatePickerRendersExtraAttributesOnItsTrigger()
    {
        Assert.Equal("button", await ElementCarrying<BbDatePicker>(new() { [nameof(BbDatePicker.Editable)] = false }));

        // The editable trigger is an input wrapped in a bordered div; the div is its root.
        Assert.Equal("div", await ElementCarrying<BbDatePicker>(new() { [nameof(BbDatePicker.Editable)] = true }));
    }

    [Fact]
    public async Task InputGroupButtonRendersExtraAttributes() =>
        Assert.Equal("button", await ElementCarrying<BbInputGroupButton>([]));

    /// <summary>
    /// Only the two horizontal orientations carried the attribute; the vertical ones rendered a
    /// second, unsplatted copy of the same checkbox and silently lost it.
    /// </summary>
    [Theory]
    [InlineData(FieldOrientation.Vertical)]
    [InlineData(FieldOrientation.VerticalEnd)]
    [InlineData(FieldOrientation.Horizontal)]
    [InlineData(FieldOrientation.HorizontalEnd)]
    [InlineData(FieldOrientation.Responsive)]
    public async Task FormFieldCheckboxRendersExtraAttributesInEveryOrientation(FieldOrientation orientation) =>
        Assert.Equal("button", await ElementCarrying<BbFormFieldCheckbox>(new()
        {
            [nameof(BbFormFieldCheckbox.Orientation)] = orientation
        }));

    /// <summary>The attribute must survive whether or not the section collapses.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FormSectionRendersExtraAttributesWhetherOrNotItCollapses(bool collapsible) =>
        Assert.Equal("div", await ElementCarrying<BbFormSection>(new()
        {
            [nameof(BbFormSection.Collapsible)] = collapsible
        }));

    /// <summary>
    /// Every other BbFormField* control splats onto the control it wraps. The time picker splatted
    /// onto the surrounding BbField, so an attribute meant for the input landed on the container.
    /// </summary>
    [Fact]
    public async Task FormFieldTimePickerRendersExtraAttributesOnThePicker() =>
        Assert.Equal("button", await ElementCarrying<BbFormFieldTimePicker>([]));

    /// <summary>
    /// Renders the component with one extra attribute and reports the element that carries it, so
    /// a test can state not just that the attribute survived but where it landed.
    /// </summary>
    private static async Task<string> ElementCarrying<T>(Dictionary<string, object?> parameters)
        where T : IComponent
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        var element = string.Empty;

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            parameters["data-testid"] = "extra";
            await renderer.MountAsync<T>(parameters);
            element = OwnerOf(renderer.Markup(), "data-testid=\"extra\"");
        });

        return element;
    }

    /// <summary>Name of the element an attribute was written on, or a description of why not.</summary>
    private static string OwnerOf(string markup, string attribute)
    {
        var at = markup.IndexOf(attribute, StringComparison.Ordinal);

        if (at < 0)
        {
            return $"(not rendered at all) {markup}";
        }

        var open = markup.LastIndexOf('<', at);
        var name = markup[(open + 1)..].Split(' ', '>')[0];
        return name;
    }

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
