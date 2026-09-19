using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Primitives;

/// <summary>
/// Sets the writing direction for its content, so the library's layout mirrors for a
/// right-to-left language.
/// </summary>
/// <remarks>
/// <para>
/// Wrap the application's layout in it, including <see cref="BbPortalHost"/> — an overlay that
/// renders through the portal host needs the direction too. An overlay whose host sits outside the
/// provider still gets it: the floating element copies the direction from its trigger when it
/// opens, the same way it copies a local theme.
/// </para>
/// <para>
/// The provider renders no box of its own (<c>display: contents</c>), so adding it does not change
/// any layout. It writes a <c>dir</c> attribute, which is what the library's logical CSS
/// properties respond to, and cascades a <see cref="DirectionContext"/> for the decisions CSS
/// cannot make, such as which way an arrow key moves.
/// </para>
/// <para>
/// A component used with no provider follows <see cref="System.Globalization.CultureInfo.CurrentCulture"/>,
/// which is the behaviour the library had before this component existed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BbDirectionProvider Direction="TextDirection.RightToLeft"&gt;
///     @Body
///     &lt;BbPortalHost /&gt;
/// &lt;/BbDirectionProvider&gt;
/// </code>
/// </example>
public partial class BbDirectionProvider : ComponentBase
{
    private DirectionContext context = new() { Direction = TextDirection.Auto };

    /// <summary>
    /// Gets or sets the writing direction. Defaults to <see cref="TextDirection.Auto"/>, which
    /// follows the current culture.
    /// </summary>
    [Parameter]
    public TextDirection Direction { get; set; } = TextDirection.Auto;

    /// <summary>
    /// Gets or sets the content the direction applies to.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // A new instance per change, so the cascade notifies descendants; the object is otherwise
        // immutable and shared for the lifetime of the provider.
        if (context.Direction != Direction)
        {
            context = new DirectionContext { Direction = Direction };
        }
    }
}
