namespace Wled.Fx;

/// <summary>
/// A three-channel colour. Port of FastLED <c>CRGB</c> as trimmed down in <c>fastled_slim.h</c>.
/// </summary>
/// <remarks>
/// Palettes store this type because it is a third smaller than <see cref="Rgbw"/>; effects that keep
/// large colour arrays use it for the same reason. Everything that reaches the strip is converted to
/// <see cref="Rgbw"/> first.
/// </remarks>
public readonly struct Crgb : IEquatable<Crgb>
{
    public readonly byte R;
    public readonly byte G;
    public readonly byte B;

    public Crgb(int r, int g, int b) { R = (byte)r; G = (byte)g; B = (byte)b; }

    /// <summary>Builds from a 24-bit <c>0xRRGGBB</c> code.</summary>
    public Crgb(uint colorCode) : this((int)(colorCode >> 16), (int)(colorCode >> 8) & 0xFF, (int)colorCode & 0xFF) { }

    /// <summary>Converts from HSV using the rainbow (visually even) method.</summary>
    public Crgb(Chsv hsv)
    {
        Rgbw c = ColorUtil.HsvToRgbRainbow((ushort)(hsv.H << 8), hsv.S, hsv.V);
        R = c.R; G = c.G; B = c.B;
    }

    public static readonly Crgb Black = new(0, 0, 0);

    /// <summary>True when every channel is zero.</summary>
    public bool IsBlack => (R | G | B) == 0;

    /// <summary>Mean of the three channels.</summary>
    public byte AverageLight => (byte)(((R + G + B) * 21846) >> 16); // x*21846>>16 == x/3

    public static implicit operator Rgbw(Crgb c) => new(c.R, c.G, c.B);
    public static explicit operator Crgb(Rgbw c) => new(c.R, c.G, c.B);
    public static explicit operator uint(Crgb c) => ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;

    /// <summary>Channel-wise addition saturating at 255.</summary>
    public static Crgb operator +(Crgb a, Crgb b)
        => new(FastMath.QAdd8(a.R, b.R), FastMath.QAdd8(a.G, b.G), FastMath.QAdd8(a.B, b.B));

    /// <summary>Channel-wise subtraction saturating at 0.</summary>
    public static Crgb operator -(Crgb a, Crgb b)
        => new(FastMath.QSub8(a.R, b.R), FastMath.QSub8(a.G, b.G), FastMath.QSub8(a.B, b.B));

    /// <summary>Inverts every channel.</summary>
    public static Crgb operator -(Crgb c) => new(255 - c.R, 255 - c.G, 255 - c.B);

    /// <summary>Channel-wise maximum ("or" in FastLED terms).</summary>
    public static Crgb operator |(Crgb a, Crgb b)
        => new(System.Math.Max(a.R, b.R), System.Math.Max(a.G, b.G), System.Math.Max(a.B, b.B));

    /// <summary>Channel-wise minimum ("and" in FastLED terms).</summary>
    public static Crgb operator &(Crgb a, Crgb b)
        => new(System.Math.Min(a.R, b.R), System.Math.Min(a.G, b.G), System.Math.Min(a.B, b.B));

    /// <summary>Adds a constant to every channel, saturating at 255.</summary>
    public Crgb AddToRgb(byte d) => new(FastMath.QAdd8(R, d), FastMath.QAdd8(G, d), FastMath.QAdd8(B, d));

    /// <summary>Subtracts a constant from every channel, saturating at 0.</summary>
    public Crgb SubtractFromRgb(byte d) => new(FastMath.QSub8(R, d), FastMath.QSub8(G, d), FastMath.QSub8(B, d));

    /// <summary>Scales to <paramref name="scaleDown"/>/256 of the current brightness; can reach black.</summary>
    public Crgb Scale8(byte scaleDown)
    {
        uint s = scaleDown + 1u;
        return new Crgb((byte)((R * s) >> 8), (byte)((G * s) >> 8), (byte)((B * s) >> 8));
    }

    /// <summary>Scales each channel by the matching channel of <paramref name="scaleDown"/>.</summary>
    public Crgb Scale8(Crgb scaleDown)
        => new(FastMath.Scale8(R, scaleDown.R), FastMath.Scale8(G, scaleDown.G), FastMath.Scale8(B, scaleDown.B));

    /// <summary>Scales down but never fades a lit channel all the way to black.</summary>
    public Crgb Scale8Video(byte scaleDown)
    {
        byte nonZero = scaleDown != 0 ? (byte)1 : (byte)0;
        return new Crgb(
            R == 0 ? 0 : (byte)(((R * scaleDown) >> 8) + nonZero),
            G == 0 ? 0 : (byte)(((G * scaleDown) >> 8) + nonZero),
            B == 0 ? 0 : (byte)(((B * scaleDown) >> 8) + nonZero));
    }

    /// <summary>Fades towards black by <paramref name="fadeFactor"/>/256.</summary>
    public Crgb FadeToBlackBy(byte fadeFactor)
    {
        uint s = 256u - fadeFactor;
        return new Crgb((byte)((R * s) >> 8), (byte)((G * s) >> 8), (byte)((B * s) >> 8));
    }

    public bool Equals(Crgb other) => R == other.R && G == other.G && B == other.B;
    public override bool Equals(object? obj) => obj is Crgb other && Equals(other);
    public override int GetHashCode() => (R << 16) | (G << 8) | B;
    public static bool operator ==(Crgb a, Crgb b) => a.Equals(b);
    public static bool operator !=(Crgb a, Crgb b) => !a.Equals(b);

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}

/// <summary>An 8-bit hue/saturation/value colour. Port of FastLED <c>CHSV</c>.</summary>
public readonly struct Chsv(int h, int s, int v)
{
    public readonly byte H = (byte)h;
    public readonly byte S = (byte)s;
    public readonly byte V = (byte)v;

    /// <summary>Returns the same colour with a different hue.</summary>
    public Chsv WithHue(byte hue) => new(hue, S, V);

    /// <summary>Returns the same colour with a different saturation.</summary>
    public Chsv WithSaturation(byte saturation) => new(H, saturation, V);

    /// <summary>Returns the same colour with a different value.</summary>
    public Chsv WithValue(byte value) => new(H, S, value);

    public static implicit operator Crgb(Chsv hsv) => new(hsv);
    public static implicit operator Chsv32(Chsv hsv) => new((ushort)(hsv.H << 8), hsv.S, hsv.V);
}

/// <summary>
/// HSV with a 16-bit hue. Port of <c>CHSV32</c> in <c>colors.h</c>.
/// </summary>
/// <remarks>
/// The wider hue makes round-tripping through RGB noticeably more accurate, which matters for the
/// effects that nudge a hue by a fraction of a degree per frame.
/// </remarks>
public readonly struct Chsv32(int h, int s, int v)
{
    public readonly ushort H = (ushort)h;
    public readonly byte S = (byte)s;
    public readonly byte V = (byte)v;

    /// <summary>Converts an RGB colour into HSV; the white channel is ignored.</summary>
    public static Chsv32 FromRgb(Rgbw rgb) => ColorUtil.RgbToHsv(rgb);

    /// <summary>Returns the same colour with the hue shifted by <paramref name="delta"/>.</summary>
    public Chsv32 ShiftHue(int delta) => new((ushort)(H + delta), S, V);

    /// <summary>Converts to RGB using the rainbow (visually even) method.</summary>
    public Rgbw ToRgb() => ColorUtil.HsvToRgbRainbow(H, S, V);

    /// <summary>Converts to RGB using the spectrum method, which round-trips more faithfully.</summary>
    public Rgbw ToRgbSpectrum() => ColorUtil.HsvToRgbSpectrum(this);

    public static implicit operator Rgbw(Chsv32 hsv) => hsv.ToRgb();
    public static explicit operator Chsv(Chsv32 hsv) => new((byte)(hsv.H >> 8), hsv.S, hsv.V);
}
