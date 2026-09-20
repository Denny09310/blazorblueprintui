using System.Globalization;
using System.Text;

namespace BlazorBlueprint.Primitives.QrCode;

/// <summary>
/// Turns text into a <see cref="QrMatrix"/>, following ISO/IEC 18004.
/// </summary>
/// <remarks>
/// <para>
/// The encoder picks the most compact mode the whole string fits (numeric, alphanumeric or UTF-8
/// bytes), then the smallest version that holds it at the requested error correction level, then
/// the mask pattern that scores best under the penalty rules in the specification. All of that is
/// deterministic: the same text and level always produce the same grid.
/// </para>
/// <para>
/// One mode is chosen for the whole string rather than splitting it into mixed-mode segments. A
/// string that is mostly digits with one letter in it therefore encodes as bytes, which can cost a
/// version compared with an optimal split. The simpler rule is easier to reason about and the
/// difference only shows on long, mixed strings.
/// </para>
/// </remarks>
public static class QrEncoder
{
    private const string AlphanumericCharset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

    private const int MinVersionNumber = 1;
    private const int MaxVersionNumber = 40;

    private const int PenaltyRun = 3;
    private const int PenaltyBlock = 3;
    private const int PenaltyFinderLike = 40;
    private const int PenaltyImbalance = 10;

    // Indexed by [error correction level][version]. Index 0 of the version axis is unused.
    private static readonly int[][] EccCodewordsPerBlockTable =
    [
        [0, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [0, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28],
        [0, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [0, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
    ];

    // Indexed by [error correction level][version]. Index 0 of the version axis is unused.
    private static readonly int[][] ErrorCorrectionBlockCountTable =
    [
        [0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25],
        [0, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49],
        [0, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68],
        [0, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81],
    ];

    // The five format bits differ from the enum's table order: Low 01, Medium 00, Quartile 11, High 10.
    private static readonly int[] FormatBitsTable = [1, 0, 3, 2];

    /// <summary>
    /// Encodes text as a QR code.
    /// </summary>
    /// <param name="value">The text to encode. Must not be empty.</param>
    /// <param name="errorCorrection">How much damage the code should survive.</param>
    /// <param name="minVersion">
    /// The smallest version to consider, 1 to 40. Raise this to force a code no smaller than a
    /// given size; the encoder still grows past it when the data needs more room.
    /// </param>
    /// <param name="mask">
    /// The mask pattern to apply, 0 to 7, or -1 to pick the best-scoring one. Leave it at -1 unless
    /// reproducing a specific code.
    /// </param>
    /// <returns>The finished grid of modules.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is empty, or it does not fit in a version 40 code at this error
    /// correction level.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minVersion"/> is outside 1 to 40, or <paramref name="mask"/> is outside -1 to 7.
    /// </exception>
    public static QrMatrix Encode(
        string value,
        QrErrorCorrection errorCorrection = QrErrorCorrection.Medium,
        int minVersion = MinVersionNumber,
        int mask = -1)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(minVersion, MinVersionNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minVersion, MaxVersionNumber);
        ArgumentOutOfRangeException.ThrowIfLessThan(mask, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(mask, 7);

        var mode = ChooseMode(value);
        var payload = mode == QrEncodingMode.Byte ? Encoding.UTF8.GetBytes(value) : [];
        var characterCount = mode == QrEncodingMode.Byte ? payload.Length : value.Length;
        var payloadBits = CountPayloadBits(value, mode, payload);

        var version = ChooseVersion(mode, payloadBits, errorCorrection, minVersion);
        if (version == 0)
        {
            var largest = GetCapacity(MaxVersionNumber, errorCorrection, mode);
            throw new ArgumentException(
                $"The value needs {characterCount} characters of {mode} capacity, which is more than the {largest} a version 40 code holds at {errorCorrection} error correction. Shorten the value or lower the error correction level.",
                nameof(value));
        }

        BitWriter writer = new();
        writer.Append(ModeIndicator(mode), 4);
        writer.Append(characterCount, CharacterCountBits(mode, version));
        AppendPayload(writer, value, mode, payload);

        var capacityBits = DataCodewordCount(version, errorCorrection) * 8;
        writer.Append(0, Math.Min(4, capacityBits - writer.Length));
        writer.Append(0, (8 - (writer.Length % 8)) % 8);
        for (var padByte = 0xEC; writer.Length < capacityBits; padByte ^= 0xEC ^ 0x11)
        {
            writer.Append(padByte, 8);
        }

        var codewords = AddErrorCorrectionAndInterleave(writer.ToBytes(), version, errorCorrection);
        return Draw(codewords, version, errorCorrection, mask, mode);
    }

    /// <summary>
    /// Gets the largest number of characters a version can hold in a given mode and level.
    /// </summary>
    /// <param name="version">The QR version, 1 to 40.</param>
    /// <param name="errorCorrection">The error correction level.</param>
    /// <param name="mode">The encoding mode. <see cref="QrEncodingMode.Byte"/> counts UTF-8 bytes, not characters.</param>
    /// <returns>The character capacity.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is outside 1 to 40.</exception>
    public static int GetCapacity(int version, QrErrorCorrection errorCorrection, QrEncodingMode mode)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, MinVersionNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(version, MaxVersionNumber);

        var bits = (DataCodewordCount(version, errorCorrection) * 8) - 4 - CharacterCountBits(mode, version);
        if (bits <= 0)
        {
            return 0;
        }

        return mode switch
        {
            QrEncodingMode.Numeric => (bits / 10 * 3) + ((bits % 10) >= 7 ? 2 : (bits % 10) >= 4 ? 1 : 0),
            QrEncodingMode.Alphanumeric => (bits / 11 * 2) + ((bits % 11) >= 6 ? 1 : 0),
            _ => bits / 8,
        };
    }

    private static QrEncodingMode ChooseMode(string value)
    {
        var numeric = true;
        var alphanumeric = true;

        foreach (var c in value)
        {
            if (c is < '0' or > '9')
            {
                numeric = false;
            }

            if (AlphanumericCharset.IndexOf(c, StringComparison.Ordinal) < 0)
            {
                alphanumeric = false;
            }

            if (!numeric && !alphanumeric)
            {
                break;
            }
        }

        return numeric ? QrEncodingMode.Numeric
            : alphanumeric ? QrEncodingMode.Alphanumeric
            : QrEncodingMode.Byte;
    }

    private static int ModeIndicator(QrEncodingMode mode) => mode switch
    {
        QrEncodingMode.Numeric => 0b0001,
        QrEncodingMode.Alphanumeric => 0b0010,
        _ => 0b0100,
    };

    private static int CharacterCountBits(QrEncodingMode mode, int version) => mode switch
    {
        QrEncodingMode.Numeric => version <= 9 ? 10 : version <= 26 ? 12 : 14,
        QrEncodingMode.Alphanumeric => version <= 9 ? 9 : version <= 26 ? 11 : 13,
        _ => version <= 9 ? 8 : 16,
    };

    private static int CountPayloadBits(string value, QrEncodingMode mode, byte[] payload) => mode switch
    {
        QrEncodingMode.Numeric => (value.Length / 3 * 10) + ((value.Length % 3) switch { 2 => 7, 1 => 4, _ => 0 }),
        QrEncodingMode.Alphanumeric => (value.Length / 2 * 11) + (value.Length % 2 * 6),
        _ => payload.Length * 8,
    };

    private static void AppendPayload(BitWriter writer, string value, QrEncodingMode mode, byte[] payload)
    {
        switch (mode)
        {
            case QrEncodingMode.Numeric:
                for (var i = 0; i < value.Length;)
                {
                    var take = Math.Min(value.Length - i, 3);
                    var digits = int.Parse(value.AsSpan(i, take), CultureInfo.InvariantCulture);
                    writer.Append(digits, (take * 3) + 1);
                    i += take;
                }

                break;

            case QrEncodingMode.Alphanumeric:
                var index = 0;
                for (; index + 2 <= value.Length; index += 2)
                {
                    var pair = (AlphanumericCharset.IndexOf(value[index], StringComparison.Ordinal) * 45)
                        + AlphanumericCharset.IndexOf(value[index + 1], StringComparison.Ordinal);
                    writer.Append(pair, 11);
                }

                if (index < value.Length)
                {
                    writer.Append(AlphanumericCharset.IndexOf(value[index], StringComparison.Ordinal), 6);
                }

                break;

            default:
                foreach (var b in payload)
                {
                    writer.Append(b, 8);
                }

                break;
        }
    }

    /// <summary>
    /// Finds the smallest version that holds the payload, or 0 when nothing does.
    /// </summary>
    private static int ChooseVersion(
        QrEncodingMode mode,
        int payloadBits,
        QrErrorCorrection errorCorrection,
        int minVersion)
    {
        for (var version = minVersion; version <= MaxVersionNumber; version++)
        {
            var used = 4 + CharacterCountBits(mode, version) + payloadBits;
            if (used <= DataCodewordCount(version, errorCorrection) * 8)
            {
                return version;
            }
        }

        return 0;
    }

    private static int RawDataModuleCount(int version)
    {
        var result = (((16 * version) + 128) * version) + 64;
        if (version >= 2)
        {
            var alignmentCount = (version / 7) + 2;
            result -= (((25 * alignmentCount) - 10) * alignmentCount) - 55;
            if (version >= 7)
            {
                result -= 36;
            }
        }

        return result;
    }

    private static int DataCodewordCount(int version, QrErrorCorrection errorCorrection) =>
        (RawDataModuleCount(version) / 8)
        - (EccCodewordsPerBlockTable[(int)errorCorrection][version]
            * ErrorCorrectionBlockCountTable[(int)errorCorrection][version]);

    private static byte[] AddErrorCorrectionAndInterleave(byte[] data, int version, QrErrorCorrection errorCorrection)
    {
        var blockCount = ErrorCorrectionBlockCountTable[(int)errorCorrection][version];
        var blockEccLength = EccCodewordsPerBlockTable[(int)errorCorrection][version];
        var rawCodewords = RawDataModuleCount(version) / 8;
        var shortBlockCount = blockCount - (rawCodewords % blockCount);
        var shortBlockLength = rawCodewords / blockCount;

        var divisor = QrGaloisField.ComputeDivisor(blockEccLength);
        var blocks = new byte[blockCount][];

        for (int i = 0, offset = 0; i < blockCount; i++)
        {
            // Every block is one codeword shorter than the last ones, so the interleave can treat
            // them as equal length and skip the missing position.
            var dataLength = shortBlockLength - blockEccLength + (i < shortBlockCount ? 0 : 1);
            var block = new byte[shortBlockLength + 1];
            Array.Copy(data, offset, block, 0, dataLength);

            var ecc = QrGaloisField.ComputeRemainder(data.AsSpan(offset, dataLength), divisor);
            Array.Copy(ecc, 0, block, block.Length - blockEccLength, blockEccLength);
            blocks[i] = block;
            offset += dataLength;
        }

        var result = new byte[rawCodewords];
        for (int i = 0, k = 0; i < blocks[0].Length; i++)
        {
            for (var j = 0; j < blocks.Length; j++)
            {
                // The short blocks have no codeword in the last data position, so skip them there.
                if (i != shortBlockLength - blockEccLength || j >= shortBlockCount)
                {
                    result[k] = blocks[j][i];
                    k++;
                }
            }
        }

        return result;
    }

    private static QrMatrix Draw(
        byte[] codewords,
        int version,
        QrErrorCorrection errorCorrection,
        int mask,
        QrEncodingMode mode)
    {
        var size = (version * 4) + 17;
        var modules = new bool[size * size];
        var isFunction = new bool[size * size];

        DrawFunctionPatterns(modules, isFunction, size, version, errorCorrection);
        DrawCodewords(modules, isFunction, size, codewords);

        var chosen = mask;
        if (chosen < 0)
        {
            var bestPenalty = int.MaxValue;
            chosen = 0;
            for (var candidate = 0; candidate < 8; candidate++)
            {
                ApplyMask(modules, isFunction, size, candidate);
                DrawFormatBits(modules, isFunction, size, errorCorrection, candidate);
                var penalty = PenaltyScore(modules, size);
                if (penalty < bestPenalty)
                {
                    bestPenalty = penalty;
                    chosen = candidate;
                }

                ApplyMask(modules, isFunction, size, candidate);
            }
        }

        ApplyMask(modules, isFunction, size, chosen);
        DrawFormatBits(modules, isFunction, size, errorCorrection, chosen);

        return new QrMatrix(size, modules, version, errorCorrection, chosen, mode);
    }

    private static void DrawFunctionPatterns(
        bool[] modules,
        bool[] isFunction,
        int size,
        int version,
        QrErrorCorrection errorCorrection)
    {
        for (var i = 0; i < size; i++)
        {
            SetFunctionModule(modules, isFunction, size, 6, i, i % 2 == 0);
            SetFunctionModule(modules, isFunction, size, i, 6, i % 2 == 0);
        }

        DrawFinderPattern(modules, isFunction, size, 3, 3);
        DrawFinderPattern(modules, isFunction, size, size - 4, 3);
        DrawFinderPattern(modules, isFunction, size, 3, size - 4);

        var positions = AlignmentPatternPositions(version, size);
        for (var i = 0; i < positions.Length; i++)
        {
            for (var j = 0; j < positions.Length; j++)
            {
                var overlapsFinder = (i == 0 && j == 0)
                    || (i == 0 && j == positions.Length - 1)
                    || (i == positions.Length - 1 && j == 0);
                if (!overlapsFinder)
                {
                    DrawAlignmentPattern(modules, isFunction, size, positions[i], positions[j]);
                }
            }
        }

        // Reserved now with a placeholder mask so the data placement skips these modules; the real
        // bits go down once the mask is chosen.
        DrawFormatBits(modules, isFunction, size, errorCorrection, 0);
        DrawVersionBits(modules, isFunction, size, version);
    }

    private static int[] AlignmentPatternPositions(int version, int size)
    {
        if (version == 1)
        {
            return [];
        }

        var count = (version / 7) + 2;
        var step = version == 32 ? 26 : ((version * 4) + (count * 2) + 1) / ((count * 2) - 2) * 2;

        var result = new int[count];
        result[0] = 6;
        for (int i = count - 1, position = size - 7; i >= 1; i--, position -= step)
        {
            result[i] = position;
        }

        return result;
    }

    private static void DrawFinderPattern(bool[] modules, bool[] isFunction, int size, int x, int y)
    {
        for (var dy = -4; dy <= 4; dy++)
        {
            for (var dx = -4; dx <= 4; dx++)
            {
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                var xx = x + dx;
                var yy = y + dy;
                if (xx >= 0 && xx < size && yy >= 0 && yy < size)
                {
                    SetFunctionModule(modules, isFunction, size, xx, yy, distance != 2 && distance != 4);
                }
            }
        }
    }

    private static void DrawAlignmentPattern(bool[] modules, bool[] isFunction, int size, int x, int y)
    {
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                SetFunctionModule(modules, isFunction, size, x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            }
        }
    }

    private static void DrawFormatBits(
        bool[] modules,
        bool[] isFunction,
        int size,
        QrErrorCorrection errorCorrection,
        int mask)
    {
        var data = (FormatBitsTable[(int)errorCorrection] << 3) | mask;
        var remainder = data;
        for (var i = 0; i < 10; i++)
        {
            remainder = (remainder << 1) ^ ((remainder >>> 9) * 0x537);
        }

        var bits = ((data << 10) | remainder) ^ 0x5412;

        for (var i = 0; i <= 5; i++)
        {
            SetFunctionModule(modules, isFunction, size, 8, i, GetBit(bits, i));
        }

        SetFunctionModule(modules, isFunction, size, 8, 7, GetBit(bits, 6));
        SetFunctionModule(modules, isFunction, size, 8, 8, GetBit(bits, 7));
        SetFunctionModule(modules, isFunction, size, 7, 8, GetBit(bits, 8));
        for (var i = 9; i < 15; i++)
        {
            SetFunctionModule(modules, isFunction, size, 14 - i, 8, GetBit(bits, i));
        }

        for (var i = 0; i < 8; i++)
        {
            SetFunctionModule(modules, isFunction, size, size - 1 - i, 8, GetBit(bits, i));
        }

        for (var i = 8; i < 15; i++)
        {
            SetFunctionModule(modules, isFunction, size, 8, size - 15 + i, GetBit(bits, i));
        }

        // The module above the bottom-left finder pattern is always dark.
        SetFunctionModule(modules, isFunction, size, 8, size - 8, true);
    }

    private static void DrawVersionBits(bool[] modules, bool[] isFunction, int size, int version)
    {
        if (version < 7)
        {
            return;
        }

        var remainder = version;
        for (var i = 0; i < 12; i++)
        {
            remainder = (remainder << 1) ^ ((remainder >>> 11) * 0x1F25);
        }

        var bits = (version << 12) | remainder;
        for (var i = 0; i < 18; i++)
        {
            var bit = GetBit(bits, i);
            var a = size - 11 + (i % 3);
            var b = i / 3;
            SetFunctionModule(modules, isFunction, size, a, b, bit);
            SetFunctionModule(modules, isFunction, size, b, a, bit);
        }
    }

    private static void DrawCodewords(bool[] modules, bool[] isFunction, int size, byte[] codewords)
    {
        var bit = 0;
        for (var right = size - 1; right >= 1; right -= 2)
        {
            // Column 6 is the vertical timing pattern, so the pair of columns shifts left past it.
            if (right == 6)
            {
                right = 5;
            }

            for (var vertical = 0; vertical < size; vertical++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? size - 1 - vertical : vertical;
                    var index = (y * size) + x;
                    if (!isFunction[index] && bit < codewords.Length * 8)
                    {
                        modules[index] = GetBit(codewords[bit >>> 3], 7 - (bit & 7));
                        bit++;
                    }
                }
            }
        }
    }

    private static void ApplyMask(bool[] modules, bool[] isFunction, int size, int mask)
    {
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var invert = mask switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (((x / 3) + (y / 2)) % 2) == 0,
                    5 => (x * y % 2) + (x * y % 3) == 0,
                    6 => (((x * y % 2) + (x * y % 3)) % 2) == 0,
                    _ => ((((x + y) % 2) + (x * y % 3)) % 2) == 0,
                };

                var index = (y * size) + x;
                if (invert && !isFunction[index])
                {
                    modules[index] = !modules[index];
                }
            }
        }
    }

    /// <summary>
    /// Scores a finished symbol under the four penalty rules in ISO/IEC 18004 table 11. Lower is better.
    /// </summary>
    private static int PenaltyScore(bool[] modules, int size)
    {
        var result = 0;

        for (var line = 0; line < size; line++)
        {
            result += RunPenalty(modules, size, line, horizontal: true);
            result += RunPenalty(modules, size, line, horizontal: false);
            result += FinderLikePenalty(modules, size, line, horizontal: true);
            result += FinderLikePenalty(modules, size, line, horizontal: false);
        }

        // N2: every block of two by two modules in one colour.
        for (var y = 0; y < size - 1; y++)
        {
            for (var x = 0; x < size - 1; x++)
            {
                var colour = modules[(y * size) + x];
                if (colour == modules[(y * size) + x + 1]
                    && colour == modules[((y + 1) * size) + x]
                    && colour == modules[((y + 1) * size) + x + 1])
                {
                    result += PenaltyBlock;
                }
            }
        }

        // N4: how far the share of dark modules sits from half, in steps of five percent.
        var dark = 0;
        foreach (var module in modules)
        {
            if (module)
            {
                dark++;
            }
        }

        var total = size * size;
        var imbalance = ((Math.Abs((dark * 20) - (total * 10)) + total - 1) / total) - 1;
        result += imbalance * PenaltyImbalance;

        return result;
    }

    /// <summary>
    /// N1: a run of five or more modules in one colour, scoring one more for each module past five.
    /// </summary>
    private static int RunPenalty(bool[] modules, int size, int line, bool horizontal)
    {
        var result = 0;
        var runColour = ModuleAt(modules, size, line, 0, horizontal);
        var runLength = 1;

        for (var i = 1; i < size; i++)
        {
            var colour = ModuleAt(modules, size, line, i, horizontal);
            if (colour == runColour)
            {
                runLength++;
                continue;
            }

            if (runLength >= 5)
            {
                result += PenaltyRun + runLength - 5;
            }

            runColour = colour;
            runLength = 1;
        }

        if (runLength >= 5)
        {
            result += PenaltyRun + runLength - 5;
        }

        return result;
    }

    /// <summary>
    /// N3: the 1:1:3:1:1 dark-light run that a reader mistakes for part of a finder pattern.
    /// </summary>
    /// <remarks>
    /// The specification scores the pattern once where it is preceded <em>or</em> followed by a light
    /// area four modules wide, so a pattern with light on both sides still scores once rather than
    /// twice. Some encoders score it twice; that only shifts which mask wins, never whether the code
    /// reads.
    /// </remarks>
    private static int FinderLikePenalty(bool[] modules, int size, int line, bool horizontal)
    {
        var result = 0;

        for (var i = 0; i + 7 <= size; i++)
        {
            if (!IsFinderLike(modules, size, line, i, horizontal))
            {
                continue;
            }

            // The quiet zone outside the symbol is light, so a pattern flush against either edge
            // counts as having its light area. That is what makes a real finder pattern score here.
            if (IsLightRun(modules, size, line, Math.Max(0, i - 4), i, horizontal)
                || IsLightRun(modules, size, line, i + 7, Math.Min(size, i + 11), horizontal))
            {
                result += PenaltyFinderLike;
            }
        }

        return result;
    }

    private static bool IsFinderLike(bool[] modules, int size, int line, int start, bool horizontal) =>
        ModuleAt(modules, size, line, start, horizontal)
        && !ModuleAt(modules, size, line, start + 1, horizontal)
        && ModuleAt(modules, size, line, start + 2, horizontal)
        && ModuleAt(modules, size, line, start + 3, horizontal)
        && ModuleAt(modules, size, line, start + 4, horizontal)
        && !ModuleAt(modules, size, line, start + 5, horizontal)
        && ModuleAt(modules, size, line, start + 6, horizontal);

    private static bool IsLightRun(bool[] modules, int size, int line, int from, int to, bool horizontal)
    {
        for (var i = from; i < to; i++)
        {
            if (ModuleAt(modules, size, line, i, horizontal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ModuleAt(bool[] modules, int size, int line, int index, bool horizontal) =>
        horizontal ? modules[(line * size) + index] : modules[(index * size) + line];

    private static void SetFunctionModule(bool[] modules, bool[] isFunction, int size, int x, int y, bool dark)
    {
        var index = (y * size) + x;
        modules[index] = dark;
        isFunction[index] = true;
    }

    private static bool GetBit(int value, int position) => ((value >>> position) & 1) != 0;

    private sealed class BitWriter
    {
        private readonly List<bool> bits = [];

        public int Length => bits.Count;

        public void Append(int value, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                bits.Add(((value >>> i) & 1) != 0);
            }
        }

        public byte[] ToBytes()
        {
            var result = new byte[(bits.Count + 7) / 8];
            for (var i = 0; i < bits.Count; i++)
            {
                if (bits[i])
                {
                    result[i >>> 3] |= (byte)(1 << (7 - (i & 7)));
                }
            }

            return result;
        }
    }
}
