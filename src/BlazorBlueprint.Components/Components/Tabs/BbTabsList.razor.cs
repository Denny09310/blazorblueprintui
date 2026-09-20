using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Tabs;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using BlazorBlueprint.Primitives.Services;
using BlazorBlueprint.Primitives.Utilities;

namespace BlazorBlueprint.Components;

/// <summary>
/// The strip of tabs inside a <see cref="BbTabs"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Closable"/>, <see cref="Renamable"/>, <see cref="Reorderable"/> and
/// <see cref="Addable"/> are off by default, and each also needs its callback wired before it
/// draws anything. A flag with nowhere to report to would put an affordance on screen that does
/// nothing when it is used.
/// </para>
/// <para>
/// None of the four changes the tabs. Each asks, and the caller changes the collection the tabs
/// are written from. That is deliberate and matches <c>BbGantt</c>: the order and the names live
/// wherever the caller keeps them, and a component that wrote to its own markup would be guessing.
/// </para>
/// </remarks>
public partial class BbTabsList : IAsyncDisposable
{
    private ElementReference _containerRef;
    private ElementReference reorderRoot;
    private IJSObjectReference? _jsModule;
    private IJSObjectReference? reorderModule;
    private DotNetObjectReference<BbTabsList>? _dotNetRef;
    private DotNetObjectReference<BbTabsList>? reorderRef;
    private bool _isOverflowing;
    private bool _disposed;
    private bool reorderStarted;
    private readonly List<(string Value, string Label)> _responsiveTriggers = new();
    private string _componentId = Guid.NewGuid().ToString("N")[..8];
    private readonly string componentId = IdGenerator.GenerateId("bb-tabs");

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    /// <summary>
    /// The child content to render within the tabs list.
    /// Should contain TabsTrigger components.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Additional CSS classes to apply to the tabs list.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets additional HTML attributes to apply to the root element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// When true, collapses tabs into a Select dropdown when they overflow their container.
    /// </summary>
    [Parameter]
    public bool Responsive { get; set; }

    /// <summary>
    /// Additional CSS classes to apply to the responsive Select fallback.
    /// </summary>
    [Parameter]
    public string? SelectClass { get; set; }

    /// <summary>
    /// Whether every tab carries a close affordance. A tab can override this for itself.
    /// </summary>
    /// <remarks>
    /// Needs <see cref="OnClose"/>. The pointer gets an icon inside the tab; the keyboard gets
    /// Delete or Backspace on the focused tab, which is the route a screen reader announces.
    /// </remarks>
    [Parameter]
    public bool Closable { get; set; }

    /// <summary>
    /// Whether every tab can be renamed in place. A tab can override this for itself.
    /// </summary>
    /// <remarks>
    /// Needs <see cref="OnRename"/>. Double-click a tab, or press F2 on the focused one; Enter
    /// commits, Escape cancels, and an empty box is a cancel.
    /// </remarks>
    [Parameter]
    public bool Renamable { get; set; }

    /// <summary>
    /// Whether every tab can be dragged to a new position. A tab can override this for itself.
    /// </summary>
    /// <remarks>
    /// Needs <see cref="OnMove"/>. The pointer drags; the keyboard uses Ctrl with an arrow key,
    /// which moves the tab one place and does not wrap at either end.
    /// </remarks>
    [Parameter]
    public bool Reorderable { get; set; }

    /// <summary>
    /// Whether a button for adding a tab is drawn after the last one.
    /// </summary>
    /// <remarks>
    /// Needs <see cref="OnAdd"/>. The button sits beside the tablist rather than inside it, so it
    /// is its own tab stop and never pretends to a screen reader that it is a tab.
    /// </remarks>
    [Parameter]
    public bool Addable { get; set; }

    /// <summary>
    /// The accessible name and tooltip for the add button. Defaults to "New tab".
    /// </summary>
    [Parameter]
    public string? AddLabel { get; set; }

    /// <summary>
    /// Invoked when a tab asks to be closed, carrying the tab's value.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnClose { get; set; }

    /// <summary>
    /// Invoked when the add button is pressed.
    /// </summary>
    [Parameter]
    public EventCallback OnAdd { get; set; }

    /// <summary>
    /// Invoked when a tab has been renamed. Set <c>Cancel</c> to refuse it and reopen the editor.
    /// </summary>
    [Parameter]
    public EventCallback<TabRenameContext> OnRename { get; set; }

    /// <summary>
    /// Invoked when a tab is moved. Apply it to your own collection, or do nothing to refuse it.
    /// </summary>
    [Parameter]
    public EventCallback<TabMoveContext> OnMove { get; set; }

    /// <summary>
    /// Cascading parameter to receive the tabs context from the Primitives layer.
    /// </summary>
    [CascadingParameter]
    public TabsContext Context { get; set; } = null!;

    internal bool HasCloseHandler => OnClose.HasDelegate;

    internal bool HasRenameHandler => OnRename.HasDelegate;

    internal bool HasMoveHandler => OnMove.HasDelegate;

    // Responsive mode already has a wrapper of its own and its own branch, so this only covers
    // the plain case: wrap when something needs to sit outside the tablist or be dragged in it.
    private bool NeedsWrapper => !Responsive && ((Addable && OnAdd.HasDelegate) || (Reorderable && HasMoveHandler));

    private string EffectiveAddLabel => AddLabel ?? Localizer["Tabs.Add"];

    private string CssClass => ClassNames.cn(
        "bb:inline-flex bb:h-10 bb:items-center bb:justify-center bb:rounded-md bb:bg-muted bb:p-1 bb:text-muted-foreground",
        Class
    );

    private static string AddCssClass => ClassNames.cn(
        "bb:inline-flex bb:size-7 bb:shrink-0 bb:items-center bb:justify-center bb:rounded-md",
        "bb:text-muted-foreground bb:transition-colors",
        "bb:hover:bg-muted bb:hover:text-foreground",
        "bb:focus-visible:outline-none bb:focus-visible:ring-2 bb:focus-visible:ring-ring bb:focus-visible:ring-offset-2"
    );

    // Use invisible (not hidden/display:none) so the tablist keeps its measured width
    // for the ResizeObserver. pointer-events-none prevents interaction while invisible.
    private string TabListVisibilityClass => _isOverflowing ? "bb:invisible bb:pointer-events-none" : "";

    // The select is absolutely positioned over the tablist area to avoid affecting layout.
    // When not overflowing, it's hidden entirely.
    private string SelectWrapperClass => _isOverflowing
        ? "bb:absolute bb:inset-x-0 bb:top-0"
        : "bb:hidden";

    private string SelectCssClass => ClassNames.cn(
        "bb:w-full",
        SelectClass
    );

    private SelectOption<string>[] SelectOptions =>
        _responsiveTriggers.Select(t => new SelectOption<string>(t.Value, t.Label)).ToArray();

    internal void RegisterResponsiveTrigger(string value, string label)
    {
        var existing = _responsiveTriggers.FindIndex(t => t.Value == value);

        if (existing < 0)
        {
            _responsiveTriggers.Add((value, label));
            return;
        }

        // Updated rather than left alone, or a renamed tab keeps its old name in the responsive
        // Select — visible only once the list overflows, which is the worst way to find it.
        _responsiveTriggers[existing] = (value, label);
    }

    internal void UnregisterResponsiveTrigger(string value) =>
        _responsiveTriggers.RemoveAll(t => t.Value == value);

    private void HandleSelectChange(string? value)
    {
        if (value is not null)
        {
            Context.SetActiveTab(value);
        }
    }

    private Task HandleAddAsync() => OnAdd.InvokeAsync();

    /// <summary>Asks the caller to close a tab.</summary>
    internal Task RequestCloseAsync(string value) => OnClose.InvokeAsync(value);

    /// <summary>Asks the caller to rename a tab.</summary>
    /// <returns><see langword="true"/> when the caller refused it.</returns>
    /// <remarks>
    /// Awaited rather than fired and forgotten, because the answer decides whether the editor
    /// closes. <c>InvokeAsync</c> completes after the handler has run, so <c>Cancel</c> is settled
    /// by the time this returns.
    /// </remarks>
    internal async Task<bool> RequestRenameAsync(string value, string oldLabel, string newLabel)
    {
        var context = new TabRenameContext
        {
            Value = value,
            OldLabel = oldLabel,
            NewLabel = newLabel,
        };

        await OnRename.InvokeAsync(context);
        return context.Cancel;
    }

    /// <summary>
    /// Asks the caller to move a tab by one place, which is the keyboard route.
    /// </summary>
    /// <param name="value">The tab.</param>
    /// <param name="delta">-1 towards the start of the list, 1 towards the end.</param>
    /// <remarks>
    /// The position is read from the DOM rather than held in C#. Blazor moves keyed components on
    /// a reorder without changing a parameter, so nothing in the component tree is told the order
    /// changed; an index kept from registration time is wrong from the first move onwards. One
    /// interop call per keypress is the price, and a keypress is not a per-frame event.
    /// </remarks>
    internal async Task RequestMoveAsync(string value, int delta)
    {
        try
        {
            var module = await GetReorderModuleAsync();
            if (module is null)
            {
                return;
            }

            var at = await module.InvokeAsync<TabPosition>("locate", componentId, value);

            var to = at.Index + delta;

            // No wrapping at either end: a tab that jumped from last place to first would read as
            // a bug whichever way the move was meant.
            if (at.Index < 0 || to < 0 || to >= at.Count)
            {
                return;
            }

            await RaiseMoveAsync(value, at.Index, to);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
        {
            // Circuit disconnected, ignore.
        }
    }

    /// <summary>Where a tab sits, as the drag module reports it.</summary>
    private readonly record struct TabPosition(int Index, int Count);

    /// <summary>Selects the text of a rename box, because a rename usually replaces the name.</summary>
    internal async Task SelectRenameTextAsync(ElementReference input)
    {
        try
        {
            var module = await GetReorderModuleAsync();
            if (module is not null)
            {
                await module.InvokeVoidAsync("selectAll", input);
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
        {
            // Circuit disconnected, ignore. The box is still usable, it just is not preselected.
        }
    }

    /// <summary>
    /// Reports a drag that finished on a new position. Called from the reorder module.
    /// </summary>
    /// <param name="value">The tab that moved.</param>
    /// <param name="oldIndex">The position it was dragged from, counting from zero.</param>
    /// <param name="newIndex">The position it was dropped at, counting from zero.</param>
    [JSInvokable]
    public Task OnTabDropped(string value, int oldIndex, int newIndex) =>
        RaiseMoveAsync(value, oldIndex, newIndex);

    private async Task RaiseMoveAsync(string value, int from, int to)
    {
        // A drop on the tab's own position is not a move. Both routes clamp before they get here,
        // so this only catches the no-op case.
        if (from < 0 || to < 0 || to == from)
        {
            return;
        }

        var context = new TabMoveContext
        {
            Value = value,
            OldIndex = from,
            NewIndex = to,
        };

        await OnMove.InvokeAsync(context);
    }

    [JSInvokable]
    public void OnOverflowChange(bool isOverflowing)
    {
        if (_isOverflowing != isOverflowing)
        {
            _isOverflowing = isOverflowing;
            StateHasChanged();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && Responsive)
        {
            try
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                _jsModule = await JsModules.GetAsync(JSRuntime, "./_content/BlazorBlueprint.Components/js/responsive-tabs.js");
                await _jsModule.InvokeVoidAsync("initialize", _dotNetRef, _componentId, _containerRef);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // Circuit disconnected, ignore
            }
        }

        // Started on the first render that actually draws the wrapper, which is not necessarily
        // the first render: Reorderable can be switched on later.
        if (!reorderStarted && NeedsWrapper && Reorderable && HasMoveHandler)
        {
            reorderStarted = true;

            try
            {
                reorderRef = DotNetObjectReference.Create(this);
                var module = await GetReorderModuleAsync();
                if (module is not null)
                {
                    await module.InvokeVoidAsync("initialize", reorderRoot, reorderRef, componentId);
                }
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // Circuit disconnected, ignore. The keyboard route still works.
                reorderStarted = false;
            }
        }
    }

    private async Task<IJSObjectReference?> GetReorderModuleAsync()
    {
        reorderModule ??= await JsModules.GetAsync(JSRuntime, "./_content/BlazorBlueprint.Components/js/tabs-reorder.js");
        return reorderModule;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);

        if (_jsModule is not null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("dispose", _componentId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // Circuit disconnected, ignore
            }
        }

        if (reorderModule is not null && reorderStarted)
        {
            try
            {
                await reorderModule.InvokeVoidAsync("dispose", componentId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // Circuit disconnected, ignore
            }
        }

        _dotNetRef?.Dispose();
        reorderRef?.Dispose();
    }
}
