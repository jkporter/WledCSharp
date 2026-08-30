namespace Wled.Fx;

/// <summary>
/// Fixed-point helpers used throughout the effect engine.
/// Port of the inline math in <c>fastled_slim.h</c> and of <c>wled_math.cpp</c>.
/// </summary>
/// <remarks>
/// Every routine reproduces the integer behaviour of the C++ original bit for bit, so effects
/// ported from WLED render identically. Portions derive from FastLED 3.6.0 (MIT licence).
/// </remarks>
public static class FastMath
{
    public const float Pi = 3.1415926535897932f;
    public const float HalfPi = Pi / 2f;
    public const float QuarterPi = Pi / 4f;
    public const float TwoPi = Pi * 2f;

    // ---------------------------------------------------------------- scaling

    /// <summary>Scales <paramref name="value"/> by <paramref name="scale"/>/256.</summary>
    public static byte Scale8(byte value, byte scale) => (byte)((value * (1 + scale)) >> 8);

    /// <summary>Scales by <paramref name="scale"/>/256 but never fades a non-zero value to zero.</summary>
    public static byte Scale8Video(byte value, byte scale)
        => (byte)(((value * scale) >> 8) + (value != 0 && scale != 0 ? 1 : 0));

    /// <summary>Scales <paramref name="value"/> by <paramref name="scale"/>/65536.</summary>
    public static ushort Scale16(ushort value, ushort scale) => (ushort)(((uint)value * (1u + scale)) >> 16);

    /// <summary>Adds with saturation at 255.</summary>
    public static byte QAdd8(byte a, byte b) { int t = a + b; return (byte)(t > 255 ? 255 : t); }

    /// <summary>Subtracts with saturation at 0.</summary>
    public static byte QSub8(byte a, byte b) { int t = a - b; return (byte)(t < 0 ? 0 : t); }

    /// <summary>Multiplies with saturation at 255.</summary>
    public static byte QMul8(byte a, byte b) { int p = a * b; return (byte)(p > 255 ? 255 : p); }

    /// <summary>Absolute value of a signed 8-bit quantity.</summary>
    public static sbyte Abs8(sbyte value) => (sbyte)(value < 0 ? -value : value);

    /// <summary>Linear interpolation between two bytes; <paramref name="frac"/> is 0..255.</summary>
    public static byte Lerp8By8(byte a, byte b, byte frac) => (byte)(a + (((b - a) * (frac + 1)) >> 8));

    /// <summary>Clamps <paramref name="value"/> into the inclusive range.</summary>
    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

    /// <summary>Re-maps a value from one range to another (integer <c>map()</c>).</summary>
    public static int Map(int x, int inMin, int inMax, int outMin, int outMax)
        => (x - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;

    /// <summary>Re-maps a value from one range to another (float <c>mapf()</c>).</summary>
    public static float MapF(float x, float inMin, float inMax, float outMin, float outMax)
        => (x - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;

    // ------------------------------------------------------------ trigonometry

    /// <summary>
    /// Integer sine using Bhaskara I approximation: input 0..65535 maps to a full turn,
    /// output is -32767..32767.
    /// </summary>
    public static short Sin16(ushort theta)
    {
        int scale = 1;
        if (theta > 0x7FFF)
        {
            theta = (ushort)(0xFFFF - theta);
            scale = -1; // the second half of the sine period is negative
        }
        uint precal = (uint)(theta * (0x7FFF - theta));
        ulong numerator = (ulong)precal * (4 * 0x7FFF);
        uint denominator = (uint)(1342095361 - (int)precal); // 1342095361 == 5 * 0x7FFF^2 / 4
        var result = (short)(numerator / denominator);
        return (short)(result * scale);
    }

    /// <summary>Integer cosine; see <see cref="Sin16"/>.</summary>
    public static short Cos16(ushort theta) => Sin16((ushort)(theta + 0x4000));

    /// <summary>8-bit sine: input 0..255 is a full turn, output 0..255 centred on 128.</summary>
    public static byte Sin8(byte theta)
    {
        int sin16 = Sin16((ushort)(theta * 257));
        sin16 += 0x7FFF + 128; // shift to 0..0xFFFF, +128 to round
        return (byte)(System.Math.Min(sin16, 0xFFFF) >> 8);
    }

    /// <summary>8-bit cosine; see <see cref="Sin8"/>.</summary>
    public static byte Cos8(byte theta) => Sin8((byte)(theta + 64));

    /// <summary>Sine of an angle in radians, accurate to about +/-0.0015.</summary>
    public static float Sin(float theta)
    {
        var scaled = (ushort)(int)(theta * (0xFFFF / TwoPi));
        return Sin16(scaled) / (float)0x7FFF;
    }

    /// <summary>Cosine of an angle in radians, accurate to about +/-0.0015.</summary>
    public static float Cos(float theta)
    {
        var scaled = (ushort)(int)(theta * (0xFFFF / TwoPi));
        return Sin16((ushort)(scaled + 0x4000)) / (float)0x7FFF;
    }

    /// <summary>Tangent of an angle in radians; returns 0 where the cosine vanishes.</summary>
    public static float Tan(float x)
    {
        float c = Cos(x);
        return c == 0f ? 0f : Sin(x) / c;
    }

    private const float Atan2ConstA = 0.1963f;
    private const float Atan2ConstB = 0.9817f;

    /// <summary>Approximate <c>atan2</c>, considerably faster than the framework version.</summary>
    public static float Atan2(float y, float x)
    {
        float absY = System.Math.Abs(y);
        float absX = System.Math.Abs(x);
        float r = (absX - absY) / (absY + absX + 1e-10f); // the epsilon avoids a division by zero
        float angle;
        if (x < 0) { r = -r; angle = HalfPi + QuarterPi; }
        else angle = HalfPi - QuarterPi;

        angle += (Atan2ConstA * (r * r) - Atan2ConstB) * r;
        return y < 0 ? -angle : angle;
    }

    /// <summary>Approximate <c>acos</c>; absolute error is below 6.7e-5.</summary>
    public static float Acos(float x)
    {
        float negate = x < 0 ? 1f : 0f;
        float xabs = System.Math.Abs(x);
        float ret = -0.0187293f;
        ret = ret * xabs + 0.0742610f;
        ret = ret * xabs - 0.2121144f;
        ret = ret * xabs + HalfPi;
        ret *= MathF.Sqrt(1.0f - xabs);
        ret -= 2 * negate * ret;
        return negate * Pi + ret;
    }

    /// <summary>Approximate <c>asin</c>.</summary>
    public static float Asin(float x) => HalfPi - Acos(x);

    /// <summary>Approximate <c>atan</c>.</summary>
    public static float Atan(float x)
    {
        const float a = 0.0776509570923569f;
        const float b = -0.287434475393028f;
        const float c = QuarterPi - a - b;
        // polynomial factors for the 1..5 range
        const float c0 = 0.089494f, c1 = 0.974207f, c2 = -0.326175f, c3 = 0.05375f, c4 = -0.003445f;

        bool neg = x < 0;
        x = System.Math.Abs(x);
        float res;
        if (x > 5.0f) res = HalfPi - (1.0f / x); // converges to pi/2 - 1/x
        else if (x > 1.0f)
        {
            float xx = x * x;
            res = (c4 * xx * xx) + (c3 * xx * x) + (c2 * xx) + (c1 * x) + c0;
        }
        else
        {
            float xx = x * x;
            res = ((a * xx + b) * xx + c) * x;
        }
        return neg ? -res : res;
    }

    /// <summary>Exact bit-wise integer square root.</summary>
    public static uint Sqrt32(uint x)
    {
        uint res = 0;
        uint num = x;
        uint bit = num < 1u << 10 ? 1u << 10 : num < 1u << 20 ? 1u << 20 : 1u << 30;

        while (bit > num) bit >>= 2;
        while (bit != 0)
        {
            if (num >= res + bit)
            {
                num -= res + bit;
                res = (res >> 1) + bit;
            }
            else res >>= 1;
            bit >>= 2;
        }
        return res;
    }

    // ------------------------------------------------------------- waveforms

    /// <summary>Cubic ease in/out (S-curve 3x^2 - 2x^3), 8-bit.</summary>
    public static byte Ease8InOutCubic(byte i)
    {
        uint ii = (uint)(i * i);
        uint factor = (3u << 8) - ((uint)i << 1);
        return (byte)((ii * factor) >> 16);
    }

    /// <summary>Cubic ease in/out, 16-bit.</summary>
    public static ushort Ease16InOutCubic(ushort i)
    {
        uint ii = ((uint)i * i) >> 16;
        uint factor = (3u << 16) - ((uint)i << 1);
        return (ushort)((ii * factor) >> 16);
    }

    /// <summary>Quadratic ease in/out.</summary>
    public static byte Ease8InOutQuad(byte i)
    {
        uint j = i;
        if ((j & 0x80) != 0) j = 255 - j; // mirror the upper half
        uint jj = (j * j) >> 7;
        return (byte)((i & 0x80) != 0 ? 255 - jj : jj);
    }

    /// <summary>Triangle wave, 8-bit.</summary>
    public static byte TriWave8(byte i)
    {
        if ((i & 0x80) != 0) i = (byte)(255 - i);
        return (byte)(i << 1);
    }

    /// <summary>Triangle wave, 16-bit.</summary>
    public static ushort TriWave16(ushort i)
        => i < 0x8000 ? (ushort)(i * 2) : (ushort)(0xFFFF - (i - 0x8000) * 2);

    /// <summary>Quadratic wave; lingers a little longer at the extremes than a sine.</summary>
    public static byte QuadWave8(byte i) => Ease8InOutQuad(TriWave8(i));

    /// <summary>Cubic wave; lingers noticeably longer at the extremes than a sine.</summary>
    public static byte CubicWave8(byte i) => Ease8InOutCubic(TriWave8(i));

    // ----------------------------------------------------------------- hashing

    /// <summary>Integer avalanche hash, used to shuffle pixel indices deterministically.</summary>
    public static uint HashInt(uint s)
    {
        s = ((s >> 16) ^ s) * 0x45d9f3b;
        s = ((s >> 16) ^ s) * 0x45d9f3b;
        return (s >> 16) ^ s;
    }
}
