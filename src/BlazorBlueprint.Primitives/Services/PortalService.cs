using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace BlazorBlueprint.Primitives.Services;

/// <summary>
/// Implementation of portal rendering service for Blazor.
/// Manages a categorized registry of portals that can be rendered at document body level.
/// Portals are split into Container (Dialog, Sheet) and Overlay (Popover, Select, etc.)
/// categories, each with insertion-order tracking for predictable rendering.
/// </summary>
public class PortalService(ILogger<PortalService> logger) : IPortalService
{
    private static readonly Action<ILogger, Exception?> logMissingPortalHost =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1, "MissingPortalHost"),
            "BlazorBlueprint: no <BbPortalHost /> is registered in this render context, so Dialog, Select, " +
            "Popover and other portal-based components cannot render. Check these in order. " +
            "(1) Is `@using BlazorBlueprint.Primitives` in scope where you wrote <BbPortalHost />? " +
            "Without it Razor does not recognise the tag as a component: it emits a literal <bbportalhost> " +
            "element instead, with no build error and no host — and adding @rendermode to it then fails with " +
            "RZ10023 ('only valid when used on a component'), which is the tell. Before v4 the host lived in " +
            "BlazorBlueprint.Primitives.Services; if you are upgrading, that using no longer resolves it. " +
            "(2) Is there a <BbPortalHost /> at all? Add one to MainLayout.razor. " +
            "(3) If it is there and imported, it is rendering somewhere this code cannot reach. In a Blazor " +
            "Web App that is usually because the layout is static while the page is interactive: a routed " +
            "page's layout inherits the page's render mode only when interactivity is applied globally. " +
            "Either set @rendermode on <Routes /> in App.razor, or — only under per-page interactivity, " +
            "where the surrounding layout is static — render <BbPortalHost />, <BbToastProvider /> and " +
            "<BbDialogProvider /> as interactive islands with their own @rendermode. Under " +
            "InteractiveWebAssembly the host must additionally live in an assembly the client project loads — " +
            "a layout in the server project cannot run in the browser, however it is marked.");

    private readonly ConcurrentDictionary<string, PortalEntry> portals = new();
    private long nextOrder;
    private int hostCount;
    private bool hasWarnedMissingHost;

    /// <inheritdoc />
    public bool HasHost => Volatile.Read(ref hostCount) > 0;

    /// <inheritdoc />
    public event Action<PortalCategory>? OnPortalsCategoryChanged;

    /// <inheritdoc />
    public event Action<string>? OnPortalRendered;

    /// <inheritdoc />
    /// <remarks>
    /// Counted rather than flagged. Two hosts are alive at once more often than it looks — the
    /// composite <c>BbPortalHost</c> is itself two category hosts, and during a layout swap the
    /// incoming page's host initialises before the outgoing layout's host disposes. With a plain
    /// boolean the first dispose cleared the flag for the host still running, so a live host
    /// reported itself missing and every subsequent portal logged the warning (#545).
    /// </remarks>
    public void RegisterHost()
    {
        Interlocked.Increment(ref hostCount);

        // A host arriving clears the one-shot warning latch, so a genuine failure later in this
        // circuit is still reported instead of being swallowed by an earlier, unrelated warning.
        hasWarnedMissingHost = false;
    }

    /// <inheritdoc />
    public void UnregisterHost()
    {
        // Clamped: a disposal without a matching registration must not drive the count negative,
        // which would leave HasHost false even after a new host registers.
        if (Interlocked.Decrement(ref hostCount) < 0)
        {
            Interlocked.Exchange(ref hostCount, 0);
        }
    }

    /// <inheritdoc />
    public void NotifyPortalRendered(string portalId) =>
        OnPortalRendered?.Invoke(portalId);

    /// <inheritdoc />
    public void RegisterPortal(string id, RenderFragment content, PortalCategory category)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Portal ID cannot be null or whitespace.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(content);

        if (!HasHost && !hasWarnedMissingHost)
        {
            hasWarnedMissingHost = true;
            logMissingPortalHost(logger, null);
        }

        var entry = new PortalEntry
        {
            Content = content,
            Category = category,
            Order = Interlocked.Increment(ref nextOrder)
        };

        portals[id] = entry;
        OnPortalsCategoryChanged?.Invoke(category);
    }

    /// <inheritdoc />
    public void UnregisterPortal(string id)
    {
        if (portals.TryRemove(id, out var entry))
        {
            OnPortalsCategoryChanged?.Invoke(entry.Category);
        }
    }

    /// <inheritdoc />
    public void UpdatePortalContent(string id, RenderFragment content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!portals.TryGetValue(id, out var entry))
        {
            throw new InvalidOperationException($"Portal with ID '{id}' is not registered.");
        }

        // Update content while preserving category and insertion order
        entry.Content = content;
        OnPortalsCategoryChanged?.Invoke(entry.Category);
    }

    /// <inheritdoc />
    public void RefreshPortal(string id)
    {
        if (portals.TryGetValue(id, out var entry))
        {
            // Notify the category host to re-render WITHOUT replacing the RenderFragment.
            // This allows the existing fragment to pick up new captured values
            // without creating new DOM elements (which would break ElementReference).
            OnPortalsCategoryChanged?.Invoke(entry.Category);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, RenderFragment>> GetPortals(PortalCategory category)
    {
        return portals
            .Where(kvp => kvp.Value.Category == category)
            .OrderBy(kvp => kvp.Value.Order)
            .Select(kvp => new KeyValuePair<string, RenderFragment>(kvp.Key, kvp.Value.Content))
            .ToList();
    }
}
