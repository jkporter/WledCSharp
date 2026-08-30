namespace Wled.Fx;

/// <summary>How a palette lookup interpolates between its 16 entries.</summary>
public enum BlendType
{
    /// <summary>Snap to the nearest entry.</summary>
    NoBlend = 0,
    /// <summary>Interpolate, wrapping from the last entry back to the first.</summary>
    LinearBlend = 1,
    /// <summary>Interpolate without wrapping, so the last entry is reached exactly.</summary>
    LinearBlendNoWrap = 2,
}

/// <summary>
/// Colour conversions and palette sampling. Port of <c>colors.cpp</c> plus the conversion helpers
/// from <c>fastled_slim.cpp</c>.
/// </summary>
public static class ColorUtil
{
    // ------------------------------------------------------------ HSV -> RGB

    /// <summary>
    /// Converts HSV (16-bit hue) to RGB using the rainbow method, which spends more of the hue
    /// range on yellow and so looks more even to the eye than a plain spectrum sweep.
    /// </summary>
    public static Rgbw HsvToRgbRainbow(ushort h, byte s, byte v)
    {
        var hue = (byte)(h >> 8);
        byte sat = s;
        uint val = v;
        uint offset = (uint)(h & 0x1FFF);
        uint third16 = offset * 21846; // offset * (1/3) in 16.16
        var third = (byte)(third16 >> 21); // max 85
        byte r, g, b;

        if ((hue & 0x80) == 0)
        {
            if ((hue & 0x40) == 0) // sections 0-1
            {
                if ((hue & 0x20) == 0) { r = (byte)(255 - third); g = third; b = 0; }
                else { r = 171; g = (byte)(85 + third); b = 0; }
            }
            else // sections 2-3
            {
                if ((hue & 0x20) == 0)
                {
                    var twoThirds = (byte)(third16 >> 20); // max 170
                    r = (byte)(171 - twoThirds); g = (byte)(170 + third); b = 0;
                }
                else { r = 0; g = (byte)(255 - third); b = third; }
            }
        }
        else // sections 4-7
        {
            if ((hue & 0x40) == 0)
            {
                if ((hue & 0x20) == 0)
                {
                    var twoThirds = (byte)(third16 >> 20);
                    r = 0; g = (byte)(171 - twoThirds); b = (byte)(85 + twoThirds);
                }
                else { r = third; g = 0; b = (byte)(255 - third); }
            }
            else
            {
                if ((hue & 0x20) == 0) { r = (byte)(85 + third); g = 0; b = (byte)(171 - third); }
                else { r = (byte)(170 + third); g = 0; b = (byte)(85 - third); }
            }
        }

        // desaturate by lifting a brightness floor and scaling the colour down towards it
        if (sat != 255)
        {
            if (sat == 0) { r = 255; g = 255; b = 255; }
            else
            {
                uint desat = 255u - sat;
                desat *= desat;
                var brightnessFloor = (byte)(desat >> 8);
                uint satScale = 0xFFFF - desat;
                if (r != 0) r = (byte)((r * satScale) >> 16);
                if (g != 0) g = (byte)((g * satScale) >> 16);
                if (b != 0) b = (byte)((b * satScale) >> 16);
                r += brightnessFloor;
                g += brightnessFloor;
                b += brightnessFloor;
            }
        }

        if (val != 255)
        {
            if (val == 0) { r = 0; g = 0; b = 0; }
            else
            {
                val = val * val + 512; // == scale8_video(val, val) + 2
                if (r != 0) r = (byte)(((r * val) >> 16) + 1);
                if (g != 0) g = (byte)(((g * val) >> 16) + 1);
                if (b != 0) b = (byte)(((b * val) >> 16) + 1);
            }
        }
        return new Rgbw(r, g, b);
    }

    /// <summary>
    /// Converts HSV (16-bit hue) to RGB using the spectrum method. Slightly less pleasing than the
    /// rainbow variant but it round-trips through <see cref="RgbToHsv"/> far more faithfully.
    /// </summary>
    public static Rgbw HsvToRgbSpectrum(Chsv32 hsv)
    {
        uint region = ((uint)hsv.H * 6) >> 16; // h / (65536/6)
        uint remainder = (uint)((hsv.H - (region * 10923)) * 6);

        if (hsv.S == 0) return new Rgbw(hsv.V, hsv.V, hsv.V);

        var p = (byte)((hsv.V * (255 - hsv.S)) >> 8);
        var q = (byte)((hsv.V * (255 - ((hsv.S * remainder) >> 16))) >> 8);
        var t = (byte)((hsv.V * (255 - ((hsv.S * (65535 - remainder)) >> 16))) >> 8);

        return region switch
        {
            0 => new Rgbw(hsv.V, t, p),
            1 => new Rgbw(q, hsv.V, p),
            2 => new Rgbw(p, hsv.V, t),
            3 => new Rgbw(p, q, hsv.V),
            4 => new Rgbw(t, p, hsv.V),
            _ => new Rgbw(hsv.V, p, q),
        };
    }

    /// <summary>Converts RGB to HSV with a 16-bit hue. The white channel is ignored.</summary>
    public static Chsv32 RgbToHsv(Rgbw rgb)
    {
        int r = rgb.R, g = rgb.G, b = rgb.B;
        int maxVal = r > g ? (r > b ? r : b) : (g > b ? g : b);
        if (maxVal == 0) return new Chsv32(0, 0, 0); // black; also avoids the division below

        int minVal = r < g ? (r < b ? r : b) : (g < b ? g : b);
        int delta = maxVal - minVal;
        if (delta == 0) return new Chsv32(0, 0, (byte)maxVal); // grey: hue is undefined, report 0

        var s = (byte)(255 * delta / maxVal);
        ushort h;
        if (maxVal == r) h = (ushort)(10923 * (g - b) / delta);
        else if (maxVal == g) h = (ushort)(21845 + 10923 * (b - r) / delta);
        else h = (ushort)(43690 + 10923 * (r - g) / delta);
        return new Chsv32(h, s, (byte)maxVal);
    }

    /// <summary>
    /// Shifts hue, saturation and value of an RGB colour by round-tripping through HSV.
    /// Port of <c>adjust_color()</c>.
    /// </summary>
    public static Rgbw AdjustColor(Rgbw rgb, int hueShift, int satChange, int valueChange)
    {
        if (rgb.Value == 0 && valueChange <= 0) return Rgbw.Black;
        Chsv32 hsv = RgbToHsv(rgb);
        var shifted = new Chsv32(
            (ushort)(hsv.H + (hueShift << 8)),
            (byte)FastMath.Clamp(hsv.S + satChange, 0, 255),
            (byte)FastMath.Clamp(hsv.V + valueChange, 0, 255));
        return HsvToRgbSpectrum(shifted).WithWhite(rgb.W);
    }

    /// <summary>Rotates the hue of a colour; 256 is a full turn.</summary>
    public static Rgbw AdjustHue(Rgbw rgb, int hueShift)
        => HsvToRgbSpectrum(RgbToHsv(rgb).ShiftHue(hueShift << 8));

    /// <summary>Black-body radiation colour for a temperature 0..255.</summary>
    public static Crgb HeatColor(byte temperature)
    {
        var t192 = (byte)(((temperature * 191) >> 8) + (temperature != 0 ? 1 : 0)); // keep 1 as the minimum
        var heatRamp = (byte)((t192 & 0x3F) << 2); // ramps 0..252 within each third of the scale

        if ((t192 & 0x80) != 0) return new Crgb(255, 255, heatRamp); // hottest third: ramp up blue
        if ((t192 & 0x40) != 0) return new Crgb(255, heatRamp, 0);   // middle third: ramp up green
        return new Crgb(heatRamp, 0, 0);                             // coolest third: ramp up red
    }

    // --------------------------------------------------------------- palettes

    /// <summary>
    /// Samples a palette. <paramref name="index"/> is 0..255 across the palette; the low nibble
    /// selects the blend between two adjacent entries.
    /// </summary>
    public static Rgbw ColorFromPalette(in Palette16 palette, int index, byte brightness = 255,
                                        BlendType blendType = BlendType.LinearBlend)
    {
        if (blendType == BlendType.LinearBlendNoWrap)
            index = (index * 0xF0) >> 8; // remap so the top of the range does not wrap around

        var idx = (byte)index;
        int hi4 = idx >> 4;
        int lo4 = idx & 0x0F;

        Crgb entry = palette[hi4];
        uint red = entry.R, green = entry.G, blue = entry.B;

        if (lo4 != 0 && blendType != BlendType.NoBlend)
        {
            Crgb next = palette[hi4 == 15 ? 0 : hi4 + 1];
            uint f2 = (uint)(lo4 << 4);
            uint f1 = 256 - f2;
            red = (red * f1 + next.R * f2) >> 8;
            green = (green * f1 + next.G * f2) >> 8;
            blue = (blue * f1 + next.B * f2) >> 8;
        }

        if (brightness < 255)
        {
            uint scale = brightness + 1u;
            red = (red * scale) >> 8;
            green = (green * scale) >> 8;
            blue = (blue * scale) >> 8;
        }
        return new Rgbw((byte)red, (byte)green, (byte)blue);
    }

    /// <summary>Fills a span with a single colour.</summary>
    public static void FillSolid(Span<Crgb> colors, Crgb color)
    {
        for (int i = 0; i < colors.Length; i++) colors[i] = color;
    }

    /// <summary>Fills <c>[startPos, endPos]</c> with a linear gradient.</summary>
    public static void FillGradient(Span<Crgb> colors, int startPos, Crgb startColor, int endPos, Crgb endColor)
    {
        if (endPos < startPos)
        {
            (startPos, endPos) = (endPos, startPos);
            (startColor, endColor) = (endColor, startColor);
        }

        int divisor = endPos - startPos;
        if (divisor == 0) divisor = 1;

        int rDelta = ((endColor.R - startColor.R) << 16) / divisor;
        int gDelta = ((endColor.G - startColor.G) << 16) / divisor;
        int bDelta = ((endColor.B - startColor.B) << 16) / divisor;

        int rShifted = startColor.R << 16;
        int gShifted = startColor.G << 16;
        int bShifted = startColor.B << 16;

        for (int i = startPos; i <= endPos && i < colors.Length; i++)
        {
            colors[i] = new Crgb(rShifted >> 16, gShifted >> 16, bShifted >> 16);
            rShifted += rDelta;
            gShifted += gDelta;
            bShifted += bDelta;
        }
    }

    /// <summary>Fills a span with a two-stop gradient.</summary>
    public static void FillGradient(Span<Crgb> colors, Crgb c1, Crgb c2)
        => FillGradient(colors, 0, c1, colors.Length - 1, c2);

    /// <summary>Fills a span with a three-stop gradient.</summary>
    public static void FillGradient(Span<Crgb> colors, Crgb c1, Crgb c2, Crgb c3)
    {
        int half = colors.Length / 2, last = colors.Length - 1;
        FillGradient(colors, 0, c1, half, c2);
        FillGradient(colors, half, c2, last, c3);
    }

    /// <summary>Fills a span with a four-stop gradient.</summary>
    public static void FillGradient(Span<Crgb> colors, Crgb c1, Crgb c2, Crgb c3, Crgb c4)
    {
        int oneThird = colors.Length / 3, twoThirds = colors.Length * 2 / 3, last = colors.Length - 1;
        FillGradient(colors, 0, c1, oneThird, c2);
        FillGradient(colors, oneThird, c2, twoThirds, c3);
        FillGradient(colors, twoThirds, c3, last, c4);
    }

    // --------------------------------------------------------- colour temperature

    /// <summary>
    /// Approximates the RGB colour of a black body at <paramref name="kelvin"/>.
    /// </summary>
    public static Rgbw KelvinToRgb(int kelvin)
    {
        int r, g, b;
        float temp = kelvin / 100.0f;
        if (temp <= 66.0f)
        {
            r = 255;
            g = (int)MathF.Round(99.4708025861f * MathF.Log(temp) - 161.1195681661f);
            b = temp <= 19.0f ? 0 : (int)MathF.Round(138.5177312231f * MathF.Log(temp - 10.0f) - 305.0447927307f);
        }
        else
        {
            r = (int)MathF.Round(329.698727446f * MathF.Pow(temp - 60.0f, -0.1332047592f));
            g = (int)MathF.Round(288.1221695283f * MathF.Pow(temp - 60.0f, -0.0755148492f));
            b = 255;
        }
        return new Rgbw(FastMath.Clamp(r, 0, 255), FastMath.Clamp(g, 0, 255), FastMath.Clamp(b, 0, 255));
    }

    /// <summary>Applies a Kelvin white-balance correction to a colour.</summary>
    public static Rgbw BalanceFromKelvin(int kelvin, Rgbw color)
    {
        Rgbw correction = KelvinToRgb(kelvin);
        return new Rgbw(
            correction.R * color.R / 255,
            correction.G * color.G / 255,
            correction.B * color.B / 255,
            color.W);
    }

    /// <summary>
    /// Estimates the colour temperature of an RGB colour. Returns 1900K..10091K.
    /// Only meaningful for near-white colours, so pair it with a saturation check.
    /// </summary>
    public static int ApproximateKelvinFromRgb(Rgbw rgb)
    {
        int r = rgb.R, b = rgb.B;
        if (r == b) return 6550; // red equals blue at roughly 6600K

        if (r > b)
        {
            int scale = 0xFFFF / r; // scale blue up as though red were at full
            b = (b * scale) >> 8;
            if (b < 33) return 1900 + b * 6;
            if (b < 72) return 2100 + (b - 33) * 10;
            if (b < 101) return 2492 + (b - 72) * 14;
            if (b < 132) return 2900 + (b - 101) * 16;
            if (b < 159) return 3398 + (b - 132) * 19;
            if (b < 186) return 3906 + (b - 159) * 22;
            if (b < 210) return 4500 + (b - 186) * 25;
            if (b < 230) return 5100 + (b - 210) * 30;
            return 5700 + (b - 230) * 34;
        }
        else
        {
            int scale = 0xFFFF / b; // scale red up as though blue were at full
            r = (r * scale) >> 8;
            if (r > 225) return 6600 + (254 - r) * 50;
            int k = 8080 + (225 - r) * 86;
            return k > 10091 ? 10091 : k;
        }
    }
}

/// <summary>
/// Gamma correction tables. Port of <c>NeoGammaWLEDMethod</c>.
/// </summary>
/// <remarks>
/// Correction is off by default; the engine only applies it when
/// <see cref="Enabled"/> is set, matching the firmware option of the same name.
/// </remarks>
public static class Gamma
{
    private static readonly byte[] Table = new byte[256];
    private static readonly byte[] InverseTable = new byte[256];

    static Gamma() => Recalculate(2.8f);

    /// <summary>Whether the engine applies gamma correction when producing output.</summary>
    public static bool Enabled { get; set; }

    /// <summary>Rebuilds both tables for the given gamma exponent.</summary>
    public static void Recalculate(float gamma)
    {
        float inverse = 1.0f / gamma;
        for (int i = 1; i < 256; i++)
        {
            Table[i] = (byte)(int)(MathF.Pow(i / 255.0f, gamma) * 255.0f + 0.5f);
            InverseTable[i] = (byte)(int)(MathF.Pow((i - 0.5f) / 255.0f, inverse) * 255.0f + 0.5f);
        }
        Table[0] = 0;
        InverseTable[0] = 0;
    }

    /// <summary>Table lookup, ignoring <see cref="Enabled"/>.</summary>
    public static byte Raw8(byte value) => Table[value];

    /// <summary>Inverse table lookup, ignoring <see cref="Enabled"/>.</summary>
    public static byte RawInverse8(byte value) => InverseTable[value];

    /// <summary>Corrects a single channel if correction is enabled.</summary>
    public static byte Correct(byte value) => Enabled ? Table[value] : value;

    /// <summary>Corrects all four channels if correction is enabled.</summary>
    public static Rgbw Correct(Rgbw color)
        => Enabled ? new Rgbw(Table[color.R], Table[color.G], Table[color.B], Table[color.W]) : color;

    /// <summary>Applies the inverse correction to all four channels if correction is enabled.</summary>
    public static Rgbw Inverse(Rgbw color)
        => Enabled
            ? new Rgbw(InverseTable[color.R], InverseTable[color.G], InverseTable[color.B], InverseTable[color.W])
            : color;
}
