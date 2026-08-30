namespace Wled.Fx;

/// <summary>
/// A 32-bit colour packed as <c>0xWWRRGGBB</c> - the colour type the whole engine works in.
/// Port of the <c>CRGBW</c> struct and the <c>RGBW32</c>/<c>R</c>/<c>G</c>/<c>B</c>/<c>W</c> macros
/// in <c>colors.h</c>.
/// </summary>
/// <remarks>
/// The blend, add and fade helpers are the two-channels-at-once implementations from
/// <c>colors.cpp</c>: they process red+blue and white+green in a single multiply each, which is why
/// they are written with masks rather than per-channel arithmetic.
/// </remarks>
public readonly struct Rgbw : IEquatable<Rgbw>
{
    private const uint TwoChannelMask = 0x00FF00FF; // R and B, or W and G once shifted down

    /// <summary>The packed value, <c>0xWWRRGGBB</c>.</summary>
    public readonly uint Value;

    public Rgbw(uint value) => Value = value;

    public Rgbw(int r, int g, int b, int w = 0)
        => Value = ((uint)(byte)w << 24) | ((uint)(byte)r << 16) | ((uint)(byte)g << 8) | (byte)b;

    public byte B => (byte)Value;
    public byte G => (byte)(Value >> 8);
    public byte R => (byte)(Value >> 16);
    public byte W => (byte)(Value >> 24);

    /// <summary>True when every channel is zero.</summary>
    public bool IsBlack => Value == 0;

    /// <summary>The RGB channels only, with white discarded.</summary>
    public Crgb Rgb => new(R, G, B);

    /// <summary>Mean of all four channels.</summary>
    public byte AverageLight => (byte)((R + G + B + W) >> 2);

    /// <summary>Mean of the three colour channels.</summary>
    public byte RgbAverage => (byte)(((R + G + B) * 21846) >> 16); // x*21846>>16 == x/3

    /// <summary>Returns this colour with its white channel replaced.</summary>
    public Rgbw WithWhite(byte w) => new((Value & 0x00FFFFFF) | ((uint)w << 24));

    public static implicit operator Rgbw(uint value) => new(value);
    public static explicit operator uint(Rgbw color) => color.Value;

    public static readonly Rgbw Black = new(0u);

    // ------------------------------------------------------------------ mixing

    /// <summary>
    /// Blends <paramref name="from"/> towards <paramref name="to"/>;
    /// <paramref name="amount"/> 0 keeps <paramref name="from"/>, 255 yields <paramref name="to"/>.
    /// </summary>
    public static Rgbw Blend(Rgbw from, Rgbw to, byte amount)
    {
        uint c1 = from.Value, c2 = to.Value;
        uint rb1 = c1 & TwoChannelMask;
        uint wg1 = (c1 >> 8) & TwoChannelMask;
        uint rb2 = c2 & TwoChannelMask;
        uint wg2 = (c2 >> 8) & TwoChannelMask;
        uint rb3 = ((((rb1 << 8) | rb2) + (rb2 * amount) - (rb1 * amount)) >> 8) & TwoChannelMask;
        uint wg3 = (((wg1 << 8) | wg2) + (wg2 * amount) - (wg1 * amount)) & ~TwoChannelMask;
        return new Rgbw(rb3 | wg3);
    }

    /// <summary>16-bit blend factor variant, used while cross-fading transitions.</summary>
    public static Rgbw Blend16(Rgbw from, Rgbw to, ushort amount) => Blend(from, to, (byte)(amount >> 8));

    /// <summary>
    /// Adds two colours. With <paramref name="preserveRatio"/> the sum is scaled back on overflow so
    /// the hue survives; otherwise each channel simply saturates at 255.
    /// </summary>
    public Rgbw Add(Rgbw other, bool preserveRatio = false)
    {
        uint c1 = Value, c2 = other.Value;
        if (c1 == 0) return other;
        if (c2 == 0) return this;

        uint rb = (c1 & TwoChannelMask) + (c2 & TwoChannelMask);
        uint wg = ((c1 >> 8) & TwoChannelMask) + ((c2 >> 8) & TwoChannelMask);

        if (preserveRatio)
        {
            uint overflow = (rb | wg) & 0x01000100; // the 9th bit of either channel
            if (overflow != 0)
            {
                uint r = rb >> 16, b = rb & 0xFFFF, w = wg >> 16, g = wg & 0xFFFF;
                uint maxVal = r > g ? (r > b ? r : b) : (g > b ? g : b);
                if (w > maxVal) maxVal = w; // include white so pure white cannot divide by zero
                uint scale = (255u << 8) / maxVal;
                rb = ((rb * scale) >> 8) & TwoChannelMask;
                wg = (wg * scale) & ~TwoChannelMask;
            }
            else wg <<= 8;
        }
        else
        {
            // branchless saturation: subtract 1 from any channel whose 9th bit is set, then mask
            rb |= ((rb & 0x01000100) - ((rb >> 8) & 0x00010001)) & TwoChannelMask;
            wg |= ((wg & 0x01000100) - ((wg >> 8) & 0x00010001)) & TwoChannelMask;
            wg <<= 8;
        }
        return new Rgbw(rb | wg);
    }

    /// <summary>
    /// Fades towards black. <paramref name="video"/> keeps a dominant channel from reaching zero,
    /// which preserves the hue of colours that are already very dim.
    /// </summary>
    public Rgbw Fade(byte amount, bool video = false)
    {
        uint c1 = Value;
        if (c1 == 0 || amount == 0) return Black;
        if (amount == 255) return this;

        uint rb = c1 & TwoChannelMask;
        uint wg = (c1 >> 8) & TwoChannelMask;
        uint rbScaled, wgScaled;

        if (video)
        {
            rbScaled = ((rb * amount + 0x007F007F) >> 8) & TwoChannelMask;
            wgScaled = (wg * amount + 0x007F007F) & ~TwoChannelMask;
            byte r = (byte)(rb >> 16), g = (byte)wg, b = (byte)rb, w = (byte)(wg >> 16);
            byte maxc = r > g ? (r > b ? r : b) : (g > b ? g : b);
            maxc = (byte)((maxc >> 2) + 1); // ~25% threshold, +1 keeps very dark colours from greying out
            if (r > maxc) rbScaled |= 0x00010000;
            if (g > maxc) wgScaled |= 0x00000100;
            if (b > maxc) rbScaled |= 0x00000001;
            if (w != 0) wgScaled |= 0x01000000;
        }
        else
        {
            rbScaled = ((rb * (uint)(amount + 1)) >> 8) & TwoChannelMask;
            wgScaled = (wg * (uint)(amount + 1)) & ~TwoChannelMask;
        }
        return new Rgbw(rbScaled | wgScaled);
    }

    /// <summary>
    /// Multiplies every channel by <paramref name="scale"/>/256, favouring speed over accuracy.
    /// Port of <c>fast_color_scale()</c>.
    /// </summary>
    public Rgbw Scale(byte scale)
    {
        uint c = Value;
        uint rb = ((c & TwoChannelMask) * scale >> 8) & TwoChannelMask;
        uint wg = ((c >> 8) & TwoChannelMask) * scale & ~TwoChannelMask;
        return new Rgbw(rb | wg);
    }

    // ---------------------------------------------------------------- equality

    public bool Equals(Rgbw other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is Rgbw other && Equals(other);
    public override int GetHashCode() => (int)Value;
    public static bool operator ==(Rgbw left, Rgbw right) => left.Value == right.Value;
    public static bool operator !=(Rgbw left, Rgbw right) => left.Value != right.Value;

    public override string ToString() => $"#{Value:X8}";
}

/// <summary>Named colours matching the constants in <c>FX.h</c>.</summary>
public static class Colors
{
    public static readonly Rgbw Black = new(0x000000u);
    public static readonly Rgbw Red = new(0xFF0000u);
    public static readonly Rgbw Green = new(0x00FF00u);
    public static readonly Rgbw Blue = new(0x0000FFu);
    public static readonly Rgbw White = new(0xFFFFFFu);
    public static readonly Rgbw Yellow = new(0xFFFF00u);
    public static readonly Rgbw Cyan = new(0x00FFFFu);
    public static readonly Rgbw Magenta = new(0xFF00FFu);
    public static readonly Rgbw Purple = new(0x400080u);
    public static readonly Rgbw Orange = new(0xFF3000u);
    public static readonly Rgbw Pink = new(0xFF1493u);
    public static readonly Rgbw Grey = new(0x808080u);
    public static readonly Rgbw DarkGrey = new(0x333333u);
    public static readonly Rgbw UltraWhite = new(0xFFFFFFFFu);
    public static readonly Rgbw DarkSlateGrey = new(0x2F4F4Fu);
}
