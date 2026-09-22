using BlazorBlueprint.Primitives.Barcode;
using Xunit;

namespace BlazorBlueprint.Tests.Barcode;

/// <summary>
/// The encoder's error messages are shown to whoever typed the value, so they have to be the
/// encoder's own words and the same words on every host.
/// </summary>
/// <remarks>
/// <see cref="ArgumentException.Message"/> composes the message with the parameter name, and the
/// wording of that suffix comes from a framework resource. On WebAssembly those resources are
/// routinely trimmed, and what comes back instead is the resource key — which is how a barcode
/// with a bad value came to print <c>Arg_ParamName_Name</c> on the page.
/// </remarks>
public class BarcodeMessageTests
{
    [Theory]
    [InlineData("", BarcodeType.Code128)]
    [InlineData("12345", BarcodeType.Ean13)]
    [InlineData("5901234123459", BarcodeType.Ean13)]
    [InlineData("hello", BarcodeType.Itf)]
    [InlineData("1", BarcodeType.Pharmacode)]
    [InlineData("not an isbn", BarcodeType.Isbn)]
    public void ARefusedValueSaysWhyWithoutAnyFrameworkWording(string value, BarcodeType type)
    {
        var error = Assert.Throws<BarcodeFormatException>(() => BarcodeEncoder.Encode(value, type));

        // The two things the framework appends. Either one on a page is a bug someone has to see.
        Assert.DoesNotContain("Arg_ParamName_Name", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("(Parameter", error.Message, StringComparison.Ordinal);

        // Still a sentence, not an empty string standing in for one.
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.EndsWith(".", error.Message.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheParameterNameIsStillThereForAnythingThatWantsIt()
    {
        // Kept on the exception, only kept out of the text.
        var error = Assert.Throws<BarcodeFormatException>(() => BarcodeEncoder.Encode("12345", BarcodeType.Ean13));

        Assert.False(string.IsNullOrEmpty(error.ParamName));
    }

    [Fact]
    public void ItIsStillAnArgumentExceptionSoExistingCatchBlocksHold()
    {
        Assert.Throws<BarcodeFormatException>(() => BarcodeEncoder.Encode("", BarcodeType.Code128));

        var caught = Record.Exception(() => BarcodeEncoder.Encode("", BarcodeType.Code128));
        Assert.IsAssignableFrom<ArgumentException>(caught);
    }

    [Fact]
    public void AWrongCheckDigitSaysWhichDigitItShouldHaveBeen()
    {
        // The specific messages are the reason this was not solved by showing DescribeInput
        // instead. "The check digit is wrong" tells someone what to change; "13 digits" does not.
        var error = Assert.Throws<BarcodeFormatException>(
            () => BarcodeEncoder.Encode("5901234123459", BarcodeType.Ean13));

        Assert.Contains("check digit", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
