using Microsoft.Extensions.Logging;

namespace BlazorBlueprint.Primitives.Utilities;

/// <summary>
/// Shared handling for primitive root components that only supply context and render no element
/// of their own (Dialog, Sheet, Popover, HoverCard, and the portals).
/// <para>
/// Those roots still declare a <c>CaptureUnmatchedValues</c> parameter, because a styled wrapper
/// splats its own <c>AdditionalAttributes</c> onto them and splatting onto a component without
/// such a parameter throws <see cref="InvalidOperationException"/>. The attributes have nowhere
/// to land, so they are accepted and reported once per component instance instead of being
/// dropped silently.
/// </para>
/// </summary>
internal static class ContextRootAttributes
{
    /// <summary>
    /// Logs a one-time warning when extra HTML attributes were supplied to a context-only root.
    /// </summary>
    /// <param name="logger">Logger used to report the warning.</param>
    /// <param name="alreadyWarned">Whether this component instance already warned.</param>
    /// <param name="componentName">Name of the root component, e.g. <c>BbDialog</c>.</param>
    /// <param name="suggestion">Where the consumer should put the attributes instead.</param>
    /// <param name="attributes">The captured attributes, if any.</param>
    /// <returns><c>true</c> once a warning has been logged, so the caller can store it.</returns>
    public static bool WarnOnce(
        ILogger logger,
        bool alreadyWarned,
        string componentName,
        string suggestion,
        IReadOnlyDictionary<string, object>? attributes)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (alreadyWarned || attributes is not { Count: > 0 })
        {
            return alreadyWarned;
        }

        noEffectWarning(logger, componentName, string.Join(", ", attributes.Keys), suggestion, null);

        return true;
    }

    private static readonly Action<ILogger, string, string, string, Exception?> noEffectWarning =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Warning,
            new EventId(1, nameof(ContextRootAttributes)),
            "{ComponentName}: the extra HTML attributes [{AttributeNames}] have no effect. " +
            "It renders no element of its own \u2014 it only supplies context to its children. {Suggestion}");
}
