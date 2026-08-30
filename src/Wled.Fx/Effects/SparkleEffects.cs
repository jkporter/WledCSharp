namespace Wled.Fx;

/// <summary>
/// Sparkles, twinkles, glitter and dissolves - effects that light individual pixels at random.
/// Port of the corresponding block of <c>FX.cpp</c>.
/// </summary>
public static class SparkleEffects
{
    internal static void Register()
    {
        EffectRegistry.Register(EffectId.Twinkle, "Twinkle@!,!;!,!;!;;m12=0", Twinkle);
        EffectRegistry.Register(EffectId.Dissolve, "Dissolve@Repeat speed,Dissolve speed,,,,Random,Complete;!,!;!", Dissolve);
        EffectRegistry.Register(EffectId.DissolveRandom, "Dissolve Rnd@Repeat speed,Dissolve speed;,!;!", DissolveRandom);
        EffectRegistry.Register(EffectId.Sparkle, "Sparkle@!,,,,,,Overlay;!,!;!;;m12=0", Sparkle);
        EffectRegistry.Register(EffectId.FlashSparkle, "Sparkle Dark@!,!,,,,,Overlay;Bg,Fx;!;;m12=0", FlashSparkle);
        EffectRegistry.Register(EffectId.HyperSparkle, "Sparkle+@!,!,,,,,Overlay;Bg,Fx;!;;m12=0", HyperSparkle);
        EffectRegistry.Register(EffectId.Glitter, "Glitter@!,!,,,,,Overlay;,,Glitter color;!;;pal=11,m12=0", Glitter);
        EffectRegistry.Register(EffectId.SolidGlitter, "Solid Glitter@,!;Bg,,Glitter color;;;m12=0", SolidGlitter);
        EffectRegistry.Register(EffectId.Colortwinkle, "Colortwinkles@Fade speed,Spawn speed;;!;;m12=0", ColorTwinkle);
        EffectRegistry.Register(EffectId.Twinkleup, "Twinkleup@!,Intensity;!,!;!;;m12=0", TwinkleUp);
        EffectRegistry.Register(EffectId.Twinklefox, "Twinklefox@!,Twinkle rate,,,,Cool;!,!;!", TwinkleFox);
        EffectRegistry.Register(EffectId.Twinklecat, "Twinklecat@!,Twinkle rate,,,,Cool,Reverse;!,!;!", TwinkleCat);
        EffectRegistry.Register(EffectId.Fairy, "Fairy@!,# of flashers;!,!;!", Fairy);
        EffectRegistry.Register(EffectId.Fairytwinkle, "Fairytwinkle@!,!;!,!;!;;m12=0", FairyTwinkle);
    }

    /// <summary>
    /// A growing crowd of lit pixels, reset once it fills up.
    /// </summary>
    /// <remarks>
    /// The set of lit pixels is re-derived from one stored seed every frame rather than stored, so
    /// the pattern is stable while it grows and changes completely when the seed does.
    /// </remarks>
    public static void Twinkle(Segment seg)
    {
        seg.FadeOut(224);

        uint cycleTime = (uint)(20 + (255 - seg.Speed) * 5);
        uint it = seg.Now / cycleTime;
        if (it != seg.Step)
        {
            int maxOn = FastMath.Map(seg.Intensity, 0, 255, 1, seg.Length); // at least one pixel lit
            if (seg.Aux0 >= maxOn)
            {
                seg.Aux0 = 0;
                seg.Aux1 = Rng.Next16(); // new seed for the pattern
            }
            seg.Aux0++;
            seg.Step = it;
        }

        var prng = (ushort)seg.Aux1;
        for (int i = 0; i < seg.Aux0; i++)
        {
            prng = (ushort)(prng * 2053 + 13849);
            int j = (int)((uint)seg.Length * prng >> 16);
            seg.SetPixelColor(j, seg.ColorFromPalette(j, true, seg.PaletteSolidWrap, 0));
        }
    }

    /// <summary>Pixels flip to the foreground one at a time, then back again.</summary>
    private static void DissolveBase(Segment seg, Rgbw color)
    {
        Rgbw[] pixels = seg.GetData<Rgbw>(seg.Length);

        if (seg.Call == 0)
        {
            for (int i = 0; i < seg.Length; i++) pixels[i] = seg.Color(1);
            seg.Aux0 = 1;
        }

        for (int j = 0; j <= seg.Length / 15; j++)
        {
            if (Rng.Next8() > seg.Intensity) continue;
            for (int attempt = 0; attempt < 10; attempt++) // try ten times to find an unconverted pixel
            {
                int i = Rng.Next16((uint)seg.Length);
                if (seg.Aux0 != 0) // dissolving towards the foreground
                {
                    if (pixels[i] != seg.Color(1)) continue;
                    Rgbw c = color == seg.Color(0) ? seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0) : color;
                    // nudge the colour so an identical foreground and background cannot stall "Complete"
                    if (seg.Check2 && c == seg.Color(1)) c = c.Value ^ 0x00000001u;
                    pixels[i] = c;
                    break;
                }
                if (pixels[i] == seg.Color(1)) continue;
                pixels[i] = seg.Color(1);
                break;
            }
        }

        int incomplete = 0;
        for (int i = 0; i < seg.Length; i++)
        {
            seg.SetPixelColor(i, pixels[i]);
            if (!seg.Check2) continue;
            if (seg.Aux0 != 0)
            {
                if (pixels[i] == seg.Color(1)) incomplete++;
            }
            else if (pixels[i] != seg.Color(1)) incomplete++;
        }

        if (seg.Step > (uint)(255 - seg.Speed) + 15)
        {
            seg.Aux0 = (ushort)(seg.Aux0 != 0 ? 0 : 1);
            seg.Step = 0;
        }
        else if (!seg.Check2 || incomplete == 0) seg.Step++;
    }

    /// <summary>Pixels dissolve between the two segment colours.</summary>
    public static void Dissolve(Segment seg)
        => DissolveBase(seg, seg.Check1 ? seg.ColorWheel(Rng.Next8()) : seg.Color(0));

    /// <summary>Pixels dissolve into random colours.</summary>
    public static void DissolveRandom(Segment seg) => DissolveBase(seg, seg.ColorWheel(Rng.Next8()));

    /// <summary>One pixel at a time flashes in the primary colour.</summary>
    public static void Sparkle(Segment seg)
    {
        if (!seg.Check2)
        {
            for (int i = 0; i < seg.Length; i++)
                seg.SetPixelColor(i, seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 1));
        }

        uint cycleTime = (uint)(10 + (255 - seg.Speed) * 2);
        uint it = seg.Now / cycleTime;
        if (it != seg.Step)
        {
            seg.Aux0 = Rng.Next16((uint)seg.Length);
            seg.Step = it;
        }

        seg.SetPixelColor(seg.Aux0, seg.Color(0));
    }

    /// <summary>A lit segment with occasional dark flashes.</summary>
    public static void FlashSparkle(Segment seg)
    {
        if (!seg.Check2)
        {
            for (int i = 0; i < seg.Length; i++)
                seg.SetPixelColor(i, seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0));
        }

        if (seg.Now - seg.Aux0 <= seg.Step) return;
        if (Rng.Next8((uint)((255 - seg.Intensity) >> 4)) == 0)
            seg.SetPixelColor(Rng.Next16((uint)seg.Length), seg.Color(1));
        seg.Step = seg.Now;
        seg.Aux0 = (ushort)(255 - seg.Speed);
    }

    /// <summary>Like <see cref="FlashSparkle"/>, but a third of the segment flashes at once.</summary>
    public static void HyperSparkle(Segment seg)
    {
        if (!seg.Check2)
        {
            for (int i = 0; i < seg.Length; i++)
                seg.SetPixelColor(i, seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0));
        }

        if (seg.Now - seg.Aux0 <= seg.Step) return;
        if (Rng.Next8((uint)((255 - seg.Intensity) >> 4)) == 0)
        {
            int count = System.Math.Max(1, seg.Length / 3);
            for (int i = 0; i < count; i++) seg.SetPixelColor(Rng.Next16((uint)seg.Length), seg.Color(1));
        }
        seg.Step = seg.Now;
        seg.Aux0 = (ushort)(255 - seg.Speed);
    }

    /// <summary>Lights one random pixel per frame, with a probability set by <paramref name="intensity"/>.</summary>
    internal static void GlitterBase(Segment seg, byte intensity, Rgbw color)
    {
        if (intensity > Rng.Next8()) seg.SetPixelColor(Rng.Next16((uint)seg.Length), color);
    }

    /// <summary>A scrolling palette background with sparkles on top.</summary>
    public static void Glitter(Segment seg)
    {
        if (!seg.Check2) // unchecked means "draw a background too"
        {
            uint counter = 0;
            if (seg.Speed != 0)
            {
                counter = (seg.Now * (uint)((seg.Speed >> 3) + 1)) & 0xFFFF;
                counter >>= 8;
            }

            bool noWrap = !seg.PaletteMovingWrap;
            for (int i = 0; i < seg.Length; i++)
            {
                int colorIndex = (int)(i * 255 / seg.Length - counter);
                if (noWrap) colorIndex = FastMath.Map(colorIndex, 0, 255, 0, 240); // stop blending at the palette end
                seg.SetPixelColor(i, seg.ColorFromPalette(colorIndex, false, true, 255));
            }
        }
        GlitterBase(seg, seg.Intensity, seg.Color(2).IsBlack ? Colors.UltraWhite : seg.Color(2));
    }

    /// <summary>A solid background with sparkles on top.</summary>
    public static void SolidGlitter(Segment seg)
    {
        seg.Fill(seg.Color(0));
        GlitterBase(seg, seg.Intensity, seg.Color(2).IsBlack ? Colors.UltraWhite : seg.Color(2));
    }

    /// <summary>
    /// Pixels light up in a palette colour, brighten to full and fade away again.
    /// </summary>
    /// <remarks>
    /// One bit per pixel records whether it is currently rising or falling; the colour itself lives
    /// in the pixel buffer, which is what gives the effect its very low state cost.
    /// </remarks>
    public static void ColorTwinkle(Segment seg)
    {
        byte[] rising = seg.GetData<byte>((seg.Length + 7) >> 3);

        if (seg.Now - seg.Step < 1000 / LedStrip.DefaultFps) return; // hold a steady rate
        seg.Step = seg.Now;

        byte brightness = seg.Strip?.Brightness ?? 255;
        var fadeUpAmount = (byte)(brightness > 28 ? 8 + (seg.Speed >> 2) : 68 - brightness);
        var fadeDownAmount = (byte)(brightness > 28 ? 8 + (seg.Speed >> 3) : 68 - brightness);

        for (int i = 0; i < seg.Length; i++)
        {
            Rgbw current = seg.GetPixelColor(i);
            int index = i >> 3;
            int bit = i & 0x07;
            bool fadeUp = (rising[index] & (1 << bit)) != 0;

            if (fadeUp)
            {
                Rgbw increment = current.Fade(fadeUpAmount, video: true);
                Rgbw col = current.Add(increment);
                if (col.R == 255 || col.G == 255 || col.B == 255) rising[index] &= (byte)~(1 << bit);
                // a saturated add can be a no-op; doubling unsticks the pixel
                if (col == current) col = col.Add(col);
                seg.SetPixelColor(i, col);
            }
            else seg.SetPixelColor(i, current.Fade((byte)(255 - fadeDownAmount)));
        }

        for (int j = 0; j <= seg.Length / 50; j++)
        {
            if (Rng.Next8() > seg.Intensity) continue;
            for (int attempt = 0; attempt < 5; attempt++) // spawn at most one new pixel per 50
            {
                int i = Rng.Next16((uint)seg.Length);
                if (!seg.GetPixelColor(i).IsBlack) continue;
                rising[i >> 3] |= (byte)(1 << (i & 0x07));
                // the inverse gamma gives the non-linear fade the effect was designed around
                seg.SetPixelColor(i, ColorUtil.ColorFromPalette(
                    seg.CurrentPalette, Rng.Next8(), Gamma.RawInverse8(64), BlendType.NoBlend));
                break;
            }
        }
    }

    /// <summary>
    /// A short twinkle with a smooth fade in and out.
    /// </summary>
    /// <remarks>
    /// Uses a fixed seed so each pixel keeps its own phase from frame to frame without any stored
    /// state; the generator seed is saved and restored so other effects are unaffected.
    /// </remarks>
    public static void TwinkleUp(Segment seg)
    {
        var prng = new Prng(535);

        for (int i = 0; i < seg.Length; i++)
        {
            byte start = prng.Next8(); // this pixel phase, identical on every frame
            var pixBri = FastMath.Sin8((byte)(start + 16 * seg.Now / (uint)(256 - seg.Speed)));
            if (prng.Next8() > seg.Intensity) pixBri = 0;
            seg.SetPixelColor(i, Rgbw.Blend(seg.Color(1),
                seg.ColorFromPalette((int)(prng.Next8() + seg.Now / 100), false, seg.PaletteSolidWrap, 0), pixBri));
        }
    }

    /// <summary>
    /// One pixel of TwinkleFOX: brightness as a function of a per-pixel clock.
    /// </summary>
    private static Rgbw TwinkleFoxOne(Segment seg, uint ms, byte salt, bool cat)
    {
        uint ticks = ms / System.Math.Max(seg.Aux0, (ushort)1);
        var fastCycle = (byte)ticks;
        var slowCycle16 = (ushort)((ticks >> 8) + salt);
        slowCycle16 += FastMath.Sin8((byte)slowCycle16);
        slowCycle16 = (ushort)(slowCycle16 * 2053 + 1384);
        var slowCycle8 = (byte)((slowCycle16 & 0xFF) + (slowCycle16 >> 8));

        int twinkleDensity = (seg.Intensity >> 5) + 1; // 0 none lit, 8 all lit at once

        int bright = 0;
        if ((slowCycle8 & 0x0E) / 2 < twinkleDensity)
        {
            int ph = fastCycle;
            if (cat)
            {
                // twinklecat: snap on and fade off, or the reverse
                bright = seg.Check2 ? ph : 255 - ph;
            }
            else if (ph < 86) bright = ph * 3; // fast attack, slow decay
            else
            {
                ph -= 86;
                bright = 255 - (ph + ph / 2);
            }
        }

        var hue = (byte)(slowCycle8 - salt);
        if (bright <= 0) return Rgbw.Black;

        // the inverse gamma keeps the non-linear fade the original was designed around
        Rgbw c = ColorUtil.ColorFromPalette(seg.CurrentPalette, hue, Gamma.RawInverse8((byte)bright), BlendType.NoBlend);
        if (!seg.Check1 && fastCycle >= 128)
        {
            // fading pixels drift towards red, the way an incandescent bulb cools
            var cooling = (byte)((fastCycle - 128) >> 4);
            c = new Rgbw(c.R, FastMath.QSub8(c.G, cooling), FastMath.QSub8(c.B, (byte)(cooling * 2)), c.W);
        }
        return c;
    }

    /// <summary>Holiday lights that fade in and out, each pixel on its own clock.</summary>
    private static void TwinkleFoxBase(Segment seg, bool cat)
    {
        // the generator must restart from the same value every frame so each pixel keeps its phase
        ushort prng = 11337;

        seg.Aux0 = seg.Speed > 100
            ? (ushort)(3 + ((255 - seg.Speed) >> 3))
            : (ushort)(22 + ((100 - seg.Speed) >> 1));

        // the effect predates gamma correction, so the background is dimmed through the inverse table
        Rgbw bg = seg.Color(1);
        int bgLight = bg.RgbAverage;
        bg = bgLight > 64 ? bg.Fade(Gamma.RawInverse8(16), true)
           : bgLight > 16 ? bg.Fade(Gamma.RawInverse8(64), true)
           : bg.Fade(Gamma.RawInverse8(86), true);
        bgLight = bg.RgbAverage;

        for (int i = 0; i < seg.Length; i++)
        {
            prng = (ushort)(prng * 2053 + 1384);
            ushort clockOffset = prng;
            prng = (ushort)(prng * 2053 + 1384);
            // clock speed multiplier in eighths, from 8/8 to 23/8
            uint speedMultiplier = (uint)(((((prng & 0xFF) >> 4) + (prng & 0x0F)) & 0x0F) + 0x08);
            uint clock = ((seg.Now * speedMultiplier) >> 3) + clockOffset;
            var salt = (byte)(prng >> 8);

            Rgbw c = TwinkleFoxOne(seg, clock, salt, cat);

            int brightness = c.RgbAverage;
            int delta = brightness - bgLight;
            if (delta >= 32 || bg.IsBlack) seg.SetPixelColor(i, c);
            else if (delta > 0) seg.SetPixelColor(i, Rgbw.Blend(bg, c, (byte)(delta * 8)));
            else seg.SetPixelColor(i, bg);
        }
    }

    /// <summary>Twinkling holiday lights that fade in and out.</summary>
    public static void TwinkleFox(Segment seg) => TwinkleFoxBase(seg, false);

    /// <summary>Twinkling lights that snap on and fade away.</summary>
    public static void TwinkleCat(Segment seg) => TwinkleFoxBase(seg, true);

    /// <summary>State of one fairy light: when its current state started, how long it lasts, and whether it is on.</summary>
    private struct Flasher
    {
        public ushort StateStart;
        public byte StateDuration;
        public bool StateOn;
    }

    private const int FlashersPerZone = 6;
    private const int MaxShimmer = 92;

    /// <summary>
    /// A field of palette colour with a scattering of flashing "fairy" bulbs.
    /// </summary>
    /// <remarks>
    /// Bulbs are grouped into zones, and the pixels of a zone dim slightly while several of its
    /// bulbs are lit - the same shimmer a real string shows when it draws more current.
    /// </remarks>
    public static void Fairy(Segment seg)
    {
        // a fixed seed keeps the background colours identical from frame to frame
        var prng = (ushort)(5100 + (seg.Strip?.CurrentSegmentId ?? 0));
        for (int i = 0; i < seg.Length; i++)
        {
            prng = (ushort)(prng * 2053 + 1384);
            seg.SetPixelColor(i, seg.ColorFromPalette(prng >> 8, false, false, 0));
        }

        if (seg.Intensity == 0) return;
        int flasherDistance = (255 - seg.Intensity) / 28 + 1; // 1-10
        int numFlashers = seg.Length / flasherDistance + 1;

        Flasher[] flashers = seg.GetData<Flasher>(numFlashers);
        var now16 = (ushort)(seg.Now & 0xFFFF);

        int zones = System.Math.Max(numFlashers / FlashersPerZone, 1);
        int flashersInZone = numFlashers / zones;
        Span<byte> flasherBri = stackalloc byte[FlashersPerZone * 2 - 1];

        for (int z = 0; z < zones; z++)
        {
            int briSum = 0;
            int firstFlasher = z * flashersInZone;
            if (z == zones - 1) flashersInZone = numFlashers - flashersInZone * (zones - 1);

            for (int f = firstFlasher; f < firstFlasher + flashersInZone; f++)
            {
                int stateTime = (ushort)(now16 - flashers[f].StateStart);
                if (stateTime > flashers[f].StateDuration * 10)
                {
                    flashers[f].StateOn = !flashers[f].StateOn;
                    flashers[f].StateDuration = flashers[f].StateOn
                        ? (byte)(12 + Rng.Next8((uint)(12 + ((255 - seg.Speed) >> 2))))
                        : (byte)(20 + Rng.Next8((uint)(6 + ((255 - seg.Speed) >> 2))));
                    flashers[f].StateStart = now16;
                    if (stateTime < 255)
                    {
                        // back-date the start so the brightness ramp picks up where it left off
                        flashers[f].StateStart -= (ushort)(255 - stateTime);
                        flashers[f].StateDuration += (byte)(26 - stateTime / 10);
                        stateTime = 255 - stateTime;
                    }
                    else stateTime = 0;
                }
                if (stateTime > 255) stateTime = 255; // the ramp only covers the first 255ms of a state
                int slot = f - firstFlasher;
                if (slot >= flasherBri.Length) continue;
                flasherBri[slot] = (byte)(flashers[f].StateOn ? stateTime : 255 - stateTime);
                briSum += flasherBri[slot];
            }

            // the more bulbs are lit, the dimmer everything else gets
            int avgFlasherBri = briSum / System.Math.Max(flashersInZone, 1);
            var globalPeakBri = (byte)(255 - ((avgFlasherBri * MaxShimmer) >> 8)); // 183-255

            for (int f = firstFlasher; f < firstFlasher + flashersInZone; f++)
            {
                int slot = f - firstFlasher;
                if (slot >= flasherBri.Length) continue;
                var bri = (byte)(flasherBri[slot] * globalPeakBri / 255);
                prng = (ushort)(prng * 2053 + 1384);
                int flasherPos = f * flasherDistance;
                seg.SetPixelColor(flasherPos, Rgbw.Blend(seg.Color(1),
                    seg.ColorFromPalette(prng >> 8, false, false, 0), bri));
                for (int i = flasherPos + 1; i < flasherPos + flasherDistance && i < seg.Length; i++)
                {
                    prng = (ushort)(prng * 2053 + 1384);
                    seg.SetPixelColor(i, seg.ColorFromPalette(prng >> 8, false, false, 0, globalPeakBri));
                }
            }
        }
    }

    /// <summary>
    /// Every pixel a fairy light: all start lit and fade in and out independently.
    /// </summary>
    public static void FairyTwinkle(Segment seg)
    {
        Flasher[] flashers = seg.GetData<Flasher>(seg.Length);
        var now16 = (ushort)(seg.Now & 0xFFFF);
        var prng = (ushort)(5100 + (seg.Strip?.CurrentSegmentId ?? 0));

        int riseFallTime = 400 + (255 - seg.Speed) * 3;
        int maxDuration = riseFallTime / 100 + ((255 - seg.Intensity) >> 2) + 13 + ((255 - seg.Intensity) >> 1);

        for (int f = 0; f < seg.Length; f++)
        {
            var stateTime = (ushort)(now16 - flashers[f].StateStart);
            if (stateTime > flashers[f].StateDuration * 100)
            {
                flashers[f].StateOn = !flashers[f].StateOn;
                bool init = flashers[f].StateDuration == 0;
                flashers[f].StateDuration = flashers[f].StateOn
                    ? (byte)(riseFallTime / 100 + ((255 - seg.Intensity) >> 2) + Rng.Next8((uint)(12 + ((255 - seg.Intensity) >> 1))) + 1)
                    : (byte)(riseFallTime / 100 + Rng.Next8((uint)(3 + ((255 - seg.Speed) >> 6))) + 1);
                flashers[f].StateStart = now16;
                stateTime = 0;
                if (init)
                {
                    flashers[f].StateStart -= (ushort)riseFallTime; // the string starts fully lit
                    flashers[f].StateDuration = (byte)(riseFallTime / 100 + Rng.Next8((uint)(12 + ((255 - seg.Intensity) >> 1))) + 5);
                    stateTime = (ushort)riseFallTime;
                }
            }
            // react promptly when the intensity slider is moved
            if (flashers[f].StateOn && flashers[f].StateDuration > maxDuration)
                flashers[f].StateDuration = (byte)maxDuration;
            if (stateTime > riseFallTime) stateTime = (ushort)riseFallTime;

            var fadeProgress = (byte)(255 - stateTime * 255 / riseFallTime);
            byte flasherBri = flashers[f].StateOn
                ? (byte)(255 - Gamma.Raw8(fadeProgress))
                : Gamma.Raw8(fadeProgress);

            int lastR = prng;
            int diff = 0;
            while (diff < 0x4000) // keep neighbouring pixels visibly different
            {
                prng = (ushort)(prng * 2053 + 1384);
                diff = prng > lastR ? prng - lastR : lastR - prng;
            }
            seg.SetPixelColor(f, Rgbw.Blend(seg.Color(1),
                seg.ColorFromPalette(prng >> 8, false, false, 0), flasherBri));
        }
    }
}
