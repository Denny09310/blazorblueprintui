using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace BlazorBlueprint.Components;

/// <summary>
/// Full-card trigger overlay for an attachment card.
/// </summary>
public partial class BbAttachmentTrigger : ComponentBase
{
    /// <summary>
    /// Gets or sets an element type to render as (for example, "a" or "button").
    /// </summary>
    [Parameter]
    public string? AsChild { get; set; }

    /// <summary>
    /// Gets or sets href when rendering as an anchor.
    /// </summary>
    [Parameter]
    public string? Href { get; set; }

    /// <summary>
    /// Gets or sets custom classes for the trigger element.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Captures additional attributes for the trigger element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => ClassNames.cn(
        "absolute inset-0 z-10 outline-none",
        Class
    );

    private Type GetElementType()
    {
        return AsChild?.ToLower(CultureInfo.InvariantCulture) switch
        {
            "a" => typeof(AnchorElement),
            _ => typeof(ButtonElement)
        };
    }

    private Dictionary<string, object> GetElementAttributes()
    {
        var attributes = new Dictionary<string, object>
        {
            ["class"] = CssClass,
            ["data-slot"] = "attachment-trigger"
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

    private sealed class AnchorElement : ComponentBase
    {
        [Parameter] public string? Class { get; set; }
        [Parameter] public string? DataSlot { get; set; }
        [Parameter] public string? Href { get; set; }

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
            builder.CloseElement();
        }
    }

    private sealed class ButtonElement : ComponentBase
    {
        [Parameter] public string? Class { get; set; }
        [Parameter] public string? DataSlot { get; set; }

        [Parameter(CaptureUnmatchedValues = true)]
        public Dictionary<string, object>? Attributes { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "type", "button");
            builder.AddAttribute(2, "class", Class);
            builder.AddAttribute(3, "data-slot", DataSlot);
            builder.AddMultipleAttributes(4, Attributes);
            builder.CloseElement();
        }
    }
}
