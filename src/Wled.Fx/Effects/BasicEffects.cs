namespace Wled.Fx;

/// <summary>
/// The foundational effects: solid colour, blinking, wipes, fades and rainbows.
/// Port of the first block of effects in <c>FX.cpp</c>.
/// </summary>
public static class BasicEffects
{
    internal static void Register()
    {
        EffectRegistry.Register(EffectId.Static, "Solid", Static);
        EffectRegistry.Register(EffectId.Copy, "Copy Segment@,Color shift,Lighten,Brighten,ID,Axis(2D),FullStack(last frame);;;12;ix=0,c1=0,c2=0,c3=0", CopySegment);
        EffectRegistry.Register(EffectId.Blink, "Blink@!,Duty cycle;!,!;!;01", Blink);
        EffectRegistry.Register(EffectId.BlinkRainbow, "Blink Rainbow@Frequency,Blink duration;!,!;!;01", BlinkRainbow);
        EffectRegistry.Register(EffectId.Strobe, "Strobe@!;!,!;!;01", Strobe);
        EffectRegistry.Register(EffectId.StrobeRainbow, "Strobe Rainbow@!;,!;!;01", StrobeRainbow);
        EffectRegistry.Register(EffectId.ColorWipe, "Wipe@!,!;!,!;!", ColorWipe);
        EffectRegistry.Register(EffectId.ColorSweep, "Sweep@!,!;!,!;!", ColorSweep);
        EffectRegistry.Register(EffectId.ColorWipeRandom, "Wipe Random@!;;!", ColorWipeRandom);
        EffectRegistry.Register(EffectId.ColorSweepRandom, "Sweep Random@!;;!", ColorSweepRandom);
        EffectRegistry.Register(EffectId.RandomColor, "Random Colors@!,Fade time;;!;01", RandomColor);
        EffectRegistry.Register(EffectId.Dynamic, "Dynamic@!,!,,,,Smooth;;!", Dynamic);
        EffectRegistry.Register(EffectId.DynamicSmooth, "Dynamic Smooth@!,!;;!", DynamicSmooth);
        EffectRegistry.Register(EffectId.Breath, "Breathe@!;!,!;!;01", Breath);
        EffectRegistry.Register(EffectId.Fade, "Fade@!;!,!;!;01", Fade);
        EffectRegistry.Register(EffectId.Scan, "Scan@!,# of dots,,,,,Overlay;!,!,!;!", Scan);
        EffectRegistry.Register(EffectId.DualScan, "Scan Dual@!,# of dots,,,,,Overlay;!,!,!;!", DualScan);
        EffectRegistry.Register(EffectId.Rainbow, "Colorloop@!,Saturation;;!;01", Rainbow);
        EffectRegistry.Register(EffectId.RainbowCycle, "Rainbow@!,Size;;!", RainbowCycle);
        EffectRegistry.Register(EffectId.TheaterChase, "Theater@!,Gap size;!,!;!", TheaterChase);
        EffectRegistry.Register(EffectId.TheaterChaseRainbow, "Theater Rainbow@!,Gap size;,!;!", TheaterChaseRainbow);
        EffectRegistry.Register(EffectId.RunningLights, "Running@!,Wave width;!,!;!", RunningLights);
        EffectRegistry.Register(EffectId.RunningDual, "Running Dual@!,Wave width;L,!,R;!", RunningDual);
        EffectRegistry.Register(EffectId.Saw, "Saw@!,Width;!,!;!", Saw);
        EffectRegistry.Register(EffectId.StaticPattern, "Solid Pattern@Fg size,Bg size;Fg,!;!;;pal=0", StaticPattern);
        EffectRegistry.Register(EffectId.TriStaticPattern, "Solid Pattern Tri@,Size;1,2,3;;;pal=0", TriStaticPattern);
        EffectRegistry.Register(EffectId.Gradient, "Gradient@!,Spread;!,!;!;;ix=16", Gradient);
        EffectRegistry.Register(EffectId.Loading, "Loading@!,Fade;!,!;!;;ix=16", Loading);
        EffectRegistry.Register(EffectId.Palette, "Palette@Shift,Size,Rotation,,,Animate Shift,Animate Rotation,Anamorphic;;!;12;ix=112,c1=0,o1=1,o2=0,o3=1", PaletteEffect);
        EffectRegistry.Register(EffectId.Spots, "Spots@Spread,Width,,,,,Overlay;!,!;!", Spots);
        EffectRegistry.Register(EffectId.SpotsFade, "Spots Fade@Spread,Width,,,,,Overlay;!,!;!", SpotsFade);
    }

    /// <summary>Plain, unchanging light in the primary colour.</summary>
    public static void Static(Segment seg) => seg.Fill(seg.Color(0));

    /// <summary>
    /// Mirrors another segment, optionally shifting its hue, saturation and brightness.
    /// </summary>
    /// <remarks>
    /// Reads either the source segment own buffer or, with checkbox 3, the last frame that reached
    /// the strip - the latter picks up everything drawn below the source segment as well.
    /// </remarks>
    public static void CopySegment(Segment seg)
    {
        LedStrip? strip = seg.Strip;
        if (strip is null) return;

        int sourceId = seg.Custom3;
        if (sourceId >= strip.Segments.Count || ReferenceEquals(strip.GetSegment(sourceId), seg))
        {
            seg.FadeToBlackBy(5); // no valid source, so fade out
            return;
        }

        Segment source = strip.GetSegment(sourceId);
        if (!source.IsActive) return;

        if (source.Is2D)
        {
            // a 2D source copied onto a 1D segment contributes only its first row (or column)
            for (int y = 0; y < seg.Height; y++)
            {
                for (int x = 0; x < seg.Width; x++)
                {
                    int sx = x, sy = y;
                    if (seg.Check1) (sx, sy) = (sy, sx);
                    Rgbw color;
                    if (seg.Check2) color = strip.GetPixelColorXY(sx + source.Start, sy + source.StartY);
                    else
                    {
                        source.SetDrawDimensions();
                        color = source.GetPixelColorXY(sx, sy);
                    }
                    color = ColorUtil.AdjustColor(color, seg.Intensity, seg.Custom1, seg.Custom2);
                    seg.SetDrawDimensions();
                    seg.SetPixelColorXY(x, y, color);
                }
            }
            return;
        }

        for (int i = 0; i < seg.Length; i++)
        {
            Rgbw color;
            if (seg.Check2) color = strip.GetPixelColor(i + source.Start);
            else
            {
                source.SetDrawDimensions();
                color = source.GetPixelColor(i);
            }
            color = ColorUtil.AdjustColor(color, seg.Intensity, seg.Custom1, seg.Custom2);
            seg.SetDrawDimensions();
            seg.SetPixelColor(i, color);
        }
    }

    /// <summary>
    /// Alternates between two colours. <paramref name="strobe"/> shortens the on phase to a single
    /// frame regardless of the duty cycle.
    /// </summary>
    private static void BlinkBase(Segment seg, Rgbw color1, Rgbw color2, bool strobe, bool usePalette)
    {
        uint cycleTime = (uint)((255 - seg.Speed) * 20);
        uint onTime = (uint)seg.FrameTime;
        if (!strobe) onTime += (cycleTime * seg.Intensity) >> 8;
        cycleTime += (uint)(seg.FrameTime * 2);
        uint it = seg.Now / cycleTime;
        uint rem = seg.Now % cycleTime;

        // force one frame on at every iteration boundary, even when the on time is vanishingly short
        bool on = it != seg.Step || rem <= onTime;
        seg.Step = it;

        Rgbw color = on ? color1 : color2;
        if (color == color1 && usePalette)
        {
            for (int i = 0; i < seg.Length; i++)
                seg.SetPixelColor(i, seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0));
        }
        else seg.Fill(color);
    }

    /// <summary>Blinks between the two segment colours; intensity sets the duty cycle.</summary>
    public static void Blink(Segment seg) => BlinkBase(seg, seg.Color(0), seg.Color(1), false, true);

    /// <summary>Blinks while cycling the primary colour through the rainbow.</summary>
    public static void BlinkRainbow(Segment seg)
        => BlinkBase(seg, seg.ColorWheel((byte)(seg.Call & 0xFF)), seg.Color(1), false, false);

    /// <summary>Single-frame flashes of the primary colour.</summary>
    public static void Strobe(Segment seg) => BlinkBase(seg, seg.Color(0), seg.Color(1), true, true);

    /// <summary>Single-frame flashes cycling through the rainbow.</summary>
    public static void StrobeRainbow(Segment seg)
        => BlinkBase(seg, seg.ColorWheel((byte)(seg.Call & 0xFF)), seg.Color(1), true, false);

    /// <summary>
    /// Fills the segment one pixel at a time, then clears it again.
    /// </summary>
    /// <param name="reverse">Clear from the far end rather than the near one.</param>
    /// <param name="useRandomColors">Pick a fresh random colour for each pass.</param>
    private static void ColorWipeBase(Segment seg, bool reverse, bool useRandomColors)
    {
        if (seg.Length <= 1) { Static(seg); return; }

        uint cycleTime = (uint)(750 + (255 - seg.Speed) * 150);
        uint perc = seg.Now % cycleTime;
        int prog = (int)(perc * 65535 / cycleTime);
        bool back = prog > 32767;
        if (back)
        {
            prog -= 32767;
            if (seg.Step == 0) seg.Step = 1;
        }
        else if (seg.Step == 2) seg.Step = 3; // ask for a colour change

        if (useRandomColors)
        {
            if (seg.Call == 0)
            {
                seg.Aux0 = Rng.Next8();
                seg.Step = 3;
            }
            if (seg.Step == 1)
            {
                seg.Aux1 = Rng.NextWheelIndex((byte)seg.Aux0);
                seg.Step = 2;
            }
            if (seg.Step == 3)
            {
                seg.Aux0 = Rng.NextWheelIndex((byte)seg.Aux1);
                seg.Step = 0;
            }
        }

        int ledIndex = prog * seg.Length >> 15;
        int rem = (ushort)(prog * seg.Length * 2); // mod 0xFFFF by truncation
        rem /= seg.Intensity + 1;
        if (rem > 255) rem = 255;

        Rgbw col1 = useRandomColors ? seg.ColorWheel((byte)seg.Aux1) : seg.Color(1);
        for (int i = 0; i < seg.Length; i++)
        {
            int index = reverse && back ? seg.Length - 1 - i : i;
            Rgbw col0 = useRandomColors
                ? seg.ColorWheel((byte)seg.Aux0)
                : seg.ColorFromPalette(index, true, seg.PaletteSolidWrap, 0);

            if (i < ledIndex) seg.SetPixelColor(index, back ? col1 : col0);
            else
            {
                seg.SetPixelColor(index, back ? col0 : col1);
                if (i == ledIndex)
                    seg.SetPixelColor(index, Rgbw.Blend(back ? col0 : col1, back ? col1 : col0, (byte)rem));
            }
        }
    }

    /// <summary>Lights the segment up one pixel at a time.</summary>
    public static void ColorWipe(Segment seg) => ColorWipeBase(seg, false, false);

    /// <summary>Lights the segment up one pixel at a time, clearing from the far end.</summary>
    public static void ColorSweep(Segment seg) => ColorWipeBase(seg, true, false);

    /// <summary>A wipe in a fresh random colour every pass.</summary>
    public static void ColorWipeRandom(Segment seg) => ColorWipeBase(seg, false, true);

    /// <summary>A sweep in a fresh random colour every pass.</summary>
    public static void ColorSweepRandom(Segment seg) => ColorWipeBase(seg, true, true);

    /// <summary>Fades the whole segment from one random colour to the next.</summary>
    public static void RandomColor(Segment seg)
    {
        uint cycleTime = (uint)(200 + (255 - seg.Speed) * 50);
        uint it = seg.Now / cycleTime;
        uint rem = seg.Now % cycleTime;
        uint fadeDuration = (cycleTime * seg.Intensity) >> 8;

        uint fade = 255;
        if (fadeDuration != 0) fade = System.Math.Min(rem * 255 / fadeDuration, 255);

        if (seg.Call == 0)
        {
            seg.Aux0 = Rng.Next8();
            seg.Step = 2;
        }
        if (it != seg.Step) // time for a new colour
        {
            seg.Aux1 = seg.Aux0;
            seg.Aux0 = Rng.NextWheelIndex((byte)seg.Aux0);
            seg.Step = it;
        }

        seg.Fill(Rgbw.Blend(seg.ColorWheel((byte)seg.Aux1), seg.ColorWheel((byte)seg.Aux0), (byte)fade));
    }

    /// <summary>Every pixel in its own random colour, all of them changing together.</summary>
    public static void Dynamic(Segment seg)
    {
        byte[] hues = seg.GetData<byte>(seg.Length);

        if (seg.Call == 0)
        {
            for (int i = 0; i < seg.Length; i++) hues[i] = Rng.Next8();
        }

        uint cycleTime = (uint)(50 + (255 - seg.Speed) * 15);
        uint it = seg.Now / cycleTime;
        if (it != seg.Step && seg.Speed != 0)
        {
            for (int i = 0; i < seg.Length; i++)
            {
                if (Rng.Next8() <= seg.Intensity) hues[i] = Rng.Next8();
            }
            seg.Step = it;
        }

        if (seg.Check1)
        {
            for (int i = 0; i < seg.Length; i++) seg.BlendPixelColor(i, seg.ColorWheel(hues[i]), 16);
        }
        else
        {
            for (int i = 0; i < seg.Length; i++) seg.SetPixelColor(i, seg.ColorWheel(hues[i]));
        }
    }

    /// <summary><see cref="Dynamic"/> with the colour changes eased rather than snapped.</summary>
    public static void DynamicSmooth(Segment seg)
    {
        bool previous = seg.Check1;
        seg.Check1 = true;
        Dynamic(seg);
        seg.Check1 = previous;
    }

    /// <summary>The standby breathing of a well-known family of devices.</summary>
    public static void Breath(Segment seg)
    {
        int variance = 0;
        uint counter = (seg.Now * (uint)((seg.Speed >> 3) + 10)) & 0xFFFF;
        counter = (counter >> 2) + (counter >> 4); // 0-16384 plus 0-2048
        if (counter < 16384)
        {
            if (counter > 8192) counter = 8192 - (counter - 8192);
            variance = FastMath.Sin16((ushort)counter) / 103; // near-parabolic over 0-8192, peaking at 23170
        }

        var lum = (byte)(30 + variance);
        for (int i = 0; i < seg.Length; i++)
            seg.SetPixelColor(i, Rgbw.Blend(seg.Color(1), seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0), lum));
    }

    /// <summary>Cross-fades back and forth between the two segment colours.</summary>
    public static void Fade(Segment seg)
    {
        var counter = (ushort)(seg.Now * (uint)((seg.Speed >> 3) + 10));
        var lum = (byte)(FastMath.TriWave16(counter) >> 8);

        for (int i = 0; i < seg.Length; i++)
            seg.SetPixelColor(i, Rgbw.Blend(seg.Color(1), seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0), lum));
    }

    /// <summary>Runs a block of pixels back and forth, optionally mirrored.</summary>
    private static void ScanBase(Segment seg, bool dual)
    {
        if (seg.Length <= 1) { Static(seg); return; }

        uint cycleTime = (uint)(750 + (255 - seg.Speed) * 150);
        uint perc = seg.Now % cycleTime;
        int prog = (int)(perc * 65535 / cycleTime);
        int size = 1 + ((seg.Intensity * seg.Length) >> 9);
        int ledIndex = (prog * (seg.Length * 2 - size * 2)) >> 16;

        if (!seg.Check2) seg.Fill(seg.Color(1));

        int offset = System.Math.Abs(ledIndex - (seg.Length - size));

        if (dual)
        {
            for (int j = offset; j < offset + size; j++)
            {
                int i2 = seg.Length - 1 - j;
                seg.SetPixelColor(i2, seg.ColorFromPalette(i2, true, seg.PaletteSolidWrap, seg.Color(2).IsBlack ? (byte)0 : (byte)2));
            }
        }

        for (int j = offset; j < offset + size; j++)
            seg.SetPixelColor(j, seg.ColorFromPalette(j, true, seg.PaletteSolidWrap, 0));
    }

    /// <summary>A dot running back and forth.</summary>
    public static void Scan(Segment seg) => ScanBase(seg, false);

    /// <summary>Two dots running in opposite directions.</summary>
    public static void DualScan(Segment seg) => ScanBase(seg, true);

    /// <summary>The whole segment cycling through the rainbow together.</summary>
    public static void Rainbow(Segment seg)
    {
        uint counter = (seg.Now * (uint)((seg.Speed >> 2) + 2)) & 0xFFFF;
        counter >>= 8;

        if (seg.Intensity < 128)
            seg.Fill(Rgbw.Blend(seg.ColorWheel((byte)counter), Colors.White, (byte)(128 - seg.Intensity)));
        else
            seg.Fill(seg.ColorWheel((byte)counter));
    }

    /// <summary>A rainbow spread along the segment and scrolling.</summary>
    public static void RainbowCycle(Segment seg)
    {
        uint counter = (seg.Now * (uint)((seg.Speed >> 2) + 2)) & 0xFFFF;
        counter >>= 8;

        for (int i = 0; i < seg.Length; i++)
        {
            // intensity/29 selects how many rainbows fit: 0 is a sixteenth, 4 is one, 8 is sixteen
            var index = (byte)(i * (16 << (seg.Intensity / 29)) / seg.Length + counter);
            seg.SetPixelColor(i, seg.ColorWheel(index));
        }
    }

    /// <summary>Every n-th pixel lit and marching along the segment.</summary>
    private static void RunningBase(Segment seg, Rgbw color1, Rgbw color2, bool theatre = false)
    {
        int width = (theatre ? 3 : 1) + (seg.Intensity >> 4);
        uint cycleTime = (uint)(50 + (255 - seg.Speed));
        uint it = seg.Now / cycleTime;
        bool usePalette = color1 == seg.Color(0);

        for (int i = 0; i < seg.Length; i++)
        {
            Rgbw col = color2;
            if (usePalette) color1 = seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0);
            if (theatre)
            {
                if (i % width == seg.Aux0) col = color1;
            }
            else
            {
                int pos = i % (width << 1);
                if (pos < seg.Aux0 - width || (pos >= seg.Aux0 && pos < seg.Aux0 + width)) col = color1;
            }
            seg.SetPixelColor(i, col);
        }

        if (it != seg.Step)
        {
            seg.Aux0 = (ushort)((seg.Aux0 + 1) % (theatre ? width : width << 1));
            seg.Step = it;
        }
    }

    /// <summary>Theatre-style crawling lights.</summary>
    public static void TheaterChase(Segment seg) => RunningBase(seg, seg.Color(0), seg.Color(1), true);

    /// <summary>Theatre-style crawling lights that cycle through the rainbow.</summary>
    public static void TheaterChaseRainbow(Segment seg)
        => RunningBase(seg, seg.ColorWheel((byte)seg.Step), seg.Color(1), true);

    /// <summary>Smoothly running waves, either sinusoidal or sawtooth.</summary>
    private static void RunningWave(Segment seg, bool saw, bool dual = false)
    {
        int xScale = seg.Intensity >> 2;
        uint counter = (seg.Now * seg.Speed) >> 9;

        for (int i = 0; i < seg.Length; i++)
        {
            uint a = (uint)(i * xScale) - counter;
            if (saw)
            {
                a &= 0xFF;
                // stretch the ramp so the leading edge is steep and the trailing edge is long
                a = a < 16 ? 192 + a * 8 : (uint)FastMath.Map((int)a, 16, 255, 64, 192);
                a = 255 - a;
            }
            byte s = dual ? SinGap((ushort)a) : FastMath.Sin8((byte)a);
            Rgbw ca = Rgbw.Blend(seg.Color(1), seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0), s);
            if (dual)
            {
                uint b = (uint)((seg.Length - 1 - i) * xScale) - counter;
                byte t = SinGap((ushort)b);
                Rgbw cb = Rgbw.Blend(seg.Color(1), seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 2), t);
                ca = Rgbw.Blend(ca, cb, 127);
            }
            seg.SetPixelColor(i, ca);
        }
    }

    /// <summary>Half a sine followed by a flat gap, so waves read as separate pulses.</summary>
    internal static byte SinGap(ushort input)
    {
        if ((input & 0x100) != 0) return 0;
        return FastMath.Sin8((byte)(input + 192)); // phase-shifted so it starts and ends at zero
    }

    /// <summary>Smooth sinusoidal waves running along the segment.</summary>
    public static void RunningLights(Segment seg) => RunningWave(seg, false);

    /// <summary>Waves running in from both ends at once.</summary>
    public static void RunningDual(Segment seg) => RunningWave(seg, false, true);

    /// <summary>Sawtooth waves running along the segment.</summary>
    public static void Saw(Segment seg) => RunningWave(seg, true);

    /// <summary>Alternating runs of foreground and background; speed sets one, intensity the other.</summary>
    public static void StaticPattern(Segment seg)
    {
        int lit = 1 + seg.Speed;
        int unlit = 1 + seg.Intensity;
        bool drawingLit = true;
        int count = 0;

        for (int i = 0; i < seg.Length; i++)
        {
            seg.SetPixelColor(i, drawingLit ? seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0) : seg.Color(1));
            count++;
            if (count >= (drawingLit ? lit : unlit))
            {
                count = 0;
                drawingLit = !drawingLit;
            }
        }
    }

    /// <summary>Equal runs of all three segment colours, repeating.</summary>
    public static void TriStaticPattern(Segment seg)
    {
        int segSize = (seg.Intensity >> 5) + 1;
        int currSeg = 0;
        int currSegCount = 0;

        for (int i = 0; i < seg.Length; i++)
        {
            seg.SetPixelColor(i, seg.Color(currSeg % 3));
            currSegCount++;
            if (currSegCount >= segSize)
            {
                currSeg++;
                currSegCount = 0;
            }
        }
    }

    /// <summary>A band of palette colour travelling along the segment.</summary>
    /// <param name="loading">
    /// Sweep in one direction with a hard edge behind it, rather than fading away on both sides.
    /// </param>
    private static void GradientBase(Segment seg, bool loading)
    {
        if (seg.Length <= 1) { Static(seg); return; }

        var counter = (ushort)(seg.Now * (uint)((seg.Speed >> 2) + 1));
        int pp = counter * seg.Length >> 16;
        if (seg.Call == 0) pp = 0;
        int border = 1 + (loading ? seg.Intensity / 2 : seg.Intensity / 4);
        int p1 = pp - seg.Length;
        int p2 = pp + seg.Length;

        for (int i = 0; i < seg.Length; i++)
        {
            int val = loading
                ? System.Math.Abs((i > pp ? p2 : pp) - i)
                : System.Math.Min(System.Math.Abs(pp - i), System.Math.Min(System.Math.Abs(p1 - i), System.Math.Abs(p2 - i)));
            val = border > val ? val * 255 / border : 255;
            seg.SetPixelColor(i, Rgbw.Blend(seg.Color(0), seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 1), (byte)val));
        }
    }

    /// <summary>A band of colour sliding back and forth.</summary>
    public static void Gradient(Segment seg) => GradientBase(seg, false);

    /// <summary>A band of colour sweeping in one direction, like a loading bar.</summary>
    public static void Loading(Segment seg) => GradientBase(seg, true);

    /// <summary>
    /// The palette itself, painted across the segment as a rotatable gradient.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The effect rotates a rectangle filled left-to-right with the palette and then samples the
    /// display out of it. The rectangle is scaled to exactly span the display at the current angle,
    /// so no solid bands appear at the edges and the whole palette stays visible.
    /// </para>
    /// <para>
    /// On a plain strip each <em>segment</em> stands in for one row of an imaginary matrix, which is
    /// why the 1D path renders only the row belonging to this segment.
    /// </para>
    /// </remarks>
    public static void PaletteEffect(Segment seg)
    {
        LedStrip? strip = seg.Strip;
        bool isMatrix = strip?.IsMatrix ?? false;
        int cols = seg.Width;
        int rows = isMatrix ? seg.Height : strip?.ActiveSegmentCount ?? 1;

        int inputShift = seg.Speed;
        int inputSize = seg.Intensity;
        int inputRotation = seg.Custom1;
        bool animateShift = seg.Check1;
        bool animateRotation = seg.Check2;
        bool assumeSquare = seg.Check3;

        float theta = !animateRotation
            ? (inputRotation + 128) * (FastMath.Pi / 256.0f)
            : ((seg.Now * (uint)((inputRotation >> 4) + 1)) & 0xFFFF) * (FastMath.TwoPi / 0xFFFF);
        float sinTheta = FastMath.Sin(theta);
        float cosTheta = FastMath.Cos(theta);

        float maxX = System.Math.Max(1, cols - 1);
        float maxY = System.Math.Max(1, rows - 1);
        // "anamorphic" mode keeps the palette square instead of stretching it to the display
        float maxXIn = assumeSquare ? maxX : 1f;
        float maxYIn = assumeSquare ? maxY : 1f;
        float maxXOut = !assumeSquare ? maxX : 1f;
        float maxYOut = !assumeSquare ? maxY : 1f;
        float centerX = maxXOut / 2f;
        float centerY = maxYOut / 2f;
        float scale = System.Math.Abs(sinTheta) + System.Math.Abs(cosTheta) * maxYOut / maxXOut;

        int yFrom = isMatrix ? 0 : strip?.CurrentSegmentId ?? 0;
        int yTo = isMatrix ? (int)maxY : yFrom;

        for (int y = yFrom; y <= yTo; y++)
        {
            float ytCosTheta = cosTheta * (y - centerY * maxYIn) / (maxYIn * scale);
            for (int x = 0; x < cols; x++)
            {
                float xtSinTheta = sinTheta * (x - centerX * maxXIn) / (maxXIn * scale);
                // every point at a given x has the same colour, so the y coordinate only rotates it in
                float sourceX = xtSinTheta + ytCosTheta + centerX;
                var colorIndex = (int)(System.Math.Clamp(sourceX, 0f, maxXOut) * 255 / maxXOut);

                // below 128 the slider shows a fraction of the palette, above it several repetitions
                colorIndex = inputSize <= 128
                    ? colorIndex * inputSize / 128
                    : (inputSize - 112) * colorIndex / 16; // maps 128 to 1 and 256 to 9 repetitions

                int paletteOffset = !animateShift
                    ? inputShift
                    : (int)(((seg.Now * (uint)((inputShift >> 3) + 1)) & 0xFFFF) >> 8);
                colorIndex -= paletteOffset;

                Rgbw color = seg.ColorWheel((byte)colorIndex);
                if (isMatrix) seg.SetPixelColorXY(x, y, color);
                else seg.SetPixelColor(x, color);
            }
        }
    }

    /// <summary>Blocks of palette colour with a soft edge, spread evenly along the segment.</summary>
    /// <param name="threshold">How much of each block is lit; lower values light more of it.</param>
    internal static void SpotsBase(Segment seg, int threshold)
    {
        if (seg.Length <= 1) { Static(seg); return; }
        if (!seg.Check2) seg.Fill(seg.Color(1));

        int maxZones = seg.Length >> 2;
        int zones = 1 + ((seg.Intensity * maxZones) >> 8);
        int zoneLen = seg.Length / zones;
        int offset = (seg.Length - zones * zoneLen) >> 1;

        for (int z = 0; z < zones; z++)
        {
            int pos = offset + z * zoneLen;
            for (int i = 0; i < zoneLen; i++)
            {
                int wave = FastMath.TriWave16((ushort)(i * 0xFFFF / zoneLen));
                if (wave <= threshold) continue;
                int index = pos + i;
                int s = (wave - threshold) * 255 / (0xFFFF - threshold);
                seg.SetPixelColor(index, Rgbw.Blend(
                    seg.ColorFromPalette(index, true, seg.PaletteSolidWrap, 0), seg.Color(1), (byte)(255 - s)));
            }
        }
    }

    /// <summary>Evenly spaced blocks of light; speed sets their width, intensity their count.</summary>
    public static void Spots(Segment seg) => SpotsBase(seg, (255 - seg.Speed) << 8);

    /// <summary>Like <see cref="Spots"/>, but the blocks breathe in and out.</summary>
    public static void SpotsFade(Segment seg)
    {
        var counter = (ushort)(seg.Now * (uint)((seg.Speed >> 2) + 8));
        int t = FastMath.TriWave16(counter);
        SpotsBase(seg, (t >> 1) + (t >> 2));
    }
}
