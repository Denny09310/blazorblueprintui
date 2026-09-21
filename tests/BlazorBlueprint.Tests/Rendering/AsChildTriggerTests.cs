using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Utilities;
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
/// The triggers with an AsChild mode, and the warning for the way that mode fails silently.
/// </summary>
/// <remarks>
/// <para>
/// With AsChild on — the default for most of these — a trigger renders nothing of its own and
/// hands its behaviour to a child that reads it. Text, an icon or a plain element cannot, so the
/// trigger does nothing and its class goes nowhere. These tests pin the three things that follow:
/// that mistake is reported in Development, the right child is not reported and works, and
/// AsChild="false" gives a real button that carries the class and works.
/// </para>
/// <para>
/// The Development check is cached for the process, so the tests force it rather than register a
/// host environment. The class sets it and puts it back, and xUnit runs one class's tests in turn.
/// </para>
/// </remarks>
public sealed class AsChildTriggerTests : IDisposable
{
    private const string WarningName = "TriggerContextUnconsumed";

    public AsChildTriggerTests()
    {
        DevelopmentEnvironment.OverrideForTests = true;
    }

    public void Dispose() => DevelopmentEnvironment.OverrideForTests = null;

    /// <summary>Every trigger that opens, closes or toggles on a click, by a short name.</summary>
    public static TheoryData<string> ClickTriggers =>
    [
        "Collapsible",
        "Popover",
        "DialogTrigger",
        "DialogClose",
        "SheetTrigger",
        "SheetClose",
        "DropdownMenu",
        "AlertDialogTrigger",
        "AlertDialogAction",
        "AlertDialogCancel",
        "DrawerTrigger",
        "DrawerClose",
    ];

    [Theory]
    [MemberData(nameof(ClickTriggers))]
    public async Task APlainTextChildLogsTheWarning(string name)
    {
        var trigger = Case(name);

        var run = await RenderAsync(trigger, Child.Text, asChild: InAsChildMode(trigger));

        var warning = Assert.Single(run.Warnings);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains(trigger.NamedAs, warning.Message, StringComparison.Ordinal);
        Assert.Contains("set AsChild=\"false\"", warning.Message, StringComparison.Ordinal);
        Assert.Contains("put a BbButton inside it", warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ClickTriggers))]
    public async Task ABbButtonChildIsNotReportedAndWorks(string name)
    {
        var trigger = Case(name);

        var run = await RenderAsync(trigger, Child.Button, asChild: InAsChildMode(trigger));
        await run.Renderer.Dispatcher.InvokeAsync(() => run.Renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Empty(run.Warnings);
        Assert.Equal(new[] { !trigger.StartsOpen }, run.OpenChanges);
    }

    [Theory]
    [MemberData(nameof(ClickTriggers))]
    public async Task AsChildFalseRendersAButtonThatCarriesTheClassAndWorks(string name)
    {
        var trigger = Case(name);

        var run = await RenderAsync(trigger, Child.Text, asChild: false, pageClass: "page-class");
        var markup = await run.Renderer.Dispatcher.InvokeAsync(run.Renderer.Markup);
        await run.Renderer.Dispatcher.InvokeAsync(() => run.Renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Matches("<button[^>]*class=\"[^\"]*page-class", markup);
        Assert.Empty(run.Warnings);
        Assert.Equal(new[] { !trigger.StartsOpen }, run.OpenChanges);
    }

    [Fact]
    public async Task TheWarningNamesTheClassThePageSet()
    {
        // The class is the half of this mistake a reader can see: a trigger styled on the page that
        // shows no styling. The warning has to say where it went.
        var run = await RenderAsync(Case("Collapsible"), Child.Text, pageClass: "flex items-center");

        var warning = Assert.Single(run.Warnings);
        Assert.Contains("It was also given class.", warning.Message, StringComparison.Ordinal);
        Assert.Contains("so it is ignored", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDrawerNamesTheClassThePageSetToo()
    {
        // The drawer takes Class as a parameter of its own rather than as an attribute, so it has to
        // add it to what it reports. Without that it would report nothing.
        var run = await RenderAsync(Case("DrawerTrigger"), Child.Text, asChild: true, pageClass: "w-full");

        var warning = Assert.Single(run.Warnings);
        Assert.Contains("It was also given class.", warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Collapsible")]
    [InlineData("Popover")]
    [InlineData("DropdownMenu")]
    [InlineData("DrawerTrigger")]
    public async Task TheWarningDoesNotBlameTheTriggersOwnStyling(string name)
    {
        // A styled trigger has focus-ring classes of its own. Named in the warning, they would read
        // as something the page did.
        var trigger = Case(name);
        var run = await RenderAsync(trigger, Child.Text, asChild: InAsChildMode(trigger));

        var warning = Assert.Single(run.Warnings);
        Assert.DoesNotContain("It was also given", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NothingIsLoggedOutsideDevelopment()
    {
        DevelopmentEnvironment.OverrideForTests = false;

        var run = await RenderAsync(Case("Collapsible"), Child.Text);

        Assert.Empty(run.Warnings);
    }

    [Theory]
    [InlineData("Tooltip")]
    [InlineData("HoverCard")]
    public async Task TheHoverTriggersUseTheSameWarning(string name)
    {
        var trigger = Case(name);

        var plain = await RenderAsync(trigger, Child.Text, asChild: true);
        var button = await RenderAsync(trigger, Child.Button, asChild: true);

        var warning = Assert.Single(plain.Warnings);
        Assert.Contains(trigger.NamedAs, warning.Message, StringComparison.Ordinal);
        Assert.Empty(button.Warnings);
    }

    [Fact]
    public void OnlyAttributesWithAValueAreNamed()
    {
        Assert.Equal(string.Empty, AsChildDiagnostics.Ignored(null));
        Assert.Equal(string.Empty, AsChildDiagnostics.Ignored(new Dictionary<string, object> { ["class"] = null!, ["id"] = "" }));

        var two = AsChildDiagnostics.Ignored(new Dictionary<string, object> { ["class"] = "x", ["data-test"] = "y" });
        Assert.Contains("given class, data-test.", two, StringComparison.Ordinal);
        Assert.Contains("so they are ignored", two, StringComparison.Ordinal);
    }

    private enum Child
    {
        Text,
        Button,
    }

    /// <summary>One trigger inside its parent, and what a click on it should do.</summary>
    /// <param name="Parent">The component that owns the open state.</param>
    /// <param name="Trigger">The trigger.</param>
    /// <param name="NamedAs">The name the warning gives it.</param>
    /// <param name="StartsOpen">Whether the parent starts open, which a close needs.</param>
    /// <param name="ClassParameter">How the trigger takes a class: its own parameter, or as an attribute.</param>
    /// <param name="AsChildByDefault">Whether the trigger is in AsChild mode unless told otherwise.</param>
    private sealed record TriggerCase(Type Parent, Type Trigger, string NamedAs, bool StartsOpen, string ClassParameter, bool AsChildByDefault = true);

    /// <summary>Leaves a trigger that defaults to AsChild alone, and switches it on for one that does not.</summary>
    private static bool? InAsChildMode(TriggerCase trigger) => trigger.AsChildByDefault ? null : true;

    private static TriggerCase Case(string name) => name switch
    {
        "Collapsible" => new(typeof(BbCollapsible), typeof(BbCollapsibleTrigger), "BbCollapsibleTrigger", false, "Class"),
        "Popover" => new(typeof(BbPopover), typeof(BbPopoverTrigger), "BbPopoverTrigger", false, "class"),
        "DialogTrigger" => new(typeof(BbDialog), typeof(BbDialogTrigger), "BbDialogTrigger", false, "class"),
        "DialogClose" => new(typeof(BbDialog), typeof(BbDialogClose), "BbDialogClose", true, "class"),
        "SheetTrigger" => new(typeof(BbSheet), typeof(BbSheetTrigger), "BbSheetTrigger", false, "class"),
        "SheetClose" => new(typeof(BbSheet), typeof(BbSheetClose), "BbSheetClose", true, "class"),
        "DropdownMenu" => new(typeof(BbDropdownMenu), typeof(BbDropdownMenuTrigger), "BbDropdownMenuTrigger", false, "Class"),
        "AlertDialogTrigger" => new(typeof(BbAlertDialog), typeof(BbAlertDialogTrigger), "BbAlertDialogTrigger", false, "class"),
        "AlertDialogAction" => new(typeof(BbAlertDialog), typeof(BbAlertDialogAction), "BbAlertDialogAction", true, "class"),
        "AlertDialogCancel" => new(typeof(BbAlertDialog), typeof(BbAlertDialogCancel), "BbAlertDialogCancel", true, "class"),
        "DrawerTrigger" => new(typeof(BbDrawer), typeof(BbDrawerTrigger), "BbDrawerTrigger", false, "Class", AsChildByDefault: false),
        "DrawerClose" => new(typeof(BbDrawer), typeof(BbDrawerClose), "BbDrawerClose", true, "Class", AsChildByDefault: false),
        "Tooltip" => new(typeof(BbTooltip), typeof(BbTooltipTrigger), "BbTooltipTrigger", false, "class", AsChildByDefault: false),
        "HoverCard" => new(typeof(BbHoverCard), typeof(BbHoverCardTrigger), "BbHoverCardTrigger", false, "Class"),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private sealed record Run(ComponentTestRenderer Renderer, List<bool> OpenChanges, IReadOnlyList<LogEntry> Warnings);

    private static async Task<Run> RenderAsync(TriggerCase trigger, Child child, bool? asChild = null, string? pageClass = null)
    {
        var logs = new CapturingLoggerFactory();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(logs);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddBlazorBlueprintComponents();

        var provider = services.BuildServiceProvider();
        var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        var openChanges = new List<bool>();

        var parameters = new Dictionary<string, object?>
        {
            ["OpenChanged"] = EventCallback.Factory.Create<bool>(new object(), openChanges.Add),
            ["ChildContent"] = (RenderFragment)(builder => BuildTrigger(builder, trigger, child, asChild, pageClass)),
        };

        if (trigger.Parent == typeof(BbDrawer))
        {
            // The drawer only reports OpenChanged when the page owns Open; left to itself it keeps
            // the state and says nothing. The other parents report either way.
            parameters["Open"] = trigger.StartsOpen;
        }
        else if (trigger.StartsOpen)
        {
            parameters["DefaultOpen"] = true;
        }

        await renderer.Dispatcher.InvokeAsync(() => MountAsync(renderer, trigger.Parent, parameters));

        var warnings = logs.Entries.Where(e => e.Id.Name == WarningName).ToList();
        return new Run(renderer, openChanges, warnings);
    }

    private static Task MountAsync(ComponentTestRenderer renderer, Type parent, Dictionary<string, object?> parameters)
    {
        var mount = typeof(ComponentTestRenderer).GetMethod(nameof(ComponentTestRenderer.MountAsync), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return (Task)mount.MakeGenericMethod(parent).Invoke(renderer, [parameters])!;
    }

    private static void BuildTrigger(RenderTreeBuilder builder, TriggerCase trigger, Child child, bool? asChild, string? pageClass)
    {
        builder.OpenComponent(0, trigger.Trigger);

        if (asChild is { } value)
        {
            builder.AddComponentParameter(1, "AsChild", value);
        }

        if (pageClass is not null)
        {
            builder.AddComponentParameter(2, trigger.ClassParameter, pageClass);
        }

        builder.AddComponentParameter(3, "ChildContent", (RenderFragment)(inner =>
        {
            if (child == Child.Button)
            {
                inner.OpenComponent<BbButton>(0);
                inner.AddComponentParameter(1, nameof(BbButton.ChildContent), (RenderFragment)(text => text.AddContent(0, "Go")));
                inner.CloseComponent();
            }
            else
            {
                inner.AddContent(2, "Go");
            }
        }));

        builder.CloseComponent();
    }

    private sealed record LogEntry(string Category, LogLevel Level, EventId Id, string Message);

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<LogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerFactory owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (owner.Entries)
                {
                    owner.Entries.Add(new LogEntry(category, logLevel, eventId, formatter(state, exception)));
                }
            }
        }
    }
}
