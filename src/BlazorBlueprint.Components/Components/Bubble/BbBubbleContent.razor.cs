using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace BlazorBlueprint.Components;

/// <summary>
/// Content surface wrapper for <see cref="BbBubble"/>.
/// </summary>
public partial class BbBubbleContent : ComponentBase
{
    /// <summary>
    /// Gets or sets an element type to render as (for example, "a" or "button").
    /// </summary>
    [Parameter]
    public string? AsChild { get; set; }

    /// <summary>
    /// Gets or sets href when rendering an anchor element.
    /// </summary>
    [Parameter]
    public string? Href { get; set; }

    /// <summary>
    /// Gets or sets custom classes for the bubble content.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets content inside the bubble surface.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Captures additional attributes for the content element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => ClassNames.cn(
        "w-fit max-w-full min-w-0 overflow-hidden rounded-3xl border border-transparent px-3 py-2.5 text-sm leading-relaxed wrap-break-word group-data-[align=end]/bubble:self-end [button]:text-left [button,a]:transition-colors [button,a]:outline-none [button,a]:focus-visible:border-ring [button,a]:focus-visible:ring-3 [button,a]:focus-visible:ring-ring/30 group-data-[variant=ghost]/bubble:border-0",
        Class
    );

    private Type GetElementType()
    {
        return AsChild?.ToLower(CultureInfo.InvariantCulture) switch
        {
            "a" => typeof(AnchorElement),
            "button" => typeof(ButtonElement),
            _ => typeof(DivElement)
        };
    }

    private Dictionary<string, object> GetElementAttributes()
    {
        var attributes = new Dictionary<string, object>
        {
            ["class"] = CssClass,
            ["data-slot"] = "bubble-content",
            ["ChildContent"] = (object?)ChildContent!
        };

        if (!string.IsNullOrWhiteSpace(Href) && string.Equals(AsChild, "a", StringComparison.OrdinalIgnoreCase))
        {
            attributes["href"] = Href;
        }

        if (AdditionalAttributes != null)
        {
            foreach (var attribute in AdditionalAttributes)
            {
                attributes[attribute.Key] = attribute.Value;
            }
        }

        return attributes;
    }

    private sealed class DivElement : ComponentBase
    {
        [Parameter] public string? Class { get; set; }
        [Parameter] public string? DataSlot { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }

        [Parameter(CaptureUnmatchedValues = true)]
        public Dictionary<string, object>? Attributes { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", Class);
            builder.AddAttribute(2, "data-slot", DataSlot);
            builder.AddMultipleAttributes(3, Attributes);
            builder.AddContent(4, ChildContent);
            builder.CloseElement();
        }
    }

    private sealed class AnchorElement : ComponentBase
    {
        [Parameter] public string? Class { get; set; }
        [Parameter] public string? DataSlot { get; set; }
        [Parameter] public string? Href { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }

        [Parameter(CaptureUnmatchedValues = true)]
        public Dictionary<string, object>? Attributes { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "a");
            builder.AddAttribute(1, "class", Class);
            builder.AddAttribute(2, "data-slot", DataSlot);
            if (!string.IsNullOrWhiteSpace(Href))
            {
                builder.AddAttribute(3, "href", Href);
            }

            builder.AddMultipleAttributes(4, Attributes);
            builder.AddContent(5, ChildContent);
            builder.CloseElement();
        }
    }

    private sealed class ButtonElement : ComponentBase
    {
        [Parameter] public string? Class { get; set; }
        [Parameter] public string? DataSlot { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }

        [Parameter(CaptureUnmatchedValues = true)]
        public Dictionary<string, object>? Attributes { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "type", "button");
            builder.AddAttribute(2, "class", Class);
            builder.AddAttribute(3, "data-slot", DataSlot);
            builder.AddMultipleAttributes(4, Attributes);
            builder.AddContent(5, ChildContent);
            builder.CloseElement();
        }
    }
}
