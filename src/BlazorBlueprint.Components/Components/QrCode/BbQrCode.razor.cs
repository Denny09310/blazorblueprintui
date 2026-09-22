using System.Globalization;
using System.Net;
using System.Text;
using BlazorBlueprint.Primitives.QrCode;
using Microsoft.AspNetCore.Components;

namespace BlazorBlueprint.Components;

/// <summary>
/// A QR code drawn as SVG.
/// </summary>
/// <remarks>
/// <para>
/// The encoding happens in C# through <see cref="QrEncoder"/> — no JavaScript, no image request and
/// no dependency — so the code is part of the rendered markup and prints, scales and copies like any
/// other vector. Set <see cref="Value"/> and you have a working code; everything else is presentation.
/// </para>
/// <para>
/// The default colours are dark on white rather than the theme's own, and they stay that way in a
/// dark theme. A reader expects a dark code on a light field, and enough of them refuse an inverted
/// one that following the theme would trade a working code for a tidier page. Override
/// <see cref="Foreground"/> and <see cref="Background"/> where that trade is worth making.
/// </para>
/// <para>
/// A QR code carries text that only a camera can read. Turn on <see cref="ShowValue"/>, or set
/// <see cref="AriaLabel"/>, wherever the value is not already written somewhere on the page.
/// </para>
/// </remarks>
public partial class BbQrCode : ComponentBase
{
    private const int MinQuietZone = 0;
    private const int MaxQuietZone = 16;
    private const double MinImageSize = 0.05;
    private const double MaxImageSize = 0.3;

    /// <summary>The longest value that still reads well as part of the accessible name.</summary>
    private const int AriaLabelValueLimit = 60;

    /// <summary>
    /// Pixels per module for the SVG's own width and height, which is the size the file opens at
    /// outside a page. The viewBox keeps it vector, so this only sets the starting size.
    /// </summary>
    private const int StandaloneModulePixels = 8;

    private string? svg;
    private string? encodeError;
    private QrMatrix? matrix;

    /// <summary>
    /// Gets or sets the text to encode. Nothing renders while this is empty.
    /// </summary>
    /// <remarks>
    /// Any text works, but the well-known prefixes are what make a code do something when scanned:
    /// <c>https://…</c> opens a page, <c>mailto:…</c> starts an email, <c>tel:…</c> dials,
    /// <c>WIFI:T:WPA;S:name;P:secret;;</c> joins a network, and a vCard adds a contact.
    /// </remarks>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets how much of the code can be lost or covered and still be read.
    /// </summary>
    /// <remarks>
    /// A higher level makes the code bigger for the same text. Raise it for a code that will be
    /// printed small or on something that creases. Setting <see cref="Image"/> raises it to
    /// <see cref="QrErrorCorrection.Quartile"/> on its own, because a centre image is damage.
    /// </remarks>
    [Parameter]
    public QrErrorCorrection ErrorCorrection { get; set; } = QrErrorCorrection.Medium;

    /// <summary>
    /// Gets or sets the smallest QR version to use, 1 to 40. Each step up adds four modules a side.
    /// </summary>
    /// <remarks>
    /// Raise this to keep a set of codes the same size as each other rather than letting each shrink
    /// to fit its own value. The encoder still grows past it when the text needs more room.
    /// </remarks>
    [Parameter]
    public int MinVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the rendered width and height as a CSS length.
    /// </summary>
    /// <remarks>
    /// The code is vector, so this only sets how large it draws. Below about 2 modules per device
    /// pixel a phone camera starts to struggle, which for a long value means a larger size.
    /// </remarks>
    [Parameter]
    public string Size { get; set; } = "12rem";

    /// <summary>
    /// Gets or sets the colour of the dark modules as a CSS colour.
    /// </summary>
    [Parameter]
    public string Foreground { get; set; } = "#18181b";

    /// <summary>
    /// Gets or sets the colour behind the code as a CSS colour, including the quiet zone.
    /// </summary>
    /// <remarks>
    /// Use <c>transparent</c> to let the page show through. Only do that over a light, plain
    /// background: the quiet zone is how a reader finds the edge of the code.
    /// </remarks>
    [Parameter]
    public string Background { get; set; } = "#ffffff";

    /// <summary>
    /// Gets or sets the width of the light margin around the code, in modules.
    /// </summary>
    /// <remarks>
    /// Four is the specified minimum and the default. A reader needs this margin to find the code at
    /// all, so drop below four only when the surrounding page is already light and empty.
    /// </remarks>
    [Parameter]
    public int QuietZone { get; set; } = 4;

    /// <summary>
    /// Gets or sets the shape drawn for each dark module.
    /// </summary>
    [Parameter]
    public QrCodeModuleShape ModuleShape { get; set; } = QrCodeModuleShape.Square;

    /// <summary>
    /// Gets or sets an image to place in the centre of the code, as a URL or a data URL.
    /// </summary>
    /// <remarks>
    /// The image covers modules, which is why setting it raises the error correction level to at
    /// least <see cref="QrErrorCorrection.Quartile"/>. Keep <see cref="ImageSize"/> small and test a
    /// real scan before shipping it.
    /// </remarks>
    [Parameter]
    public string? Image { get; set; }

    /// <summary>
    /// Gets or sets the width of the centre image as a fraction of the code, from 0.05 to 0.3.
    /// </summary>
    [Parameter]
    public double ImageSize { get; set; } = 0.2;

    /// <summary>
    /// Gets or sets the colour of the patch drawn behind the centre image. Defaults to <see cref="Background"/>.
    /// </summary>
    [Parameter]
    public string? ImageBackground { get; set; }

    /// <summary>
    /// Gets or sets whether the value is also written as text under the code.
    /// </summary>
    /// <remarks>
    /// This is the accessible route to what the code says, and the one anyone without a camera to
    /// hand can use. Turn it on unless the value already appears somewhere on the page.
    /// </remarks>
    [Parameter]
    public bool ShowValue { get; set; }

    /// <summary>
    /// Gets or sets a label shown above the code.
    /// </summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the accessible name of the code.
    /// </summary>
    /// <remarks>
    /// Defaults to the value where the value is short enough to read aloud usefully, and to a plain
    /// "QR code" where it is not. Set it to say what scanning the code does — "QR code to join the
    /// guest network" beats the encoded string.
    /// </remarks>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked whenever the encoded grid changes.
    /// </summary>
    /// <remarks>
    /// Carries the new grid, or <see langword="null"/> where the value is empty or will not fit. Use
    /// this rather than <see cref="Matrix"/> to show the version or the mode in markup: a parent
    /// builds its own markup before its children re-render, so reading <see cref="Matrix"/> there
    /// gives the previous grid.
    /// </remarks>
    [Parameter]
    public EventCallback<QrMatrix?> OnEncoded { get; set; }

    /// <summary>
    /// Gets or sets additional CSS classes for the root element.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets additional attributes splatted onto the root element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// Gets the encoded grid, or <see langword="null"/> while the value is empty or does not fit.
    /// </summary>
    /// <remarks>
    /// Read this from an event handler or from <c>OnAfterRender</c>, where it is current. Reading it
    /// from a parent's markup gives the previous grid, because a parent builds its markup before its
    /// children re-render — use <see cref="OnEncoded"/> there. The grid excludes the quiet zone.
    /// </remarks>
    public QrMatrix? Matrix => matrix;

    private string RootClass => ClassNames.cn("bb:inline-flex bb:flex-col bb:items-center", Class);

    private int EffectiveQuietZone => Math.Clamp(QuietZone, MinQuietZone, MaxQuietZone);

    private double EffectiveImageSize => Math.Clamp(ImageSize, MinImageSize, MaxImageSize);

    /// <summary>
    /// Gets the error correction level actually used, raised where a centre image needs the headroom.
    /// </summary>
    private QrErrorCorrection EffectiveErrorCorrection =>
        string.IsNullOrEmpty(Image) || ErrorCorrection >= QrErrorCorrection.Quartile
            ? ErrorCorrection
            : QrErrorCorrection.Quartile;

    private string EffectiveAriaLabel =>
        AriaLabel
        ?? (Value is not null && Value.Length <= AriaLabelValueLimit
            ? Localizer["QrCode.AriaLabelWithValue", Value]
            : Localizer["QrCode.AriaLabel"]);

    /// <summary>
    /// Returns the rendered SVG markup, or <see langword="null"/> while nothing is drawn.
    /// </summary>
    /// <returns>The SVG document, ready to write to a file or hand to a download.</returns>
    /// <remarks>
    /// This is the same markup the component renders, as a standalone document. It carries no CSS
    /// class, so it keeps its colours wherever it lands.
    /// </remarks>
    public string? GetSvg() => svg;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        var previous = svg;

        svg = null;
        encodeError = null;
        matrix = null;

        if (!string.IsNullOrEmpty(Value))
        {
            try
            {
                matrix = QrEncoder.Encode(Value, EffectiveErrorCorrection, Math.Clamp(MinVersion, 1, 40));
                svg = BuildSvg(matrix);
            }
            catch (ArgumentException)
            {
                // A value past the version 40 capacity is a content problem, not a bug. Say so in the
                // markup rather than throwing, which on Blazor Server would take the circuit down.
                encodeError = Localizer["QrCode.TooLong"];
            }
        }

        // Only on a real change, so a parent that re-renders in response does not bounce back here.
        if (OnEncoded.HasDelegate && !string.Equals(previous, svg, StringComparison.Ordinal))
        {
            await OnEncoded.InvokeAsync(matrix);
        }
    }

    private string BuildSvg(QrMatrix code)
    {
        var quiet = EffectiveQuietZone;
        var span = code.Size + (quiet * 2);
        var size = WebUtility.HtmlEncode(Size);
        var builder = new StringBuilder(1024);

        // The width and height attributes are unitless, so they are pixels and the file has an
        // intrinsic size when it is opened on its own. Size goes in the style, where a CSS length
        // like rem is valid and where it also wins over the attributes on the page.
        builder.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {span} {span}\"");
        builder.Append(CultureInfo.InvariantCulture, $" width=\"{span * StandaloneModulePixels}\" height=\"{span * StandaloneModulePixels}\" role=\"img\"");
        builder.Append(CultureInfo.InvariantCulture, $" aria-label=\"{WebUtility.HtmlEncode(EffectiveAriaLabel)}\"");
        builder.Append(CultureInfo.InvariantCulture, $" style=\"width:{size};height:auto;max-width:100%\">");

        builder.Append(CultureInfo.InvariantCulture, $"<rect width=\"{span}\" height=\"{span}\" fill=\"{WebUtility.HtmlEncode(Background)}\"/>");

        var foreground = WebUtility.HtmlEncode(Foreground);

        // The finder patterns stay solid whatever the module shape, so a reader still finds the code.
        builder.Append(CultureInfo.InvariantCulture, $"<path fill=\"{foreground}\" shape-rendering=\"crispEdges\" d=\"{BuildSquarePath(code, quiet, finderOnly: ModuleShape != QrCodeModuleShape.Square)}\"/>");

        if (ModuleShape != QrCodeModuleShape.Square)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<g fill=\"{foreground}\">");
            AppendShapedModules(builder, code, quiet);
            builder.Append("</g>");
        }

        AppendCentreImage(builder, span);

        builder.Append("</svg>");
        return builder.ToString();
    }

    /// <summary>
    /// Builds a path of merged horizontal runs, which keeps the node count down on a large code.
    /// </summary>
    /// <param name="code">The encoded grid.</param>
    /// <param name="quiet">The quiet zone width in modules.</param>
    /// <param name="finderOnly">
    /// <see langword="true"/> to draw only the three finder patterns, leaving the rest to the shaped
    /// pass.
    /// </param>
    private static string BuildSquarePath(QrMatrix code, int quiet, bool finderOnly)
    {
        var builder = new StringBuilder(code.Size * code.Size / 2);

        for (var y = 0; y < code.Size; y++)
        {
            var x = 0;
            while (x < code.Size)
            {
                if (!code[x, y] || (finderOnly && !IsFinderPattern(code.Size, x, y)))
                {
                    x++;
                    continue;
                }

                var run = 0;
                while (x + run < code.Size
                    && code[x + run, y]
                    && (!finderOnly || IsFinderPattern(code.Size, x + run, y)))
                {
                    run++;
                }

                builder.Append(CultureInfo.InvariantCulture, $"M{x + quiet} {y + quiet}h{run}v1h-{run}z");
                x += run;
            }
        }

        return builder.ToString();
    }

    private void AppendShapedModules(StringBuilder builder, QrMatrix code, int quiet)
    {
        for (var y = 0; y < code.Size; y++)
        {
            for (var x = 0; x < code.Size; x++)
            {
                if (!code[x, y] || IsFinderPattern(code.Size, x, y))
                {
                    continue;
                }

                if (ModuleShape == QrCodeModuleShape.Dot)
                {
                    builder.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{x + quiet + 0.5:0.##}\" cy=\"{y + quiet + 0.5:0.##}\" r=\"0.45\"/>");
                }
                else
                {
                    builder.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x + quiet}\" y=\"{y + quiet}\" width=\"1\" height=\"1\" rx=\"0.3\"/>");
                }
            }
        }
    }

    private void AppendCentreImage(StringBuilder builder, int span)
    {
        if (string.IsNullOrEmpty(Image))
        {
            return;
        }

        // Sized against the code rather than the whole SVG, so the quiet zone does not change it.
        var width = (span - (EffectiveQuietZone * 2)) * EffectiveImageSize;
        var offset = (span - width) / 2;
        var padding = width * 0.08;
        var patch = width + (padding * 2);
        var patchOffset = offset - padding;
        var patchFill = WebUtility.HtmlEncode(ImageBackground ?? Background);

        builder.Append(CultureInfo.InvariantCulture, $"<rect x=\"{patchOffset:0.###}\" y=\"{patchOffset:0.###}\" width=\"{patch:0.###}\" height=\"{patch:0.###}\" rx=\"{patch * 0.1:0.###}\" fill=\"{patchFill}\"/>");
        builder.Append(CultureInfo.InvariantCulture, $"<image x=\"{offset:0.###}\" y=\"{offset:0.###}\" width=\"{width:0.###}\" height=\"{width:0.###}\" href=\"{WebUtility.HtmlEncode(Image)}\" preserveAspectRatio=\"xMidYMid meet\"/>");
    }

    /// <summary>
    /// Gets whether a module belongs to one of the three seven-by-seven finder patterns.
    /// </summary>
    private static bool IsFinderPattern(int size, int x, int y) =>
        (x < 7 && y < 7)
        || (x >= size - 7 && y < 7)
        || (x < 7 && y >= size - 7);
}
