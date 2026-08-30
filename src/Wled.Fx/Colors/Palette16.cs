namespace Wled.Fx;

/// <summary>
/// A 16-entry colour palette. Port of FastLED <c>CRGBPalette16</c> as trimmed in
/// <c>fastled_slim.h</c>.
/// </summary>
/// <remarks>
/// This is a mutable reference type rather than a struct because palettes are morphed in place
/// while transitions run (see <see cref="BlendToward"/>), and because the engine keeps long-lived
/// references to the current, random and target palettes.
/// </remarks>
public sealed class Palette16
{
    /// <summary>Number of entries in every palette.</summary>
    public const int Size = 16;

    private readonly Crgb[] _entries = new Crgb[Size];

    /// <summary>Creates an all-black palette.</summary>
    public Palette16() { }

    /// <summary>Creates a palette from 16 explicit entries.</summary>
    public Palette16(ReadOnlySpan<Crgb> entries)
    {
        if (entries.Length != Size) throw new ArgumentException($"A palette needs exactly {Size} entries.", nameof(entries));
        entries.CopyTo(_entries);
    }

    /// <summary>Creates a solid palette.</summary>
    public Palette16(Crgb c1) => ColorUtil.FillSolid(_entries, c1);

    /// <summary>Creates a two-stop gradient palette.</summary>
    public Palette16(Crgb c1, Crgb c2) => ColorUtil.FillGradient(_entries, c1, c2);

    /// <summary>Creates a three-stop gradient palette.</summary>
    public Palette16(Crgb c1, Crgb c2, Crgb c3) => ColorUtil.FillGradient(_entries, c1, c2, c3);

    /// <summary>Creates a four-stop gradient palette.</summary>
    public Palette16(Crgb c1, Crgb c2, Crgb c3, Crgb c4) => ColorUtil.FillGradient(_entries, c1, c2, c3, c4);

    /// <summary>The palette entries.</summary>
    public Span<Crgb> Entries => _entries;

    public Crgb this[int index]
    {
        get => _entries[index & 0x0F];
        set => _entries[index & 0x0F] = value;
    }

    /// <summary>Returns an independent copy.</summary>
    public Palette16 Clone()
    {
        var copy = new Palette16();
        _entries.CopyTo(copy._entries, 0);
        return copy;
    }

    /// <summary>Overwrites this palette with the contents of <paramref name="other"/>.</summary>
    public void CopyFrom(Palette16 other) => other._entries.CopyTo(_entries, 0);

    /// <summary>Samples the palette; see <see cref="ColorUtil.ColorFromPalette"/>.</summary>
    public Rgbw ColorAt(int index, byte brightness = 255, BlendType blendType = BlendType.LinearBlend)
        => ColorUtil.ColorFromPalette(this, index, brightness, blendType);

    /// <summary>
    /// Builds a palette from a FastLED gradient-palette blob: repeating
    /// <c>{index, r, g, b}</c> quads terminated by an entry with index 255.
    /// </summary>
    public static Palette16 FromGradient(ReadOnlySpan<byte> gradient)
    {
        var palette = new Palette16();
        palette.LoadGradient(gradient);
        return palette;
    }

    /// <summary>Replaces this palette contents from a gradient blob; see <see cref="FromGradient"/>.</summary>
    public void LoadGradient(ReadOnlySpan<byte> gradient)
    {
        // count the stops so short gradients can be spread over all 16 slots
        int count = 0;
        while (count * 4 < gradient.Length)
        {
            count++;
            if (gradient[(count - 1) * 4] == 255) break;
        }

        int lastSlotUsed = -1;
        int cursor = 0;
        var rgbStart = new Crgb(gradient[cursor + 1], gradient[cursor + 2], gradient[cursor + 3]);
        int indexStart = 0;

        while (indexStart < 255)
        {
            cursor += 4;
            if (cursor + 3 >= gradient.Length) break;
            int indexEnd = gradient[cursor];
            var rgbEnd = new Crgb(gradient[cursor + 1], gradient[cursor + 2], gradient[cursor + 3]);
            int iStart8 = indexStart / 16;
            int iEnd8 = indexEnd / 16;
            if (count < 16)
            {
                // spread sparse gradients so no colour band is dropped entirely
                if (iStart8 <= lastSlotUsed && lastSlotUsed < 15)
                {
                    iStart8 = lastSlotUsed + 1;
                    if (iEnd8 < iStart8) iEnd8 = iStart8;
                }
                lastSlotUsed = iEnd8;
            }
            ColorUtil.FillGradient(_entries, iStart8, rgbStart, iEnd8, rgbEnd);
            indexStart = indexEnd;
            rgbStart = rgbEnd;
        }
    }

    /// <summary>
    /// Nudges this palette towards <paramref name="target"/> by at most
    /// <paramref name="maxChanges"/> single-channel steps. Repeated calls morph one palette into
    /// another smoothly; roughly 255 passes of 48 changes complete the journey.
    /// </summary>
    public void BlendToward(Palette16 target, byte maxChanges)
    {
        int changes = 0;
        for (int i = 0; i < Size; i++)
        {
            Crgb a = _entries[i];
            Crgb b = target._entries[i];
            if (a == b) continue;
            _entries[i] = new Crgb(
                StepChannel(a.R, b.R, ref changes, maxChanges),
                StepChannel(a.G, b.G, ref changes, maxChanges),
                StepChannel(a.B, b.B, ref changes, maxChanges));
            if (changes >= maxChanges) break;
        }
    }

    private static byte StepChannel(byte current, byte target, ref int changes, byte maxChanges)
    {
        if (current == target || changes >= maxChanges) return current;
        changes++;
        if (current < target) return (byte)(current + 1);
        // step down by two where that does not overshoot, matching FastLED asymmetry
        current--;
        if (current > target) current--;
        return current;
    }

    /// <summary>A fully random palette of four vivid colours.</summary>
    public static Palette16 Random() => new(
        new Chsv(Rng.Next8(), Rng.Next8(160, 255), Rng.Next8(128, 255)),
        new Chsv(Rng.Next8(), Rng.Next8(160, 255), Rng.Next8(128, 255)),
        new Chsv(Rng.Next8(), Rng.Next8(160, 255), Rng.Next8(128, 255)),
        new Chsv(Rng.Next8(), Rng.Next8(160, 255), Rng.Next8(128, 255)));

    /// <summary>
    /// Generates a random palette that is harmonically related to <paramref name="basePalette"/>:
    /// one of its colours is kept and the remaining three are derived from it using a randomly
    /// chosen harmony (analogous, triadic, split-complementary, square or tetradic).
    /// </summary>
    public static Palette16 RandomHarmonic(Palette16 basePalette)
    {
        Span<byte> hues = stackalloc byte[4];
        Span<byte> sats = stackalloc byte[4];
        Span<byte> vals = stackalloc byte[4];

        byte keepPosition = Rng.Next8(4);
        Chsv kept = (Chsv)ColorUtil.RgbToHsv(basePalette[keepPosition * 5]);
        hues[keepPosition] = (byte)(kept.H + Rng.Next8(10) - 5); // +/- 5 of the base colour

        // three vivid colours plus one that is allowed to be muted
        for (int i = 0; i < 3; i++)
        {
            sats[i] = Rng.Next8(200, 255);
            vals[i] = Rng.Next8(220, 255);
        }
        sats[3] = Rng.Next8(20, 255);
        vals[3] = Rng.Next8(80, 255);

        for (int i = 3; i > 0; i--)
        {
            int j = Rng.Next8((uint)(i + 1));
            (sats[i], sats[j]) = (sats[j], sats[i]);
            (vals[i], vals[j]) = (vals[j], vals[i]);
        }

        byte baseHue = hues[keepPosition];
        Span<byte> harmonics = stackalloc byte[3];
        switch (Rng.Next8(5))
        {
            case 0: // analogous
                harmonics[0] = (byte)(baseHue + Rng.Next8(30, 50));
                harmonics[1] = (byte)(baseHue + Rng.Next8(10, 30));
                harmonics[2] = (byte)(baseHue - Rng.Next8(10, 30));
                break;
            case 1: // triadic
                harmonics[0] = (byte)(baseHue + 113 + Rng.Next8(15));
                harmonics[1] = (byte)(baseHue + 233 + Rng.Next8(15));
                harmonics[2] = (byte)(baseHue - 7 + Rng.Next8(15));
                break;
            case 2: // split-complementary
                harmonics[0] = (byte)(baseHue + 145 + Rng.Next8(10));
                harmonics[1] = (byte)(baseHue + 205 + Rng.Next8(10));
                harmonics[2] = (byte)(baseHue - 5 + Rng.Next8(10));
                break;
            case 3: // square
                harmonics[0] = (byte)(baseHue + 85 + Rng.Next8(10));
                harmonics[1] = (byte)(baseHue + 175 + Rng.Next8(10));
                harmonics[2] = (byte)(baseHue + 265 + Rng.Next8(10));
                break;
            default: // tetradic
                harmonics[0] = (byte)(baseHue + 80 + Rng.Next8(20));
                harmonics[1] = (byte)(baseHue + 170 + Rng.Next8(20));
                harmonics[2] = (byte)(baseHue - 15 + Rng.Next8(30));
                break;
        }

        if (Rng.Next8() < 128) // half the time, shuffle the harmonics instead of keeping their order
        {
            for (int i = 2; i > 0; i--)
            {
                int j = Rng.Next8((uint)(i + 1));
                (harmonics[i], harmonics[j]) = (harmonics[j], harmonics[i]);
            }
        }

        int h = 0;
        for (int i = 0; i < 4; i++)
        {
            if (i == keepPosition) continue;
            hues[i] = harmonics[h++];
        }

        bool pastel = Rng.Next8() < 25; // roughly a 10% chance of a desaturated palette
        var colors = new Crgb[4];
        for (int i = 0; i < 4; i++)
        {
            byte sat = sats[i];
            if (pastel && sat > 180) sat -= 160;
            colors[i] = new Crgb(new Chsv(hues[i], sat, vals[i]));
        }
        return new Palette16(colors[0], colors[1], colors[2], colors[3]);
    }
}
