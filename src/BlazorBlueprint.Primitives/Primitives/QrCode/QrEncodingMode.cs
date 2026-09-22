namespace BlazorBlueprint.Primitives.QrCode;

/// <summary>
/// How the text was packed into the QR code's bit stream.
/// </summary>
/// <remarks>
/// The encoder picks the most compact mode that covers the whole string, so a string of digits
/// takes roughly a third of the space it would take as bytes.
/// </remarks>
public enum QrEncodingMode
{
    /// <summary>Digits only. About 3.33 bits per character.</summary>
    Numeric,

    /// <summary>Digits, upper-case letters and <c>$%*+-./:</c> plus space. About 5.5 bits per character.</summary>
    Alphanumeric,

    /// <summary>Any text, as UTF-8 bytes. 8 bits per byte.</summary>
    Byte,
}
