namespace BlazorBlueprint.Primitives.Barcode;

/// <summary>
/// The barcode symbologies the encoder can produce.
/// </summary>
/// <remarks>
/// A symbology is the alphabet and the rules, not just the look. Each one accepts a different set
/// of characters and a different length, so the type has to match what the reader on the other end
/// expects. <see cref="Code128"/> is the general-purpose choice where nothing else is mandated.
/// </remarks>
public enum BarcodeType
{
    /// <summary>
    /// The general-purpose choice. Any ASCII character, and the densest of the linear codes because
    /// it packs pairs of digits into one symbol character.
    /// </summary>
    Code128,

    /// <summary>
    /// Digits, upper-case letters and <c>-.$/+%</c> plus space. Older and much wider than
    /// <see cref="Code128"/>, but still required by some logistics and defence standards.
    /// </summary>
    Code39,

    /// <summary>
    /// The 13-digit retail product code used everywhere outside North America. Give 12 digits and
    /// the check digit is worked out, or give all 13 and it is verified.
    /// </summary>
    Ean13,

    /// <summary>
    /// The short retail code for small packages. Give 7 digits and the check digit is worked out.
    /// </summary>
    Ean8,

    /// <summary>
    /// The 12-digit North American retail code. Give 11 digits and the check digit is worked out.
    /// </summary>
    UpcA,

    /// <summary>
    /// Interleaved 2 of 5. Digits only and always an even number of them, which is what lets it
    /// carry one digit in the bars and the next in the spaces.
    /// </summary>
    Itf,

    /// <summary>
    /// Digits and <c>-$:/.+</c>, wrapped in a start and stop letter from A to D. Still standard in
    /// blood banks, libraries and photo labs.
    /// </summary>
    Codabar,

    /// <summary>
    /// The United States Postal Service ZIP routing code. Height-modulated rather than
    /// width-modulated: every bar is the same width and it is the tall ones that carry the data.
    /// </summary>
    Postnet,

    /// <summary>
    /// Laetus Pharmacode. A single number from 3 to 131070 as a run of thin and thick bars, made to
    /// be readable on a fast packaging line even when printed badly.
    /// </summary>
    Pharmacode,

    /// <summary>
    /// The Royal Mail 4-state customer code. Four bar shapes rather than two widths, which packs
    /// more into the same length of label.
    /// </summary>
    Rm4scc,

    /// <summary>
    /// A book number as <see cref="Ean13"/>. Takes a 10- or 13-digit ISBN with or without its
    /// hyphens, and prints the hyphenated ISBN above the bars.
    /// </summary>
    Isbn,

    /// <summary>
    /// A serial publication number as <see cref="Ean13"/>. Takes the eight-digit ISSN and prints it
    /// above the bars.
    /// </summary>
    Issn,

    /// <summary>
    /// Modified Plessey. Digits only, with an optional check digit. Common on shelf-edge labels in
    /// stock rooms.
    /// </summary>
    Msi,

    /// <summary>
    /// Any ASCII character in sixteen modules each, using only two element widths. Standard in
    /// United Kingdom libraries and in some laboratory systems.
    /// </summary>
    Telepen,
}
