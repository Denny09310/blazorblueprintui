using Microsoft.Extensions.DependencyInjection;
using BlazorBlueprint.Primitives.Services;

namespace BlazorBlueprint.Primitives.Extensions;

/// <summary>
/// Extension methods for registering BlazorBlueprint.Primitives services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all BlazorBlueprint.Primitives primitive services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOverlays">Optional action to configure overlay rendering options
    /// (e.g. opt the whole app into native <c>&lt;dialog&gt;</c> rendering).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlazorBlueprintPrimitives(
        this IServiceCollection services,
        Action<OverlayRenderingOptions>? configureOverlays = null)
    {
        // Overlay rendering options (global default strategy). Registered as singleton so the
        // resolved default is consistent across all render-mode scopes.
        //
        // Configured in place when a registration already exists, so calling this before
        // AddBlazorBlueprintComponents works as well as calling it after. It used to add a second
        // registration; the container returns the last one, so AddBlazorBlueprintComponents —
        // which calls this method itself, with no configuration — silently replaced the caller's
        // options with the defaults whenever it ran second.
        var overlayOptions = ExistingOverlayOptions(services);

        if (overlayOptions is null)
        {
            overlayOptions = new OverlayRenderingOptions();
            configureOverlays?.Invoke(overlayOptions);
            services.AddSingleton(overlayOptions);
        }
        else
        {
            configureOverlays?.Invoke(overlayOptions);
        }

        // Native overlay service (capability detection + native <dialog> driving).
        services.AddScoped<INativeOverlayService, NativeOverlayService>();

        // Register PortalService as scoped for user isolation in Blazor Server
        // Each user session gets its own portal registry
        services.AddScoped<IPortalService, PortalService>();

        // Register FocusManager as scoped (component-specific state)
        services.AddScoped<IFocusManager, FocusManager>();

        // Register PositioningService as scoped (component-specific state)
        services.AddScoped<IPositioningService, PositioningService>();

        // Register DropdownManagerService as scoped (ensures only one dropdown open at a time per user session)
        services.AddScoped<DropdownManagerService>();

        // Register KeyboardShortcutService as scoped (per user session for global keyboard shortcuts)
        services.AddScoped<IKeyboardShortcutService, KeyboardShortcutService>();

        return services;
    }

    /// <summary>
    /// The <see cref="OverlayRenderingOptions"/> instance already registered, if any.
    /// </summary>
    private static OverlayRenderingOptions? ExistingOverlayOptions(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(OverlayRenderingOptions)
                && services[i].ImplementationInstance is OverlayRenderingOptions options)
            {
                return options;
            }
        }

        return null;
    }
}
