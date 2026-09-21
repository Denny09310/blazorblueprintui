using System.Reflection;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.NavigationMenu;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Tests.Rendering;

public class ApiAuditRegressionTests
{
    private static readonly int[] replacementPageSizes = [42];
    [Theory]
    [InlineData("Combobox", "button")]
    [InlineData("DatePicker", "button")]
    [InlineData("DateRangePicker", "button")]
    [InlineData("DateTimePicker", "button")]
    [InlineData("TimePicker", "button")]
    [InlineData("MultiSelect", "button")]
    [InlineData("Select", "button")]
    [InlineData("NativeSelect", "select")]
    [InlineData("CheckboxGroup", "div")]
    [InlineData("FileUpload", "input")]
    [InlineData("InputOTP", "input")]
    public async Task WrapperNamesReachTheActualControl(string name, string tag)
    {
        var types = new Dictionary<string, Type>
        {
            ["Combobox"] = typeof(BbFormFieldCombobox<string>),
            ["DatePicker"] = typeof(BbFormFieldDatePicker),
            ["DateRangePicker"] = typeof(BbFormFieldDateRangePicker),
            ["DateTimePicker"] = typeof(BbFormFieldDateTimePicker),
            ["TimePicker"] = typeof(BbFormFieldTimePicker),
            ["MultiSelect"] = typeof(BbFormFieldMultiSelect<string>),
            ["Select"] = typeof(BbFormFieldSelect<string>),
            ["NativeSelect"] = typeof(BbFormFieldNativeSelect<string>),
            ["CheckboxGroup"] = typeof(BbFormFieldCheckboxGroup<string>),
            ["FileUpload"] = typeof(BbFormFieldFileUpload),
            ["InputOTP"] = typeof(BbFormFieldInputOTP)
        };
        await Run(async renderer =>
        {
            var parameters = new Dictionary<string, object?> { ["AriaLabel"] = "Audit label" };
            if (name == "Select")
            {
                parameters["Options"] = new[] { new SelectOption<string>("one", "One") };
            }
            await renderer.MountTypeAsync(types[name], parameters);
            Assert.Matches($"<{tag}\\b[^>]*aria-label=\"Audit label\"", renderer.Markup());
        });
    }

    [Fact]
    public async Task EditableDateValidationReachesTheInput()
    {
        await Run(async renderer =>
        {
            await renderer.MountAsync<BbFormFieldDatePicker>(new()
            {
                ["Editable"] = true, ["AriaLabel"] = "Date", ["ErrorText"] = "Choose a date"
            });
            Assert.Matches("<input\\b[^>]*aria-invalid=\"true\"", renderer.Markup());
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(60)]
    public async Task DateTimePickerRejectsInvalidSteps(int step)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Run(async renderer =>
            await renderer.MountAsync<BbDateTimePicker>(new() { ["MinuteStep"] = step })));
    }

    [Theory]
    [InlineData("MinDate")]
    [InlineData("MaxDate")]
    [InlineData("DisabledDates")]
    public async Task NowAndEmptyTimeSteppersCannotChooseBlockedDates(string restriction)
    {
        await Run(async renderer =>
        {
            var changes = 0;
            var parameters = new Dictionary<string, object?>
            {
                ["ValueChanged"] = EventCallback.Factory.Create<DateTime?>(this, _ => changes++)
            };
            parameters[restriction] = restriction switch
            {
                "MinDate" => DateTime.Today.AddDays(1),
                "MaxDate" => DateTime.Today.AddDays(-1),
                _ => (Func<DateTime, bool>)(_ => true)
            };
            var picker = await renderer.MountAsync<BbDateTimePicker>(parameters);
            await Invoke(picker, "SetNow");
            await Invoke(picker, "IncrementMinute");
            Assert.Null(picker.Value);
            Assert.Equal(0, changes);
        });
    }

    [Theory]
    [InlineData("BbCalendar")]
    [InlineData("BbDateRangePicker")]
    public async Task ReplacingDayNamesRefreshesTheCache(string name)
    {
        await Run(async renderer =>
        {
            var type = typeof(BbCalendar).Assembly.GetType($"BlazorBlueprint.Components.{name}")!;
            var component = await renderer.MountTypeAsync(type, new() { ["CustomDayNames"] = Enumerable.Repeat("old", 7).ToArray() });
            var property = type.GetProperty("DayNames", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.All((string[])property.GetValue(component)!, day => Assert.Equal("old", day));
            await component.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["CustomDayNames"] = Enumerable.Repeat("new", 7).ToArray()
            }));
            Assert.All((string[])property.GetValue(component)!, day => Assert.Equal("new", day));
        });
    }

    [Fact]
    public void UnvaluedNavigationContextIsClosed()
    {
        using var root = new NavigationMenuContext(() => { }, true);
        Assert.False(new NavigationMenuItemContext(root, null).IsOpen);
    }

    [Fact]
    public async Task PaginationTemplateReceivesReplacementStateAndOptions()
    {
        await Run(async renderer =>
        {
            var pagination = await renderer.MountAsync<BlazorBlueprint.Primitives.Table.BbTablePagination<object>>(new()
            {
                ["State"] = new PaginationState(),
                ["PaginationTemplate"] = (RenderFragment<BlazorBlueprint.Primitives.Table.PaginationContext>)(context => builder =>
                    builder.AddContent(0, $"{context.State.PageSize}:{context.PageSizeOptions[0]}:{context.ShowPageInfo}"))
            });
            await pagination.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["State"] = new PaginationState { PageSize = 42 }, ["PageSizeOptions"] = replacementPageSizes, ["ShowPageInfo"] = false
            }));
            Assert.Contains("42:42:False", renderer.Markup(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task RequiredChipCannotBeDismissed()
    {
        await Run(async renderer =>
        {
            var set = await renderer.MountAsync<BbChipSet<string>>(new() { ["Required"] = true, ["Value"] = "only" });
            await Invoke(set, "DismissCoreAsync", "only");
            var selected = typeof(BbChipSet<string>).GetMethod("IsSelectedCore", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.True((bool)selected.Invoke(set, ["only"])!);
        });
    }

    [Fact]
    public async Task HeadingLevelChangesSemanticsWithoutChangingDefault()
    {
        await Run(async renderer =>
        {
            var heading = await renderer.MountAsync<BbSectionHeader>(new() { ["Title"] = "Nested section" });
            Assert.Contains("<h2", renderer.Markup(), StringComparison.Ordinal);
            await heading.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { ["HeadingLevel"] = 3 }));
            Assert.Contains("<h3", renderer.Markup(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ContextMenuReadsControlledStateAndReportsUncontrolledChanges()
    {
        await Run(async renderer =>
        {
            var reports = new List<bool>();
            var menu = await renderer.MountAsync<BlazorBlueprint.Primitives.ContextMenu.BbContextMenu>(new()
            {
                ["OpenChanged"] = EventCallback.Factory.Create<bool>(this, reports.Add)
            });
            await menu.OpenAt(12, 34);
            await menu.Close();
            Assert.Collection(reports, value => Assert.True(value), value => Assert.False(value));
            await menu.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Open"] = true }));
            Assert.True(menu.IsOpen);
            await menu.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Open"] = false }));
            Assert.False(menu.IsOpen);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("same")]
    public async Task DockRejectsMissingAndDuplicatePanelIds(string id)
    {
        var exception = await Record.ExceptionAsync(() => Run(async renderer =>
            await renderer.MountAsync<BbDock>(new()
            {
                ["ChildContent"] = (RenderFragment)(builder =>
                {
                    builder.OpenComponent<BbDockPanel>(0);
                    builder.AddAttribute(1, "Id", id);
                    builder.CloseComponent();
                    builder.OpenComponent<BbDockPanel>(2);
                    builder.AddAttribute(3, "Id", id);
                    builder.CloseComponent();
                })
            })));
        if (id.Length == 0)
        {
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains("non-empty", exception.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains("already registered", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task DecorativeImageFallbackIsHiddenAndKeepsAttributes()
    {
        await Run(async renderer =>
        {
            var image = await renderer.MountAsync<BbImage>(new()
            {
                ["Alt"] = "", ["AdditionalAttributes"] = new Dictionary<string, object> { ["data-testid"] = "fallback" }
            });
            await Invoke(image, "HandleError");
            await image.SetParametersAsync(ParameterView.Empty);
            var markup = renderer.Markup();
            Assert.Contains("aria-hidden=\"true\"", markup, StringComparison.Ordinal);
            Assert.Contains("data-testid=\"fallback\"", markup, StringComparison.Ordinal);
            Assert.DoesNotMatch("<div\\b[^>]*role=\"img\"", markup);
        });
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("_self", false)]
    [InlineData("_blank", true)]
    public async Task ExternalIconOnlyAnnouncesANewTabWhenOneWillOpen(string? target, bool announces)
    {
        await Run(async renderer =>
        {
            await renderer.MountAsync<BbLink>(new() { ["Href"] = "/docs", ["Target"] = target, ["ShowExternalIcon"] = true });
            Assert.Equal(announces, renderer.Markup().Contains("opens in a new tab", StringComparison.Ordinal));
        });
    }

    private static Task Invoke(object target, string method, params object?[] args) =>
        (Task)target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args)!;

    private static async Task Run(Func<ComponentTestRenderer, Task> test)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddBlazorBlueprintComponents();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(() => test(renderer));
    }
}
