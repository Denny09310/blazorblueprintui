using Microsoft.Extensions.Logging;

namespace BlazorBlueprint.Primitives.Utilities;

/// <summary>
/// How an AsChild trigger describes itself in the warning it logs when nothing inside it read its
/// <see cref="TriggerContext"/>.
/// </summary>
/// <param name="Name">The trigger's name as it is written in markup, such as <c>BbCollapsibleTrigger</c>.</param>
/// <param name="Action">What nothing can now do, such as <c>toggle this collapsible</c>.</param>
/// <param name="Element">The element the trigger renders when AsChild is off, such as <c>&lt;button&gt;</c>.</param>
public sealed record AsChildTriggerDescription(string Name, string Action, string Element);

/// <summary>
/// The development-time warning for the one way AsChild fails silently, for any trigger that
/// cascades a <see cref="TriggerContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// With AsChild on, a trigger renders no element and no handlers of its own. It cascades a
/// <see cref="TriggerContext"/> and leaves the child to wire itself up. A child that never reads
/// the context — text, an icon, a plain <c>span</c>, even a plain <c>button</c> — leaves nothing
/// that can open or toggle anything, and the class and attributes given to the trigger have no
/// element to land on. Nothing fails, so nothing says so. This does.
/// </para>
/// <para>
/// Every trigger in the library with an AsChild mode uses it, and it is public so a trigger built
/// outside the library can warn the same way. Call it from the trigger's <c>OnAfterRender</c> on
/// the first render, with the context the render cascaded. Children read the context while they
/// render, in the same render batch, so by then the answer is settled; checking only the first
/// render means the trigger warns at most once.
/// </para>
/// <para>
/// Consumption is recorded by the context itself: any read of its members marks it, and a child
/// that only reads it inside event handlers can say so with
/// <see cref="TriggerContext.NotifyConsumed"/>. Nothing is logged outside the Development
/// environment.
/// </para>
/// </remarks>
public static class AsChildDiagnostics
{
    private static readonly Action<ILogger, string, string, string, string, Exception?> LogUnconsumed =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Warning,
            new EventId(1, "TriggerContextUnconsumed"),
            "{Trigger} has AsChild=\"true\", but nothing inside it read its TriggerContext, so nothing can {Action}. " +
            "In this mode the trigger renders no element and no event handlers of its own. " +
            "Either set AsChild=\"false\", so the trigger renders its own {Element} around the content - which is what " +
            "text, plain markup or a bare icon such as LucideIcon needs - or put a BbButton inside it, which reads the " +
            "context and becomes the trigger.{Ignored} " +
            "A custom child that only reads the context inside event handlers can call TriggerContext.NotifyConsumed() " +
            "to say so. This warning is only logged in the Development environment.");

    /// <summary>
    /// Logs a warning, in the Development environment only, when an AsChild trigger rendered a
    /// context that nothing read.
    /// </summary>
    /// <typeparam name="TTrigger">The trigger, which names the log category.</typeparam>
    /// <param name="services">The application's services, for the environment and the logger.</param>
    /// <param name="trigger">How the trigger describes itself.</param>
    /// <param name="asChild">Whether the trigger is in AsChild mode.</param>
    /// <param name="context">The context the trigger cascaded in its last render, if any.</param>
    /// <param name="attributes">The attributes the trigger was given, which AsChild mode drops.</param>
    /// <remarks>
    /// Pass every attribute the trigger would have put on its own element, class included. Only
    /// those with a value are named, so a styled trigger should hand over the class the page set
    /// rather than its own styling, which would read as something the page did. The logger is
    /// resolved only when there is something to log, so a trigger that is used correctly — nearly
    /// all of them — takes no logger at all.
    /// </remarks>
    /// <example>
    /// <code>
    /// private static readonly AsChildTriggerDescription Description =
    ///     new("MyTrigger", "open this panel", "&lt;button&gt;");
    ///
    /// protected override void OnAfterRender(bool firstRender)
    /// {
    ///     if (firstRender)
    ///     {
    ///         AsChildDiagnostics.WarnIfUnconsumed&lt;MyTrigger&gt;(
    ///             Services, Description, AsChild, lastTriggerContext, AdditionalAttributes);
    ///     }
    /// }
    /// </code>
    /// </example>
    public static void WarnIfUnconsumed<TTrigger>(
        IServiceProvider services,
        AsChildTriggerDescription trigger,
        bool asChild,
        TriggerContext? context,
        IReadOnlyDictionary<string, object>? attributes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(trigger);

        if (!asChild || context is null || context.WasConsumed)
        {
            return;
        }

        if (!DevelopmentEnvironment.IsDevelopment(services))
        {
            return;
        }

        if (services.GetService(typeof(ILoggerFactory)) is not ILoggerFactory factory)
        {
            return;
        }

        LogUnconsumed(factory.CreateLogger<TTrigger>(), trigger.Name, trigger.Action, trigger.Element, Ignored(attributes), null);
    }

    /// <summary>
    /// Names the attributes AsChild mode dropped, as a sentence to add to the warning.
    /// </summary>
    /// <param name="attributes">The attributes the trigger was given.</param>
    /// <returns>The sentence with a leading space, or empty where nothing was dropped.</returns>
    /// <remarks>
    /// Only attributes with a value count. A styled trigger hands its primitive a null class in
    /// AsChild mode rather than its own styling, so what is named here is only what the page set.
    /// </remarks>
    internal static string Ignored(IReadOnlyDictionary<string, object>? attributes)
    {
        if (attributes is null)
        {
            return string.Empty;
        }

        var names = attributes
            .Where(a => a.Value is not null && a.Value is not string { Length: 0 })
            .Select(a => a.Key)
            .ToList();

        return names.Count == 0
            ? string.Empty
            : $" It was also given {string.Join(", ", names)}. With AsChild=\"true\" there is no element to put " +
              (names.Count == 1 ? "it on, so it is ignored." : "them on, so they are ignored.");
    }
}
