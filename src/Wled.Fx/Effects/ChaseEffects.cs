namespace Wled.Fx;

/// <summary>
/// Effects built around something travelling along the segment: chases, scanners, comets and rain.
/// Port of the corresponding block of <c>FX.cpp</c>.
/// </summary>
public static class ChaseEffects
{
    internal static void Register()
    {
        EffectRegistry.Register(EffectId.MultiStrobe, "Strobe Mega@!,!;!,!;!;01", MultiStrobe);
        EffectRegistry.Register(EffectId.Android, "Android@!,Width;!,!;!;;m12=1", Android);
        EffectRegistry.Register(EffectId.ChaseColor, "Chase@!,Width;!,!,!;!", ChaseColor);
        EffectRegistry.Register(EffectId.ChaseRandom, "Chase Random@!,Width;!,,!;!", ChaseRandom);
        EffectRegistry.Register(EffectId.ChaseRainbow, "Chase Rainbow@!,Width;!,!;!", ChaseRainbow);
        EffectRegistry.Register(EffectId.ChaseRainbowWhite, "Rainbow Runner@!,Size;Bg;!", ChaseRainbowWhite);
        EffectRegistry.Register(EffectId.Colorful, "Colorful@!,Saturation;1,2,3;!", Colorful);
        EffectRegistry.Register(EffectId.TrafficLight, "Traffic Light@!,US style;,!;!", TrafficLight);
        EffectRegistry.Register(EffectId.ChaseFlash, "Chase Flash@!;Bg,Fx;!", ChaseFlash);
        EffectRegistry.Register(EffectId.ChaseFlashRandom, "Chase Flash Rnd@!;!,!;!", ChaseFlashRandom);
        EffectRegistry.Register(EffectId.RunningColor, "Chase 2@!,Width;!,!;!", RunningColor);
        EffectRegistry.Register(EffectId.RunningRandom, "Stream@!,Zone size;;!", RunningRandom);
        EffectRegistry.Register(EffectId.LarsonScanner, "Scanner@!,Trail,Delay,,,Dual,Bi-delay;!,!,!;!;;m12=0,c1=0", LarsonScanner);
        EffectRegistry.Register(EffectId.DualLarsonScanner, "Scanner Dual@!,Trail,Delay,,,Dual,Bi-delay;!,!,!;!;;m12=0,c1=0", DualLarsonScanner);
        EffectRegistry.Register(EffectId.Comet, "Lighthouse@!,Fade rate;!,!;!", Comet);
        EffectRegistry.Register(EffectId.Fireworks, "Fireworks@,Frequency;!,!;!;12;ix=192,pal=11", Fireworks);
        EffectRegistry.Register(EffectId.Rain, "Rain@!,Spawning rate;!,!;!;12;ix=128,pal=0", Rain);
        EffectRegistry.Register(EffectId.FireFlicker, "Fire Flicker@!,!;!;!;01", FireFlicker);
        EffectRegistry.Register(EffectId.TwoDots, "Two Dots@!,Dot size,,,,,Overlay;1,2,Bg;!", TwoDots);
    }

    /// <summary>Bursts of strobe flashes separated by pauses; intensity sets the burst length.</summary>
    public static void MultiStrobe(Segment seg)
    {
        for (int i = 0; i < seg.Length; i++)
            seg.SetPixelColor(i, seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 1));

        seg.Aux0 = (ushort)(50 + 20 * (255 - seg.Speed));
        int count = 2 * (seg.Intensity / 10 + 1);
        if (seg.Aux1 < count)
        {
            if ((seg.Aux1 & 1) == 0)
            {
                seg.Fill(seg.Color(0));
                seg.Aux0 = 15;
            }
            else seg.Aux0 = 50;
        }

        if (seg.Now - seg.Aux0 > seg.Step)
        {
            seg.Aux1++;
            if (seg.Aux1 > count) seg.Aux1 = 0;
            seg.Step = seg.Now;
        }
    }

    /// <summary>The loading circle of a well-known mobile platform: an arc that grows, then shrinks.</summary>
    public static void Android(Segment seg)
    {
        uint[] state = seg.GetData<uint>(1);
        int size = seg.Aux1 >> 1;              // upper 15 bits hold the arc length
        bool shrinking = (seg.Aux1 & 0x01) != 0; // lowest bit holds the direction

        if (seg.Now >= seg.Step)
        {
            seg.Step = seg.Now + 3 + (uint)(8 * (255 - seg.Speed) / seg.Length);
            if (size > seg.Intensity * seg.Length / 255) shrinking = true;
            else if (size < 2) shrinking = false;

            if (!shrinking)
            {
                // advancing the start only every third frame is what makes the arc grow
                if (state[0] % 3 == 1) seg.Aux0++;
                else size++;
            }
            else
            {
                seg.Aux0++;
                if (state[0] % 3 != 1) size--;
            }
            seg.Aux1 = (ushort)(size << 1 | (shrinking ? 1 : 0));
            state[0]++;
            if (seg.Aux0 >= seg.Length) seg.Aux0 = 0;
        }

        int start = seg.Aux0;
        int end = (seg.Aux0 + size) % seg.Length;
        for (int i = 0; i < seg.Length; i++)
        {
            bool inArc = start < end ? i >= start && i < end : i >= start || i < end;
            seg.SetPixelColor(i, inArc ? seg.Color(0) : seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 1));
        }
    }

    /// <summary>
    /// Two adjacent bands of colour running along a background.
    /// </summary>
    /// <param name="seg">The segment to render into.</param>
    /// <param name="color1">Background colour.</param>
    /// <param name="color2">Leading band.</param>
    /// <param name="color3">Trailing band.</param>
    /// <param name="usePalette">Paint the background from the palette instead of <paramref name="color1"/>.</param>
    /// <param name="randomColor">Pick a new background colour on every lap.</param>
    private static void ChaseBase(Segment seg, Rgbw color1, Rgbw color2, Rgbw color3,
                                  bool usePalette, bool randomColor = false)
    {
        var counter = (ushort)(seg.Now * (uint)((seg.Speed >> 2) + 1));
        int a = counter * seg.Length >> 16;

        if (randomColor)
        {
            if (a < seg.Step) // wrapped around, so pick the next colour
            {
                seg.Aux1 = seg.Aux0;
                seg.Aux0 = Rng.NextWheelIndex((byte)seg.Aux0);
            }
            color1 = seg.ColorWheel((byte)seg.Aux0);
        }
        seg.Step = (uint)a;

        int size = 1 + ((seg.Intensity * seg.Length) >> 10); // up to half the segment

        int b = a + size;
        if (b > seg.Length) b -= seg.Length;
        int c = b + size;
        if (c > seg.Length) c -= seg.Length;

        if (usePalette)
        {
            for (int i = 0; i < seg.Length; i++)
                seg.SetPixelColor(i, seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 1));
        }
        else seg.Fill(color1);

        if (randomColor) // the part of the lap not yet reached keeps the previous colour
        {
            color1 = seg.ColorWheel((byte)seg.Aux1);
            for (int i = a; i < seg.Length; i++) seg.SetPixelColor(i, color1);
        }

        FillWrapped(seg, a, b, color2);
        FillWrapped(seg, b, c, color3);
    }

    private static void FillWrapped(Segment seg, int from, int to, Rgbw color)
    {
        if (from < to)
        {
            for (int i = from; i < to; i++) seg.SetPixelColor(i, color);
        }
        else
        {
            for (int i = from; i < seg.Length; i++) seg.SetPixelColor(i, color);
            for (int i = 0; i < to; i++) seg.SetPixelColor(i, color);
        }
    }

    /// <summary>Two bands of the primary colour chasing along a palette background.</summary>
    public static void ChaseColor(Segment seg)
        => ChaseBase(seg, seg.Color(1), seg.Color(2).IsBlack ? seg.Color(0) : seg.Color(2), seg.Color(0), true);

    /// <summary>A chase that leaves a fresh random background behind it on every lap.</summary>
    public static void ChaseRandom(Segment seg)
        => ChaseBase(seg, seg.Color(1), seg.Color(2).IsBlack ? seg.Color(0) : seg.Color(2), seg.Color(0), false, true);

    /// <summary>A chase over a background that cycles through the rainbow.</summary>
    public static void ChaseRainbow(Segment seg)
    {
        int colorSeparation = 256 / seg.Length;
        if (colorSeparation == 0) colorSeparation = 1; // segments longer than 256 pixels
        var colorIndex = (byte)(seg.Call & 0xFF);
        Rgbw color = seg.ColorWheel((byte)((seg.Step * colorSeparation + colorIndex) & 0xFF));
        ChaseBase(seg, color, seg.Color(0), seg.Color(1), false);
    }

    /// <summary>The primary colour chasing along a rainbow.</summary>
    public static void ChaseRainbowWhite(Segment seg)
    {
        int n = (int)seg.Step;
        int m = ((int)seg.Step + 1) % seg.Length;
        Rgbw color2 = seg.ColorWheel((byte)((n * 256 / seg.Length + (seg.Call & 0xFF)) & 0xFF));
        Rgbw color3 = seg.ColorWheel((byte)((m * 256 / seg.Length + (seg.Call & 0xFF)) & 0xFF));
        ChaseBase(seg, seg.Color(0), color2, color3, false);
    }

    /// <summary>Bands of red, amber, green and blue marching along the segment.</summary>
    public static void Colorful(Segment seg)
    {
        int numColors = 4;
        Span<Rgbw> colors = stackalloc Rgbw[9];
        colors[0] = 0x00FF0000u;
        colors[1] = 0x00EEBB00u;
        colors[2] = 0x0000EE00u;
        colors[3] = 0x000077CCu;

        if (seg.Intensity > 160 || seg.Palette != 0)
        {
            if (seg.Palette == 0)
            {
                numColors = 3;
                for (int i = 0; i < 3; i++) colors[i] = seg.Color(i);
            }
            else
            {
                int spacing = 80;
                if (seg.Palette == 52) { numColors = 5; spacing = 61; } // "C9 2" has five colours
                for (int i = 0; i < numColors; i++) colors[i] = seg.ColorFromPalette(i * spacing, false, true, 255);
            }
        }
        else if (seg.Intensity < 80) // pastel colours
        {
            colors[0] = 0x00FF8040u;
            colors[1] = 0x00E5D241u;
            colors[2] = 0x0077FF77u;
            colors[3] = 0x0077F0F0u;
        }
        for (int i = numColors; i < numColors * 2 - 1; i++) colors[i] = colors[i - numColors];

        uint cycleTime = (uint)(50 + 8 * (255 - seg.Speed));
        uint it = seg.Now / cycleTime;
        if (it != seg.Step)
        {
            if (seg.Speed > 0) seg.Aux0++;
            if (seg.Aux0 >= numColors) seg.Aux0 = 0;
            seg.Step = it;
        }

        for (int i = 0; i < seg.Length; i += numColors)
        {
            for (int j = 0; j < numColors; j++) seg.SetPixelColor(i + j, colors[seg.Aux0 + j]);
        }
    }

    /// <summary>Repeating red / amber / green groups running the traffic light sequence.</summary>
    public static void TrafficLight(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        for (int i = 0; i < seg.Length; i++)
            seg.SetPixelColor(i, seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 1));

        uint delay = 500;
        for (int i = 0; i < seg.Length - 2; i += 3)
        {
            switch (seg.Aux0)
            {
                case 0:
                    seg.SetPixelColor(i, 0x00FF0000u);
                    delay = (uint)(150 + 100 * (255 - seg.Speed));
                    break;
                case 1:
                    seg.SetPixelColor(i, 0x00FF0000u);
                    seg.SetPixelColor(i + 1, 0x00EECC00u);
                    delay = (uint)(150 + 20 * (255 - seg.Speed));
                    break;
                case 2:
                    seg.SetPixelColor(i + 2, 0x0000FF00u);
                    delay = (uint)(150 + 100 * (255 - seg.Speed));
                    break;
                case 3:
                    // inverse gamma keeps the amber matching the pre-0.16 look
                    seg.SetPixelColor(i + 1, Gamma.Inverse(0x00EECC00u));
                    delay = (uint)(150 + 20 * (255 - seg.Speed));
                    break;
            }
        }

        if (seg.Now - seg.Step > delay)
        {
            seg.Aux0++;
            if (seg.Aux0 == 1 && seg.Intensity > 140) seg.Aux0 = 2; // US sequence: skip red plus amber
            if (seg.Aux0 > 3) seg.Aux0 = 0;
            seg.Step = seg.Now;
        }
    }

    private const int FlashCount = 4;

    /// <summary>A pair of pixels that flash in place, then step along the segment.</summary>
    public static void ChaseFlash(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        uint now = seg.Now;
        bool advance = true;
        int flashStep = seg.Aux1 % (FlashCount * 2 + 1);
        // render every frame for smooth transitions, but only step the animation when due
        if (now < seg.Step) advance = false;
        else seg.Aux1++;

        for (int i = 0; i < seg.Length; i++)
            seg.SetPixelColor(i, seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0));

        int n = seg.Aux0;
        int m = (seg.Aux0 + 1) % seg.Length;

        uint delay = (uint)(10 + 30 * (255 - seg.Speed) / seg.Length);
        if (flashStep < FlashCount * 2)
        {
            if (flashStep % 2 == 0)
            {
                seg.SetPixelColor(n, seg.Color(1));
                seg.SetPixelColor(m, seg.Color(1));
                delay = 20;
            }
            else delay = 30;
        }
        else if (advance) seg.Aux0 = (ushort)m;

        if (advance) seg.Step = now + delay;
    }

    /// <summary>Flashing pixels that step along, filling the segment with a random colour behind them.</summary>
    public static void ChaseFlashRandom(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        uint now = seg.Now;
        bool advance = true;
        if (now < seg.Step)
        {
            seg.Call--; // undo the engine increment so the same frame is simply re-rendered
            advance = false;
        }
        uint flashStep = seg.Call % (FlashCount * 2 + 1);

        for (int i = 0; i < seg.Aux1; i++) seg.SetPixelColor(i, seg.ColorWheel((byte)seg.Aux0));

        uint delay = (uint)(1 + 10 * (255 - seg.Speed) / seg.Length);
        if (flashStep < FlashCount * 2)
        {
            int n = seg.Aux1;
            int m = (seg.Aux1 + 1) % seg.Length;
            if (flashStep % 2 == 0)
            {
                seg.SetPixelColor(n, seg.Color(0));
                seg.SetPixelColor(m, seg.Color(0));
                delay = 20;
            }
            else
            {
                seg.SetPixelColor(n, seg.ColorWheel((byte)seg.Aux0));
                seg.SetPixelColor(m, seg.Color(1));
                delay = 30;
            }
        }
        else if (advance)
        {
            seg.Aux1 = (ushort)((seg.Aux1 + 1) % seg.Length);
            if (seg.Aux1 == 0) seg.Aux0 = Rng.NextWheelIndex((byte)seg.Aux0);
        }

        if (advance) seg.Step = now + delay;
    }

    /// <summary>Alternating runs of the two segment colours, marching along.</summary>
    public static void RunningColor(Segment seg) => RunningTwoColor(seg, seg.Color(0), seg.Color(1));

    private static void RunningTwoColor(Segment seg, Rgbw color1, Rgbw color2)
    {
        int width = 1 + (seg.Intensity >> 4);
        uint cycleTime = (uint)(50 + (255 - seg.Speed));
        uint it = seg.Now / cycleTime;
        bool usePalette = color1 == seg.Color(0);

        for (int i = 0; i < seg.Length; i++)
        {
            Rgbw col = color2;
            if (usePalette) color1 = seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0);
            int pos = i % (width << 1);
            if (pos < seg.Aux0 - width || (pos >= seg.Aux0 && pos < seg.Aux0 + width)) col = color1;
            seg.SetPixelColor(i, col);
        }

        if (it != seg.Step)
        {
            seg.Aux0 = (ushort)((seg.Aux0 + 1) % (width << 1));
            seg.Step = it;
        }
    }

    /// <summary>Bands of random colour streaming along the segment.</summary>
    public static void RunningRandom(Segment seg)
    {
        uint cycleTime = (uint)(25 + 3 * (255 - seg.Speed));
        uint it = seg.Now / cycleTime;
        if (seg.Call == 0) seg.Aux0 = Rng.Next16();

        int zoneSize = ((255 - seg.Intensity) >> 4) + 1;
        var prng = (ushort)seg.Aux0;

        int z = (int)(it % (uint)zoneSize);
        bool newZone = z == 0 && it != seg.Aux1;
        for (int i = seg.Length - 1; i >= 0; i--)
        {
            if (newZone || z >= zoneSize)
            {
                int lastRandom = prng >> 8;
                int diff = 0;
                while (System.Math.Abs(diff) < 42) // keep neighbouring bands visibly different
                {
                    prng = (ushort)(prng * 2053 + 13849);
                    diff = (prng >> 8) - lastRandom;
                }
                if (newZone)
                {
                    seg.Aux0 = prng; // remember the seed the next frame should start from
                    newZone = false;
                }
                z = 0;
            }
            seg.SetPixelColor(i, seg.ColorWheel((byte)(prng >> 8)));
            z++;
        }

        seg.Aux1 = (ushort)it;
    }

    /// <summary>A block of light sweeping back and forth, leaving a trail. Also known as K.I.T.T.</summary>
    public static void LarsonScanner(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        int speed = seg.FrameTime * FastMath.Map(seg.Speed, 0, 255, 96, 2);
        int pixels = seg.Length / System.Math.Max(speed, 1); // pixels to advance this frame

        seg.FadeOut((byte)(255 - seg.Intensity));

        if (seg.Step > seg.Now) return; // pausing at the end of a sweep

        int index = seg.Aux1 + pixels;
        if (pixels == 0) // slower than one pixel per frame, so count frames instead
        {
            int frames = speed / seg.Length;
            if (seg.Step++ < frames) return;
            seg.Step = 0;
            index++;
        }

        if (index > seg.Length)
        {
            seg.Aux0 = (ushort)(seg.Aux0 != 0 ? 0 : 1); // turn around
            seg.Aux1 = 0;
            seg.Step = seg.Aux0 != 0 || seg.Check2 ? seg.Now + (uint)(seg.Custom1 * 25) : 0;
            return;
        }

        for (int i = seg.Aux1; i < index; i++)
        {
            int j = seg.Aux0 != 0 ? i : seg.Length - 1 - i;
            Rgbw c = seg.ColorFromPalette(j, true, seg.PaletteSolidWrap, 0);
            seg.SetPixelColor(j, c);
            if (seg.Check1) seg.SetPixelColor(seg.Length - 1 - j, seg.Color(2).IsBlack ? c : seg.Color(2));
        }
        seg.Aux1 = (ushort)index;
    }

    /// <summary>Two scanners sweeping in opposite directions.</summary>
    public static void DualLarsonScanner(Segment seg)
    {
        seg.Check1 = true;
        LarsonScanner(seg);
    }

    /// <summary>A bright head sweeping along the segment with a fading tail.</summary>
    public static void Comet(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        uint counter = (seg.Now * (uint)((seg.Speed >> 2) + 1)) & 0xFFFF;
        int index = (int)(counter * seg.Length >> 16);
        if (seg.Call == 0) seg.Aux0 = (ushort)index;

        seg.FadeOut(seg.Intensity);

        seg.SetPixelColor(index, seg.ColorFromPalette(index, true, seg.PaletteSolidWrap, 0));
        if (index > seg.Aux0)
        {
            // fill in the pixels skipped since the last frame so fast comets stay continuous
            for (int i = seg.Aux0; i < index; i++)
                seg.SetPixelColor(i, seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0));
        }
        else if (index < seg.Aux0 && index < 10)
        {
            for (int i = 0; i < index; i++)
                seg.SetPixelColor(i, seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0));
        }
        seg.Aux0 = (ushort)index;
    }

    /// <summary>Random sparks that flare and blur away.</summary>
    public static void Fireworks(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        int width = seg.Is2D ? seg.Width : seg.Length;
        int height = seg.Height;

        if (seg.Call == 0)
        {
            seg.Aux0 = ushort.MaxValue;
            seg.Aux1 = ushort.MaxValue;
        }
        seg.FadeOut(128);

        int x = seg.Aux0 % width, y = seg.Aux0 / width; // the 2D position is packed into aux0
        if (seg.Step == 0)
        {
            // blur the flares but keep the spark cores sharp, so they read as points of light
            bool valid1 = seg.Aux0 < width * height;
            bool valid2 = seg.Aux1 < width * height;
            Rgbw sv1 = Rgbw.Black, sv2 = Rgbw.Black;
            if (valid1) sv1 = seg.Is2D ? seg.GetPixelColorXY(x, y) : seg.GetPixelColor(seg.Aux0);
            if (valid2) sv2 = seg.Is2D ? seg.GetPixelColorXY(x, y) : seg.GetPixelColor(seg.Aux1);
            seg.Blur(16);
            if (valid1)
            {
                if (seg.Is2D) seg.SetPixelColorXY(x, y, sv1);
                else seg.SetPixelColor(seg.Aux0, sv1);
            }
            if (valid2)
            {
                if (seg.Is2D) seg.SetPixelColorXY(x, y, sv2);
                else seg.SetPixelColor(seg.Aux1, sv2);
            }
        }

        for (int i = 0; i < System.Math.Max(1, width / 20); i++)
        {
            if (Rng.Next8((uint)(129 - (seg.Intensity >> 1))) != 0) continue;
            int index = Rng.Next16((uint)(width * height));
            x = index % width;
            y = index / width;
            Rgbw col = seg.ColorFromPalette(Rng.Next8(), false, false, 0);
            if (seg.Is2D) seg.SetPixelColorXY(x, y, col);
            else seg.SetPixelColor(index, col);
            seg.Aux1 = seg.Aux0;       // the previous spark
            seg.Aux0 = (ushort)index;  // where this one landed
        }
    }

    /// <summary>Sparks that drift down the matrix, or along the strip, like falling rain.</summary>
    public static void Rain(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        int width = seg.Width;
        int height = seg.Height;
        seg.Step += (uint)seg.FrameTime;

        int speedFormula = 5 + 50 * (255 - seg.Speed) / seg.Length;
        if (seg.Call != 0 && seg.Step > speedFormula)
        {
            seg.Step = 1;
            if (seg.Is2D)
            {
                seg.Move(6, 1, true); // shift everything down a row
                seg.Aux0 = (ushort)(seg.Aux0 % width + (seg.Aux0 / width + 1) * width);
                seg.Aux1 = (ushort)(seg.Aux1 % width + (seg.Aux1 / width + 1) * width);
            }
            else
            {
                Rgbw wrap = seg.GetPixelColor(0);
                for (int i = 0; i < seg.Length - 1; i++) seg.SetPixelColor(i, seg.GetPixelColor(i + 1));
                seg.SetPixelColor(seg.Length - 1, wrap);
                seg.Aux0++;
                seg.Aux1++;
            }
            if (seg.Aux0 == 0) seg.Aux0 = ushort.MaxValue; // forget the previous spark position
            if (seg.Aux1 == 0) seg.Aux0 = ushort.MaxValue;
            if (seg.Aux0 >= width * height) seg.Aux0 = 0;
            if (seg.Aux1 >= width * height) seg.Aux1 = 0;
        }
        Fireworks(seg);
    }

    /// <summary>A warm, randomly flickering glow.</summary>
    public static void FireFlicker(Segment seg)
    {
        uint cycleTime = (uint)(40 + (255 - seg.Speed));
        uint it = seg.Now / cycleTime;
        if (seg.Step == it) return;

        Rgbw baseColor = seg.Color(0);
        byte w = baseColor.W, r = baseColor.R, g = baseColor.G, b = baseColor.B;
        byte lum = seg.Palette == 0
            ? System.Math.Max(w, System.Math.Max(r, System.Math.Max(g, b)))
            : (byte)255;
        lum /= (byte)((256 - seg.Intensity) / 16 + 1);

        for (int i = 0; i < seg.Length; i++)
        {
            byte flicker = Rng.Next8(lum);
            if (seg.Palette == 0)
            {
                seg.SetPixelColor(i, new Rgbw(
                    System.Math.Max(r - flicker, 0),
                    System.Math.Max(g - flicker, 0),
                    System.Math.Max(b - flicker, 0),
                    System.Math.Max(w - flicker, 0)));
            }
            else seg.SetPixelColor(i, seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0, (byte)(255 - flicker)));
        }

        seg.Step = it;
    }

    /// <summary>Two dots chasing each other around the segment, half a lap apart.</summary>
    public static void TwoDots(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        int delay = 1 + (seg.FrameTime << 3) / seg.Length; // longer segments should move faster
        uint it = seg.Now / (uint)System.Math.Max(FastMath.Map(seg.Speed, 0, 255, delay << 4, delay), 1);
        int offset = (int)(it % (uint)seg.Length);
        int width = (seg.Length * (seg.Intensity + 1)) >> 9; // at most half the segment
        if (width == 0) width = 1;

        if (!seg.Check2) seg.Fill(seg.Color(2));
        Rgbw color1 = seg.Color(0);
        Rgbw color2 = seg.Color(1) == seg.Color(2) ? color1 : seg.Color(1);

        for (int i = 0; i < width; i++)
        {
            seg.SetPixelColor((offset + i) % seg.Length, color1);
            seg.SetPixelColor((offset + i + (seg.Length >> 1)) % seg.Length, color2);
        }
    }
}
