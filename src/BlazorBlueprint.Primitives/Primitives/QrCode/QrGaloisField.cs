namespace BlazorBlueprint.Primitives.QrCode;

/// <summary>
/// Reed-Solomon arithmetic over GF(256) with the QR code field polynomial, x^8 + x^4 + x^3 + x^2 + 1.
/// </summary>
internal static class QrGaloisField
{
    private const int FieldPolynomial = 0x11D;

    /// <summary>
    /// Builds the generator polynomial for a block of the given number of error correction codewords.
    /// </summary>
    /// <param name="degree">The number of error correction codewords per block, 1 to 255.</param>
    /// <returns>The coefficients from the highest power down, with the implicit leading 1 left out.</returns>
    internal static byte[] ComputeDivisor(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;

        // Multiply out (x - r^0)(x - r^1)...(x - r^(degree-1)), one root at a time.
        var root = 1;
        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < degree; j++)
            {
                result[j] = Multiply(result[j], (byte)root);
                if (j + 1 < degree)
                {
                    result[j] ^= result[j + 1];
                }
            }

            root = Multiply((byte)root, 0x02);
        }

        return result;
    }

    /// <summary>
    /// Divides the data by the generator polynomial and returns the remainder, which is the block's
    /// error correction codewords.
    /// </summary>
    /// <param name="data">The block's data codewords.</param>
    /// <param name="divisor">A generator polynomial from <see cref="ComputeDivisor"/>.</param>
    /// <returns>The error correction codewords, one per degree of the divisor.</returns>
    internal static byte[] ComputeRemainder(ReadOnlySpan<byte> data, byte[] divisor)
    {
        var result = new byte[divisor.Length];
        foreach (var b in data)
        {
            var factor = (byte)(b ^ result[0]);
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;
            for (var i = 0; i < result.Length; i++)
            {
                result[i] ^= Multiply(divisor[i], factor);
            }
        }

        return result;
    }

    /// <summary>
    /// Multiplies two field elements.
    /// </summary>
    /// <param name="x">The first element.</param>
    /// <param name="y">The second element.</param>
    /// <returns>The product in the field.</returns>
    internal static byte Multiply(byte x, byte y)
    {
        var result = 0;
        for (var i = 7; i >= 0; i--)
        {
            result = (result << 1) ^ ((result >>> 7) * FieldPolynomial);
            result ^= ((y >>> i) & 1) * x;
        }

        return (byte)result;
    }
}
