namespace Wled.Fx;

/// <summary>
/// Integer Perlin noise in one, two and three dimensions.
/// Port of the <c>perlin*_raw</c> / <c>perlin8</c> / <c>perlin16</c> family in <c>util.cpp</c>.
/// </summary>
/// <remarks>
/// The gradient hashing and the fixed-point smoothstep are tuned to stay close to the classic
/// FastLED <c>inoise8</c> output that most WLED effects were authored against.
/// </remarks>
public static class Perlin
{
    private const int Shift = 1;

    /// <summary>Derives a small signed gradient from a hash value.</summary>
    private static int HashToGradient(uint h) => (int)(h & 0x03) - 2;

    private static int Gradient1D(uint x0, int dx)
    {
        uint h = x0 * 0x27D4EB2D;
        h ^= h >> 15;
        h *= 0x92C3412B;
        h ^= h >> 13;
        h ^= h >> 7;
        return (HashToGradient(h) * dx) >> Shift;
    }

    private static int Gradient2D(uint x0, int dx, uint y0, int dy)
    {
        uint h = (x0 * 0x27D4EB2D) ^ (y0 * 0xB5297A4D);
        h ^= h >> 15;
        h *= 0x92C3412B;
        h ^= h >> 13;
        return (HashToGradient(h) * dx + HashToGradient(h >> Shift) * dy) >> (1 + Shift);
    }

    private static int Gradient3D(uint x0, int dx, uint y0, int dy, uint z0, int dz)
    {
        uint h = (x0 * 0x27D4EB2D) ^ (y0 * 0xB5297A4D) ^ (z0 * 0x1B56C4E9);
        h ^= h >> 15;
        h *= 0x92C3412B;
        h ^= h >> 13;
        return ((HashToGradient(h) * dx
               + HashToGradient(h >> (1 + Shift)) * dy
               + HashToGradient(h >> (1 + 2 * Shift)) * dz) * 85) >> (8 + Shift); // x*85>>8 == x/3
    }

    /// <summary>Fixed-point cubic smoothstep t*(3 - 2t^2), scaled to avoid overflow.</summary>
    private static uint SmoothStep(uint t)
    {
        uint tSquared = (t * t) >> 16;
        uint factor = (3u << 16) - (t << 1);
        return (tSquared * factor) >> 18;
    }

    /// <summary>Linear interpolation matched to <see cref="SmoothStep"/> scaling.</summary>
    private static int Lerp(int a, int b, int t) => a + (((b - a) * t) >> 14);

    /// <summary>Raw 1D noise; the result spans roughly -24691..24689.</summary>
    public static int Raw1D(uint x, bool is16Bit = false)
    {
        int x0 = (int)(x >> 16);
        int x1 = x0 + 1;
        if (is16Bit) x1 &= 0xFF; // wrap at 0xFF rather than 0xFFFF

        int dx0 = (int)(x & 0xFFFF);
        int dx1 = dx0 - 0x10000;

        int g0 = Gradient1D((uint)x0, dx0);
        int g1 = Gradient1D((uint)x1, dx1);
        return Lerp(g0, g1, (int)SmoothStep((uint)dx0));
    }

    /// <summary>Raw 2D noise; the result spans roughly -20633..20629.</summary>
    public static int Raw2D(uint x, uint y, bool is16Bit = false)
    {
        int x0 = (int)(x >> 16), y0 = (int)(y >> 16);
        int x1 = x0 + 1, y1 = y0 + 1;
        if (is16Bit) { x1 &= 0xFF; y1 &= 0xFF; }

        int dx0 = (int)(x & 0xFFFF), dy0 = (int)(y & 0xFFFF);
        int dx1 = dx0 - 0x10000, dy1 = dy0 - 0x10000;

        int g00 = Gradient2D((uint)x0, dx0, (uint)y0, dy0);
        int g10 = Gradient2D((uint)x1, dx1, (uint)y0, dy0);
        int g01 = Gradient2D((uint)x0, dx0, (uint)y1, dy1);
        int g11 = Gradient2D((uint)x1, dx1, (uint)y1, dy1);

        int tx = (int)SmoothStep((uint)dx0);
        int ty = (int)SmoothStep((uint)dy0);

        return Lerp(Lerp(g00, g10, tx), Lerp(g01, g11, tx), ty);
    }

    /// <summary>Raw 3D noise; the result spans roughly -16788..16381.</summary>
    public static int Raw3D(uint x, uint y, uint z, bool is16Bit = false)
    {
        int x0 = (int)(x >> 16), y0 = (int)(y >> 16), z0 = (int)(z >> 16);
        int x1 = x0 + 1, y1 = y0 + 1, z1 = z0 + 1;
        if (is16Bit) { x1 &= 0xFF; y1 &= 0xFF; z1 &= 0xFF; }

        int dx0 = (int)(x & 0xFFFF), dy0 = (int)(y & 0xFFFF), dz0 = (int)(z & 0xFFFF);
        int dx1 = dx0 - 0x10000, dy1 = dy0 - 0x10000, dz1 = dz0 - 0x10000;

        int g000 = Gradient3D((uint)x0, dx0, (uint)y0, dy0, (uint)z0, dz0);
        int g001 = Gradient3D((uint)x0, dx0, (uint)y0, dy0, (uint)z1, dz1);
        int g010 = Gradient3D((uint)x0, dx0, (uint)y1, dy1, (uint)z0, dz0);
        int g011 = Gradient3D((uint)x0, dx0, (uint)y1, dy1, (uint)z1, dz1);
        int g100 = Gradient3D((uint)x1, dx1, (uint)y0, dy0, (uint)z0, dz0);
        int g101 = Gradient3D((uint)x1, dx1, (uint)y0, dy0, (uint)z1, dz1);
        int g110 = Gradient3D((uint)x1, dx1, (uint)y1, dy1, (uint)z0, dz0);
        int g111 = Gradient3D((uint)x1, dx1, (uint)y1, dy1, (uint)z1, dz1);

        int tx = (int)SmoothStep((uint)dx0);
        int ty = (int)SmoothStep((uint)dy0);
        int tz = (int)SmoothStep((uint)dz0);

        int ny0 = Lerp(Lerp(g000, g100, tx), Lerp(g010, g110, tx), ty);
        int ny1 = Lerp(Lerp(g001, g101, tx), Lerp(g011, g111, tx), ty);
        return Lerp(ny0, ny1, tz);
    }

    /// <summary>16-bit 1D noise (FastLED <c>inoise16</c> replacement).</summary>
    public static ushort Noise16(uint x) => (ushort)(((Raw1D(x) * 1159) >> 10) + 32803);

    /// <summary>16-bit 2D noise.</summary>
    public static ushort Noise16(uint x, uint y) => (ushort)(((Raw2D(x, y) * 1537) >> 10) + 32725);

    /// <summary>16-bit 3D noise.</summary>
    public static ushort Noise16(uint x, uint y, uint z) => (ushort)(((Raw3D(x, y, z) * 1731) >> 10) + 33147);

    /// <summary>8-bit 1D noise (FastLED <c>inoise8</c> replacement).</summary>
    public static byte Noise8(ushort x) => (byte)(((((Raw1D((uint)x << 8, true) * 1353) >> 10) + 32769) >> 8) & 0xFF);

    /// <summary>8-bit 2D noise.</summary>
    public static byte Noise8(ushort x, ushort y)
        => (byte)(((((Raw2D((uint)x << 8, (uint)y << 8, true) * 1620) >> 10) + 32771) >> 8) & 0xFF);

    /// <summary>8-bit 3D noise.</summary>
    public static byte Noise8(ushort x, ushort y, ushort z)
        => (byte)(((((Raw3D((uint)x << 8, (uint)y << 8, (uint)z << 8, true) * 2015) >> 10) + 33168) >> 8) & 0xFF);
}
