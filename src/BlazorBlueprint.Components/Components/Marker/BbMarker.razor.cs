using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace BlazorBlueprint.Components;

/// <summary>
/// Displays an inline conversation marker such as status updates, separators, or bordered notes.
/// </summary>
public partial class BbMarker : ComponentBase
{
    /// <summary>
    /// Gets or sets the marker visual variant.
    /// </summary>
    [Parameter]
    public MarkerVariant Variant { get; set; } = MarkerVariant.Default;

    /// <summary>
    /// Gets or sets the element type to render as (for example, "a" or "button").
    /// </summary>
    [Parameter]
    public string? AsChild { get; set; }

    /// <summary>
    /// Gets or sets the href when rendering as an anchor.
    /// </summary>
    [Parameter]
    public string? Href { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes to apply to the marker root.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets marker content.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Captures unmatched attributes for the rendered element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => ClassNames.cn(
        "group/marker relative flex min-h-4 w-full items-center gap-2 text-left text-sm text-muted-foreground [&_svg:not([class*='size-'])]:size-4 [a]:underline [a]:underline-offset-3 [a]:hover:text-foreground",
        Variant switch
        {
            MarkerVariant.Border => "border-b border-border pb-2",
            MarkerVariant.Separator => "w-full items-center justify-center text-xs uppercase tracking-wide",
            _ => null
        },
        Variant == MarkerVariant.Separator
            ? "before:mr-1 before:h-px before:min-w-0 before:flex-1 before:bg-border after:ml-1 after:h-px after:min-w-0 after:flex-1 after:bg-border"
            : null,
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
            ["data-slot"] = "marker",
            ["data-variant"] = Variant.ToString().ToLowerInvariant(),
            ["ChildContent"] = ChildContent!
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
        [Parameter] public string? @class { get; set; }
        [Parameter] public string? DataSlot { get; set; }
        [Parameter] public string? DataVariant { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }
        [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? Attributes { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", @class);
            builder.AddAttribute(2, "data-slot", DataSlot);
            builder.AddAttribute(3, "data-variant", DataVariant);
            builder.AddMultipleAttributes(4, Attributes);
            builder.AddContent(5, ChildContent);
            builder.CloseElement();
        }
    }

    private sealed class AnchorElement : ComponentBase
    {
        [Parameter] public string? @class { get; set; }
        [Parameter] public string? DataSlot { get; set; }
        [Parameter] public string? DataVariant { get; set; }
        [Parameter] public string? href { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }
        [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? Attributes { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "a");
            builder.AddAttribute(1, "class", @class);
            builder.AddAttribute(2, "data-slot", DataSlot);
            builder.AddAttribute(3, "data-variant", DataVariant);
            if (!string.IsNullOrWhiteSpace(href))
            {
                builder.AddAttribute(4, "href", href);
            }

            builder.AddMultipleAttributes(5, Attributes);
            builder.AddContent(6, ChildContent);
            builder.CloseElement();
        }
    }

    private sealed class ButtonElement : ComponentBase
    {
        [Parameter] public string? @class { get; set; }
        [Parameter] public string? DataSlot { get; set; }
        [Parameter] public string? DataVariant { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }
        [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? Attributes { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "type", "button");
            builder.AddAttribute(2, "class", @class);
            builder.AddAttribute(3, "data-slot", DataSlot);
            builder.AddAttribute(4, "data-variant", DataVariant);
            builder.AddMultipleAttributes(5, Attributes);
            builder.AddContent(6, ChildContent);
            builder.CloseElement();
        }
    }
}
