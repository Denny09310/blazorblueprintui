using BlazorBlueprint.Primitives.QrCode;
using Xunit;

namespace BlazorBlueprint.Tests.QrCode;

/// <summary>
/// Structure and behaviour of <see cref="QrEncoder"/>. The exact module grids are cross-checked
/// against an independent implementation in <see cref="QrEncoderGoldenTests"/>.
/// </summary>
public class QrEncoderTests
{
    [Fact]
    public void RejectsEmptyValue()
    {
        Assert.Throws<ArgumentException>(() => QrEncoder.Encode(string.Empty));
        Assert.Throws<ArgumentNullException>(() => QrEncoder.Encode(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(41)]
    public void RejectsVersionOutsideRange(int minVersion) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => QrEncoder.Encode("x", minVersion: minVersion));

    [Theory]
    [InlineData(-2)]
    [InlineData(8)]
    public void RejectsMaskOutsideRange(int mask) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => QrEncoder.Encode("x", mask: mask));

    [Fact]
    public void RejectsValueTooLongForVersion40()
    {
        var tooLong = new string('7', 8000);

        var error = Assert.Throws<ArgumentException>(() => QrEncoder.Encode(tooLong));

        Assert.Contains("version 40", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1234567890", QrEncodingMode.Numeric)]
    [InlineData("0", QrEncodingMode.Numeric)]
    [InlineData("HELLO WORLD", QrEncodingMode.Alphanumeric)]
    [InlineData("A$%*+-./:", QrEncodingMode.Alphanumeric)]
    [InlineData("ABC123", QrEncodingMode.Alphanumeric)]
    [InlineData("hello", QrEncodingMode.Byte)]
    [InlineData("Hello, World!", QrEncodingMode.Byte)]
    [InlineData("café", QrEncodingMode.Byte)]
    [InlineData("你好", QrEncodingMode.Byte)]
    public void PicksTheMostCompactMode(string value, QrEncodingMode expected) =>
        Assert.Equal(expected, QrEncoder.Encode(value).Mode);

    [Fact]
    public void PicksTheSmallestVersionThatFits()
    {
        // Version 1 at Low holds 41 numeric characters, so one more forces version 2.
        Assert.Equal(1, QrEncoder.Encode(new string('7', 41), QrErrorCorrection.Low).Version);
        Assert.Equal(2, QrEncoder.Encode(new string('7', 42), QrErrorCorrection.Low).Version);
    }

    [Fact]
    public void HigherErrorCorrectionNeedsAtLeastAsLargeASymbol()
    {
        var value = new string('7', 100);

        var low = QrEncoder.Encode(value, QrErrorCorrection.Low).Version;
        var medium = QrEncoder.Encode(value, QrErrorCorrection.Medium).Version;
        var quartile = QrEncoder.Encode(value, QrErrorCorrection.Quartile).Version;
        var high = QrEncoder.Encode(value, QrErrorCorrection.High).Version;

        Assert.True(low <= medium);
        Assert.True(medium <= quartile);
        Assert.True(quartile <= high);
        Assert.True(low < high);
    }

    [Fact]
    public void MinVersionRaisesTheSymbolButNeverShrinksIt()
    {
        Assert.Equal(5, QrEncoder.Encode("x", minVersion: 5).Version);

        // The data still needs more room than version 2, so the floor is ignored.
        Assert.True(QrEncoder.Encode(new string('7', 200), minVersion: 2).Version > 2);
    }

    [Fact]
    public void SameInputAlwaysProducesTheSameGrid()
    {
        var first = QrEncoder.Encode("https://blazorblueprintui.com", QrErrorCorrection.Quartile);
        var second = QrEncoder.Encode("https://blazorblueprintui.com", QrErrorCorrection.Quartile);

        Assert.Equal(first.Version, second.Version);
        Assert.Equal(first.Mask, second.Mask);
        for (var y = 0; y < first.Size; y++)
        {
            for (var x = 0; x < first.Size; x++)
            {
                Assert.Equal(first[x, y], second[x, y]);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void HonoursAForcedMask(int mask) =>
        Assert.Equal(mask, QrEncoder.Encode("https://blazorblueprintui.com", mask: mask).Mask);

    [Fact]
    public void ForcingEachMaskChangesOnlyTheDataRegion()
    {
        var baseline = QrEncoder.Encode("BLAZOR BLUEPRINT", mask: 0);

        for (var mask = 1; mask < 8; mask++)
        {
            var other = QrEncoder.Encode("BLAZOR BLUEPRINT", mask: mask);

            Assert.Equal(baseline.Size, other.Size);

            // The finder patterns are function modules, so no mask touches them.
            AssertFinderPattern(other, 0, 0);
            AssertFinderPattern(other, other.Size - 7, 0);
            AssertFinderPattern(other, 0, other.Size - 7);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(40)]
    public void PlacesTheMandatoryFunctionPatterns(int version)
    {
        var matrix = QrEncoder.Encode(new string('7', 1), minVersion: version);
        var size = matrix.Size;

        Assert.Equal((version * 4) + 17, size);

        AssertFinderPattern(matrix, 0, 0);
        AssertFinderPattern(matrix, size - 7, 0);
        AssertFinderPattern(matrix, 0, size - 7);

        // Separators: the light ring between each finder pattern and the data.
        for (var i = 0; i < 8; i++)
        {
            Assert.False(matrix[7, i]);
            Assert.False(matrix[i, 7]);
            Assert.False(matrix[size - 8, i]);
            Assert.False(matrix[i, size - 8]);
        }

        // Timing patterns: alternating modules along row 6 and column 6, dark at even offsets.
        for (var i = 8; i < size - 8; i++)
        {
            Assert.Equal(i % 2 == 0, matrix[i, 6]);
            Assert.Equal(i % 2 == 0, matrix[6, i]);
        }

        // The module above the bottom-left finder pattern is always dark.
        Assert.True(matrix[8, size - 8]);
    }

    [Fact]
    public void ReadsOutsideTheGridAsLight()
    {
        var matrix = QrEncoder.Encode("x");

        Assert.False(matrix[-1, 0]);
        Assert.False(matrix[0, -1]);
        Assert.False(matrix[matrix.Size, 0]);
        Assert.False(matrix[0, matrix.Size]);
    }

    [Theory]
    [InlineData(1, QrErrorCorrection.Low, QrEncodingMode.Numeric, 41)]
    [InlineData(1, QrErrorCorrection.Low, QrEncodingMode.Alphanumeric, 25)]
    [InlineData(1, QrErrorCorrection.Low, QrEncodingMode.Byte, 17)]
    [InlineData(1, QrErrorCorrection.High, QrEncodingMode.Numeric, 17)]
    [InlineData(1, QrErrorCorrection.High, QrEncodingMode.Byte, 7)]
    [InlineData(10, QrErrorCorrection.Medium, QrEncodingMode.Numeric, 513)]
    [InlineData(40, QrErrorCorrection.Low, QrEncodingMode.Numeric, 7089)]
    [InlineData(40, QrErrorCorrection.Low, QrEncodingMode.Alphanumeric, 4296)]
    [InlineData(40, QrErrorCorrection.Low, QrEncodingMode.Byte, 2953)]
    [InlineData(40, QrErrorCorrection.High, QrEncodingMode.Numeric, 3057)]
    [InlineData(40, QrErrorCorrection.High, QrEncodingMode.Byte, 1273)]
    public void ReportsTheCapacityFromTheSpecification(
        int version,
        QrErrorCorrection errorCorrection,
        QrEncodingMode mode,
        int expected) =>
        Assert.Equal(expected, QrEncoder.GetCapacity(version, errorCorrection, mode));

    [Theory]
    [InlineData(QrErrorCorrection.Low)]
    [InlineData(QrErrorCorrection.Medium)]
    [InlineData(QrErrorCorrection.Quartile)]
    [InlineData(QrErrorCorrection.High)]
    public void CapacityIsExactlyWhatFitsInVersionOne(QrErrorCorrection errorCorrection)
    {
        var capacity = QrEncoder.GetCapacity(1, errorCorrection, QrEncodingMode.Numeric);

        Assert.Equal(1, QrEncoder.Encode(new string('7', capacity), errorCorrection).Version);
        Assert.Equal(2, QrEncoder.Encode(new string('7', capacity + 1), errorCorrection).Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(41)]
    public void GetCapacityRejectsVersionOutsideRange(int version) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => QrEncoder.GetCapacity(version, QrErrorCorrection.Low, QrEncodingMode.Byte));

    [Fact]
    public void CarriesTheChosenSettingsOnTheResult()
    {
        var matrix = QrEncoder.Encode("HELLO", QrErrorCorrection.Quartile, mask: 3);

        Assert.Equal(QrErrorCorrection.Quartile, matrix.ErrorCorrection);
        Assert.Equal(QrEncodingMode.Alphanumeric, matrix.Mode);
        Assert.Equal(3, matrix.Mask);
    }

    private static void AssertFinderPattern(QrMatrix matrix, int left, int top)
    {
        for (var dy = 0; dy < 7; dy++)
        {
            for (var dx = 0; dx < 7; dx++)
            {
                var ring = Math.Max(Math.Abs(dx - 3), Math.Abs(dy - 3));
                Assert.Equal(ring != 2, matrix[left + dx, top + dy]);
            }
        }
    }
}
