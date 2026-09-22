using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace BlazorBlueprint.Tests.Rendering;

// Keep renderer internals confined to this adapter; tests use the component's public methods.
#pragma warning disable BL0006
internal sealed class ComponentTestRenderer(IServiceProvider services, ILoggerFactory loggerFactory)
    : Renderer(services, loggerFactory)
{
    private readonly List<int> roots = [];
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    protected override void HandleException(Exception exception) => System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();

    internal async Task<T> MountAsync<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        var component = (T)InstantiateComponent(typeof(T));
        var id = AssignRootComponentId(component);
        roots.Add(id);
        await RenderRootComponentAsync(id, ParameterView.FromDictionary(parameters));
        return component;
    }

    internal async Task<IComponent> MountTypeAsync(Type type, Dictionary<string, object?> parameters)
    {
        var component = InstantiateComponent(type);
        var id = AssignRootComponentId(component);
        roots.Add(id);
        await RenderRootComponentAsync(id, ParameterView.FromDictionary(parameters));
        return component;
    }

    /// <summary>
    /// Serialises the rendered tree to HTML-like markup, so tests can assert on what a component
    /// actually put in the DOM: which element an attribute landed on, whether an id was written,
    /// whether an ARIA attribute carries a value. The renderer itself never produces a document,
    /// and the defects these tests guard against are exactly the ones you cannot see from
    /// component state alone.
    /// </summary>
    internal string Markup()
    {
        var builder = new StringBuilder();

        foreach (var id in roots)
        {
            AppendFrames(builder, GetCurrentRenderTreeFrames(id));
        }

        return builder.ToString();
    }

    private void AppendFrames(StringBuilder builder, ArrayRange<RenderTreeFrame> frames)
    {
        var index = 0;

        while (index < frames.Count)
        {
            index = AppendFrame(builder, frames, index);
        }
    }

    private int AppendFrame(StringBuilder builder, ArrayRange<RenderTreeFrame> frames, int index)
    {
        var frame = frames.Array[index];

        switch (frame.FrameType)
        {
            case RenderTreeFrameType.Element:
            {
                var end = index + frame.ElementSubtreeLength;
                var child = index + 1;

                builder.Append('<').Append(frame.ElementName);

                while (child < end && frames.Array[child].FrameType is RenderTreeFrameType.Attribute
                       or RenderTreeFrameType.ElementReferenceCapture)
                {
                    if (frames.Array[child].FrameType == RenderTreeFrameType.Attribute)
                    {
                        AppendAttribute(builder, frames.Array[child]);
                    }

                    child++;
                }

                builder.Append('>');

                while (child < end)
                {
                    child = AppendFrame(builder, frames, child);
                }

                builder.Append("</").Append(frame.ElementName).Append('>');
                return end;
            }

            case RenderTreeFrameType.Text:
                builder.Append(frame.TextContent);
                return index + 1;

            case RenderTreeFrameType.Markup:
                builder.Append(frame.MarkupContent);
                return index + 1;

            case RenderTreeFrameType.Component:
                AppendFrames(builder, GetCurrentRenderTreeFrames(frame.ComponentId));
                return index + frame.ComponentSubtreeLength;

            case RenderTreeFrameType.Region:
            {
                var end = index + frame.RegionSubtreeLength;
                var child = index + 1;

                while (child < end)
                {
                    child = AppendFrame(builder, frames, child);
                }

                return end;
            }

            default:
                return index + 1;
        }
    }

    private static void AppendAttribute(StringBuilder builder, RenderTreeFrame frame)
    {
        switch (frame.AttributeValue)
        {
            case null:
                return;

            // Event handlers have no markup form; writing the delegate would only add noise.
            case Delegate:
            case EventCallback:
                return;

            case bool flag:
                if (flag)
                {
                    builder.Append(' ').Append(frame.AttributeName);
                }

                return;

            default:
                builder.Append(' ').Append(frame.AttributeName)
                    .Append("=\"").Append(frame.AttributeValue).Append('"');
                return;
        }
    }

    /// <summary>
    /// Raises a DOM event against a rendered handler, so keyboard and pointer behaviour can be
    /// tested the way a browser drives it. <paramref name="occurrence"/> picks between several
    /// elements carrying the same handler, in render order.
    /// </summary>
    internal Task DispatchAsync(string attributeName, EventArgs args, int occurrence = 0)
    {
        var handlers = new List<ulong>();

        foreach (var id in roots)
        {
            CollectHandlers(GetCurrentRenderTreeFrames(id), attributeName, handlers);
        }

        if (handlers.Count <= occurrence)
        {
            throw new InvalidOperationException(
                $"No rendered '{attributeName}' handler at index {occurrence}; found {handlers.Count}.");
        }

        return DispatchEventAsync(handlers[occurrence], new EventFieldInfo(), args);
    }

    private void CollectHandlers(ArrayRange<RenderTreeFrame> frames, string attributeName, List<ulong> handlers)
    {
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i];

            if (frame.FrameType == RenderTreeFrameType.Attribute
                && frame.AttributeEventHandlerId != 0
                && string.Equals(frame.AttributeName, attributeName, StringComparison.Ordinal))
            {
                handlers.Add(frame.AttributeEventHandlerId);
            }
            else if (frame.FrameType == RenderTreeFrameType.Component)
            {
                CollectHandlers(GetCurrentRenderTreeFrames(frame.ComponentId), attributeName, handlers);
            }
        }
    }

    internal T FindComponent<T>() where T : IComponent
    {
        var pending = new Stack<int>(roots);
        while (pending.TryPop(out var id))
        {
            var frames = GetCurrentRenderTreeFrames(id);
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames.Array[i];
                if (frame.FrameType == RenderTreeFrameType.Component)
                {
                    if (frame.Component is T match)
                    {
                        return match;
                    }
                    pending.Push(frame.ComponentId);
                }
            }
        }
        throw new InvalidOperationException($"No rendered {typeof(T).Name} component found.");
    }
}
#pragma warning restore BL0006

internal sealed class NoopJavaScript : IJSRuntime, IJSObjectReference
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        ValueTask.FromResult(typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
