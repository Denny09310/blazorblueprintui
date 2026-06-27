using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// Footer slot displayed below a message surface.
/// </summary>
public partial class BbMessageFooter : ComponentBase
{
    /// <summary>
    /// Gets or sets custom classes for the footer.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets footer content.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Captures additional attributes for the footer container.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    [CascadingParameter]
    private BbMessage? Message { get; set; }

    private string CssClass => ClassNames.cn(
        "flex max-w-full min-w-0 items-center px-3 text-xs font-medium text-muted-foreground group-has-data-[variant=ghost]/message:px-0 group-data-[align=end]/message:justify-end",
        Message?.Align == MessageAlign.End ? "self-end text-right" : "self-start text-left",
        Class
    );
}
