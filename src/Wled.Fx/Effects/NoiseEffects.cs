namespace Wled.Fx;

/// <summary>
/// Effects driven by Perlin noise and by sine fields: the noise family, plasma, waves and clouds.
/// Port of the corresponding blocks of <c>FX.cpp</c>.
/// </summary>
public static class NoiseEffects
{
    internal static void Register()
    {
        EffectRegistry.Register(EffectId.Fillnoise8, "Fill Noise@!;!;!", FillNoise8);
        EffectRegistry.Register(EffectId.Noise161, "Noise 1@!;!;!;;pal=20", Noise16A);
        EffectRegistry.Register(EffectId.Noise162, "Noise 2@!;!;!;;pal=43", Noise16B);
        EffectRegistry.Register(EffectId.Noise163, "Noise 3@!;!;!;;pal=35", Noise16C);
        EffectRegistry.Register(EffectId.Noise164, "Noise 4@!;!;!;;pal=26", Noise16D);
        EffectRegistry.Register(EffectId.Noisepal, "Noise Pal@!,Scale;;!", NoisePalette);
        EffectRegistry.Register(EffectId.Phased, "Phased@!,!;!,!;!", Phased);
        EffectRegistry.Register(EffectId.Phasednoise, "Phased Noise@!,!;!,!;!", PhasedNoise);
        EffectRegistry.Register(EffectId.Sinewave, "Sine@!,Scale;;!", SineWave);
        EffectRegistry.Register(EffectId.Perlinmove, "Perlin Move@!,# of pixels,Fade rate;!,!;!", PerlinMove);
        EffectRegistry.Register(EffectId.Wavesins, "Wavesins@!,Brightness variation,Starting color,Range of colors,Color variation;!;!", WaveSins);
        EffectRegistry.Register(EffectId.Flowstripe, "Flow Stripe@Hue speed,Effect speed;;!;pal=11", FlowStripe);
        EffectRegistry.Register(EffectId.Bpm, "Bpm@!;!;!;;sx=64", Bpm);
        EffectRegistry.Register(EffectId.Pride2015, "Pride 2015@!;;", Pride2015);
        EffectRegistry.Register(EffectId.Colorwaves, "Colorwaves@!,Hue;!;!;;pal=26", ColorWaves);
        EffectRegistry.Register(EffectId.Juggle, "Juggle@!,Trail;;!;;sx=64,ix=128", Juggle);
        EffectRegistry.Register(EffectId.Blends, "Blends@Shift speed,Blend speed;;!", Blends);
        EffectRegistry.Register(EffectId.Lake, "Lake@!;Fx;!", Lake);
        EffectRegistry.Register(EffectId.Plasma, "Plasma@Phase,!;!;!", Plasma);
        EffectRegistry.Register(EffectId.Flow, "Flow@!,Zones;;!;;m12=1", Flow);
        EffectRegistry.Register(EffectId.Chunchun, "Chunchun@!,Gap size;!,!;!", ChunChun);
        EffectRegistry.Register(EffectId.Colorclouds, "Color Clouds@!,!,Clouds,Colors,Distance,,,Cozy;;!;;sx=24,ix=32,c1=48,c2=64,c3=12,pal=0", ColorClouds);
    }

    /// <summary>Perlin noise mapped straight onto the palette.</summary>
    public static void FillNoise8(Segment seg)
    {
        if (seg.Call == 0) seg.Step = Rng.Next();
        for (int i = 0; i < seg.Length; i++)
        {
            byte index = Perlin.Noise8((ushort)(i * seg.Length), (ushort)(seg.Step + (uint)(i * seg.Length)));
            seg.SetPixelColor(i, seg.ColorFromPalette(index, false, seg.PaletteSolidWrap, 0));
        }
        seg.Step += Beat.Sin8(seg.Speed, 1, 6);
    }

    /// <summary>A noise field that swings along X while drifting along Y and Z.</summary>
    public static void Noise16A(Segment seg)
    {
        const int scale = 320; // zoom factor of the noise field
        seg.Step += (uint)(1 + seg.Speed / 16);

        for (int i = 0; i < seg.Length; i++)
        {
            uint shiftX = Beat.Sin8(11); // the X position swings at roughly 17 bpm
            uint shiftY = seg.Step / 42; // Y creeps forward
            uint realX = (uint)((i + shiftX) * scale);
            uint realY = (uint)((i + shiftY) * scale);
            uint realZ = seg.Step;
            uint noise = (uint)Perlin.Noise16(realX, realY, realZ) >> 8;
            byte index = FastMath.Sin8((byte)(noise * 3));
            seg.SetPixelColor(i, seg.ColorFromPalette(index, false, seg.PaletteSolidWrap, 0));
        }
    }

    /// <summary>A one-dimensional noise field scrolling past.</summary>
    public static void Noise16B(Segment seg)
    {
        const int scale = 1000;
        seg.Step += (uint)(1 + (seg.Speed >> 1));

        for (int i = 0; i < seg.Length; i++)
        {
            uint shiftX = seg.Step >> 6;
            uint realX = (uint)(i + shiftX) * scale;
            uint noise = (uint)Perlin.Noise16(realX, 0, 4223) >> 8;
            byte index = FastMath.Sin8((byte)(noise * 3));
            seg.SetPixelColor(i, seg.ColorFromPalette(index, false, seg.PaletteSolidWrap, 0, (byte)noise));
        }
    }

    /// <summary>A stationary noise field animated only through its Z axis.</summary>
    public static void Noise16C(Segment seg)
    {
        const int scale = 800;
        seg.Step += (uint)(1 + seg.Speed);

        for (int i = 0; i < seg.Length; i++)
        {
            uint realX = (uint)(i + 4223) * scale; // fixed offsets: no movement along X or Y
            uint realY = (uint)(i + 1234) * scale;
            uint realZ = seg.Step * 8;
            uint noise = (uint)Perlin.Noise16(realX, realY, realZ) >> 8;
            byte index = FastMath.Sin8((byte)(noise * 3));
            seg.SetPixelColor(i, seg.ColorFromPalette(index, false, seg.PaletteSolidWrap, 0, (byte)noise));
        }
    }

    /// <summary>A smooth noise gradient scrolling along the segment.</summary>
    public static void Noise16D(Segment seg)
    {
        uint stp = (seg.Now * seg.Speed) >> 7;
        for (int i = 0; i < seg.Length; i++)
        {
            int index = Perlin.Noise16((uint)i << 12, stp);
            seg.SetPixelColor(i, seg.ColorFromPalette(index, false, seg.PaletteSolidWrap, 0));
        }
    }

    /// <summary>
    /// Slow noise rendered through a palette that keeps morphing into fresh random ones.
    /// </summary>
    public static void NoisePalette(Segment seg)
    {
        int scale = 15 + (seg.Intensity >> 2);
        Palette16[] palettes = seg.GetObjects(2, () => new Palette16()); // [0] shown, [1] the target

        uint changePaletteMs = (uint)(4000 + seg.Speed * 10); // 4 to 6.5 seconds
        if (seg.Now - seg.Step > changePaletteMs)
        {
            seg.Step = seg.Now;
            byte baseHue = Rng.Next8();
            // the inverse gamma on the minimum brightness restores the vivid pre-0.16 palettes
            byte minBri = Gamma.RawInverse8(128);
            palettes[1] = new Palette16(
                new Chsv(baseHue + Rng.Next8(64), 255, Rng.Next8(minBri, 255)),
                new Chsv(baseHue + 128, 255, Rng.Next8(minBri, 255)),
                new Chsv(baseHue + Rng.Next8(92), 192, Rng.Next8(minBri, 255)),
                new Chsv(baseHue + Rng.Next8(92), 255, Rng.Next8(minBri, 255)));
        }

        palettes[0].BlendToward(palettes[1], 48);
        if (seg.Palette > 0) palettes[0].CopyFrom(seg.CurrentPalette);

        for (int i = 0; i < seg.Length; i++)
        {
            byte index = Perlin.Noise8((ushort)(i * scale), (ushort)(seg.Aux0 + i * scale));
            seg.SetPixelColor(i, ColorUtil.ColorFromPalette(palettes[0], index, 255, BlendType.LinearBlend));
        }

        seg.Aux0 += Beat.Sin8(10, 1, 4); // drift along the field, varied by a slow sine
    }

    /// <summary>
    /// Sine waves whose phase change accelerates along the segment.
    /// </summary>
    /// <param name="seg">The segment to render into.</param>
    /// <param name="moder">0 keeps the modulus fixed, 1 randomises it with Perlin noise.</param>
    private static void PhasedBase(Segment seg, byte moder)
    {
        const int allFreq = 16;
        // the phase is a float kept in the 32-bit step slot, exactly as the C++ original does
        float phase = BitConverter.UInt32BitsToSingle(seg.Step);
        int cutOff = 255 - seg.Intensity;
        int modVal = 5;

        int index = (int)(seg.Now / 64); // colour rotation speed
        phase += seg.Speed / 32.0f;
        seg.Step = BitConverter.SingleToUInt32Bits(phase);

        for (int i = 0; i < seg.Length; i++)
        {
            if (moder == 1) modVal = Perlin.Noise8((ushort)(i * 10 + i * 10)) / 16;
            int val = (i + 1) * allFreq; // the +1 makes sure pixel 0 is used
            if (modVal == 0) modVal = 1;
            val += (int)(phase * (i % modVal + 1) / 2);
            int b = FastMath.CubicWave8((byte)val);
            b = b > cutOff ? b - cutOff : 0;
            seg.SetPixelColor(i, Rgbw.Blend(seg.Color(1), seg.ColorFromPalette(index, false, false, 0), (byte)b));
            index += 256 / seg.Length;
            if (seg.Length > 256) index++; // correction for very long segments
        }
    }

    /// <summary>Sine waves with a phase change that varies along the segment.</summary>
    public static void Phased(Segment seg) => PhasedBase(seg, 0);

    /// <summary>Like <see cref="Phased"/>, with the wave spacing randomised by noise.</summary>
    public static void PhasedNoise(Segment seg) => PhasedBase(seg, 1);

    /// <summary>A travelling sine wave with adjustable frequency.</summary>
    public static void SineWave(Segment seg)
    {
        uint colorIndex = seg.Now / 32;
        seg.Step += (uint)(seg.Speed / 16);
        int freq = seg.Intensity / 4;

        for (int i = 0; i < seg.Length; i++)
        {
            byte pixBri = FastMath.CubicWave8((byte)(i * freq + seg.Step));
            seg.SetPixelColor(i, Rgbw.Blend(seg.Color(1),
                seg.ColorFromPalette((int)(i * colorIndex / 255), false, seg.PaletteSolidWrap, 0), pixBri));
        }
    }

    /// <summary>Pixels whose positions are driven by 16-bit Perlin noise rather than a sine.</summary>
    public static void PerlinMove(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        seg.FadeOut((byte)(255 - seg.Custom1));
        for (int i = 0; i < seg.Intensity / 16 + 1; i++)
        {
            uint t = seg.Now * 128 / (uint)(260 - seg.Speed);
            int locn = Perlin.Noise16(t + (uint)(i * 15000), t);
            // the noise rarely reaches its extremes, so only its usable middle is mapped
            int pixloc = FastMath.Map(locn, 50 * 256, 192 * 256, 0, seg.Length - 1);
            seg.SetPixelColor(pixloc, seg.ColorFromPalette(pixloc % 255, false, seg.PaletteSolidWrap, 0));
        }
    }

    /// <summary>Phase-shifted sine waves in both brightness and colour.</summary>
    public static void WaveSins(Segment seg)
    {
        for (int i = 0; i < seg.Length; i++)
        {
            byte bri = FastMath.Sin8((byte)(seg.Now / 4 + i * seg.Intensity));
            byte index = Beat.Sin8(seg.Speed, seg.Custom1, (byte)(seg.Custom1 + seg.Custom2), 0,
                                   (byte)(i * (seg.Custom3 << 3)));
            seg.SetPixelColor(i, seg.ColorFromPalette(index, false, seg.PaletteSolidWrap, 0, bri));
        }
    }

    /// <summary>A hue gradient flowing outward from the centre of the segment.</summary>
    public static void FlowStripe(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        int hl = seg.Length * 10 / 13;
        var hue = (byte)(seg.Now / (uint)(seg.Speed + 1));
        uint t = seg.Now / (uint)(seg.Intensity / 8 + 1);

        for (int i = 0; i < seg.Length; i++)
        {
            int c = System.Math.Abs(i - hl) * 127 / hl;
            c = FastMath.Sin8((byte)c);
            c = FastMath.Sin8((byte)(c / 2 + t));
            byte b = FastMath.Sin8((byte)(c + t / 8));
            seg.SetPixelColor(i, seg.ColorFromPalette((byte)(b + hue), false, true, 3));
        }
    }

    /// <summary>A palette sweep pulsing in time with a beat.</summary>
    public static void Bpm(Segment seg)
    {
        uint stp = (seg.Now / 20) & 0xFF;
        byte beat = Beat.Sin8(seg.Speed, 64, 255);
        for (int i = 0; i < seg.Length; i++)
        {
            seg.SetPixelColor(i, seg.ColorFromPalette((int)(stp + (uint)(i * 2)), false, seg.PaletteSolidWrap, 0,
                (byte)(beat - stp + (uint)(i * 10))));
        }
    }

    /// <summary>
    /// Ever-changing rainbows, either straight from the hue wheel (Pride) or through the palette
    /// (Colorwaves).
    /// </summary>
    /// <remarks>
    /// Every parameter of the wave - saturation, brightness depth, hue increment, time multiplier -
    /// is itself driven by a slow beat at a different prime-ish rate, which is what keeps the
    /// pattern from ever visibly repeating.
    /// </remarks>
    private static void ColorWavesPrideBase(Segment seg, bool isPride2015)
    {
        uint duration = (uint)(10 + seg.Speed);
        uint pseudotime = seg.Step;
        uint hue16Start = seg.Aux0;

        byte sat8 = isPride2015 ? (byte)Beat.Sin88(87, 220, 250) : (byte)255;
        uint brightDepth = Beat.Sin88(341, 96, 224);
        uint brightnessThetaInc16 = Beat.Sin88(203, 25 * 256, 40 * 256);
        uint msMultiplier = Beat.Sin88(147, 23, 60);

        uint hue16 = hue16Start;
        uint hueInc16 = isPride2015
            ? Beat.Sin88(113, 1, 3000)
            : (uint)(Beat.Sin88(113, 60, 300) * seg.Intensity * 10 / 255);

        pseudotime += duration * msMultiplier;
        hue16Start += duration * Beat.Sin88(400, 5, 9);
        uint brightnessTheta16 = pseudotime;

        for (int i = 0; i < seg.Length; i++)
        {
            hue16 += hueInc16;
            byte hue8;
            if (isPride2015) hue8 = (byte)(hue16 >> 8);
            else
            {
                // fold the hue so colour waves sweep back and forth through the palette
                uint h16128 = hue16 >> 7;
                hue8 = (h16128 & 0x100) != 0 ? (byte)(255 - (h16128 >> 1)) : (byte)(h16128 >> 1);
            }

            brightnessTheta16 += brightnessThetaInc16;
            uint b16 = (uint)(FastMath.Sin16((ushort)brightnessTheta16) + 32768);
            uint bri16 = b16 * b16 / 65536;
            var bri8 = (byte)(bri16 * brightDepth / 65536 + (255 - brightDepth));

            if (isPride2015)
            {
                Rgbw newColor = Gamma.Inverse(new Crgb(new Chsv(hue8, sat8, bri8)));
                seg.BlendPixelColor(i, newColor, 64);
            }
            else seg.BlendPixelColor(i, seg.ColorFromPalette(hue8, false, seg.PaletteSolidWrap, 0, bri8), 128);
        }

        seg.Step = pseudotime;
        seg.Aux0 = (ushort)hue16Start;
    }

    /// <summary>Animated, ever-changing rainbows.</summary>
    public static void Pride2015(Segment seg) => ColorWavesPrideBase(seg, true);

    /// <summary>Colour waves drawn through the selected palette.</summary>
    public static void ColorWaves(Segment seg) => ColorWavesPrideBase(seg, false);

    /// <summary>Eight coloured dots weaving in and out of sync.</summary>
    public static void Juggle(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        seg.FadeToBlackBy((byte)(192 - 3 * seg.Intensity / 4));
        byte dotHue = 0;
        for (int i = 0; i < 8; i++)
        {
            int index = Beat.Sin16((uint)((16 + seg.Speed) * (i + 7)), 0, (ushort)(seg.Length - 1));
            var existing = (Crgb)seg.GetPixelColor(index);
            Crgb dot = seg.Palette == 0
                ? new Crgb(new Chsv(dotHue, 220, 255))
                : (Crgb)ColorUtil.ColorFromPalette(seg.CurrentPalette, dotHue);
            seg.SetPixelColor(index, existing | dot); // brightest channel wins, so dots overlap cleanly
            dotHue += 32;
        }
    }

    /// <summary>Random palette colours slowly blending into one another along the segment.</summary>
    public static void Blends(Segment seg)
    {
        int pixelLen = System.Math.Min(seg.Length, 255);
        Rgbw[] pixels = seg.GetData<Rgbw>(pixelLen + 1);
        var blendSpeed = (byte)FastMath.Map(seg.Intensity, 0, 255, 10, 128);
        uint shift = (seg.Now * (uint)((seg.Speed >> 3) + 1)) >> 8;

        for (int i = 0; i < pixelLen; i++)
        {
            pixels[i] = Rgbw.Blend(pixels[i],
                seg.ColorFromPalette((int)(shift + FastMath.QuadWave8((byte)((i + 1) * 16))), false, seg.PaletteSolidWrap, 255),
                blendSpeed);
            shift += 3;
        }

        int offset = 0;
        for (int i = 0; i < seg.Length; i++)
        {
            seg.SetPixelColor(i, pixels[offset++]);
            if (offset >= pixelLen) offset = 0;
        }
    }

    /// <summary>A calm effect, like moonlight on water.</summary>
    public static void Lake(Segment seg)
    {
        uint sp = (uint)(seg.Speed / 10);
        byte wave1 = Beat.Sin8(sp + 2, -64, 64);
        byte wave2 = Beat.Sin8(sp + 1, -64, 64);
        byte wave3 = Beat.Sin8(sp + 2, 0, 80);

        for (int i = 0; i < seg.Length; i++)
        {
            int index = FastMath.Cos8((byte)(i * 15 + wave1)) / 2 + FastMath.CubicWave8((byte)(i * 23 + wave2)) / 2;
            var lum = (byte)(index > wave3 ? index - wave3 : 0);
            seg.SetPixelColor(i, seg.ColorFromPalette(index, false, false, 0, lum));
        }
    }

    /// <summary>Interfering sine waves, the classic demoscene plasma.</summary>
    public static void Plasma(Segment seg)
    {
        // a touch of randomness in the beat rates keeps two segments from locking together
        if (seg.Call == 0) seg.Aux0 = Rng.Next8(0, 2);
        byte thisPhase = Beat.Sin8((uint)(6 + seg.Aux0), -64, 64);
        byte thatPhase = Beat.Sin8((uint)(7 + seg.Aux0), -64, 64);

        for (int i = 0; i < seg.Length; i++)
        {
            var colorIndex = (byte)(FastMath.CubicWave8((byte)(i * (2 + 3 * (seg.Speed >> 5)) + thisPhase)) / 2
                                  + FastMath.Cos8((byte)(i * (1 + 2 * (seg.Speed >> 5)) + thatPhase)) / 2);
            byte thisBright = FastMath.QSub8(colorIndex, Beat.Sin8(7, 0, (byte)(128 - (seg.Intensity >> 1))));
            seg.SetPixelColor(i, seg.ColorFromPalette(colorIndex, false, seg.PaletteSolidWrap, 0, thisBright));
        }
    }

    /// <summary>Palette gradients flowing in alternating directions, zone by zone.</summary>
    public static void Flow(Segment seg)
    {
        uint counter = 0;
        if (seg.Speed != 0)
        {
            counter = seg.Now * (uint)((seg.Speed >> 2) + 1);
            counter >>= 8;
        }

        int maxZones = seg.Length / 6; // a zone needs about six pixels to read as a gradient
        int zones = (seg.Intensity * maxZones) >> 8;
        if ((zones & 0x01) != 0) zones++; // zones must come in pairs
        if (zones < 2) zones = 2;
        int zoneLen = seg.Length / zones;
        if (zoneLen < 1) zoneLen = 1;
        int requiredZones = (seg.Length + zoneLen - 1) / zoneLen;
        zones = requiredZones + 2; // extra zones cover the ends after integer truncation
        int offset = (seg.Length - zones * zoneLen) / 2;

        for (int z = 0; z < zones; z++)
        {
            int pos = offset + z * zoneLen;
            for (int i = 0; i < zoneLen; i++)
            {
                int colorIndex = (int)(i * 255 / zoneLen - counter);
                int led = (z & 0x01) != 0 ? i : zoneLen - 1 - i;
                if (seg.Reverse) led = zoneLen - 1 - led;
                seg.SetPixelColor(pos + led, seg.ColorFromPalette(colorIndex, false, true, 255));
            }
        }
    }

    /// <summary>Dots swinging around the segment in a pendulum motion, like birds in flight.</summary>
    public static void ChunChun(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        seg.FadeOut(254); // just enough to leave a trail
        uint counter = seg.Now * (uint)(6 + (seg.Speed >> 4));
        int numBirds = 2 + (seg.Length >> 3);
        uint span = (uint)((seg.Intensity << 8) / numBirds);

        for (int i = 0; i < numBirds; i++)
        {
            counter -= span;
            uint position = (uint)(FastMath.Sin16((ushort)counter) + 0x8000);
            var bird = (int)(position * (uint)seg.Length >> 16);
            bird = FastMath.Clamp(bird, 0, seg.Length - 1);
            seg.SetPixelColor(bird, seg.ColorFromPalette(i * 255 / numBirds, false, false, 0));
        }
    }

    /// <summary>
    /// Soft drifting clouds of colour, built from two independent noise fields - one for density
    /// and one for hue.
    /// </summary>
    public static void ColorClouds(Segment seg)
    {
        if (seg.Call == 0)
        {
            seg.Aux0 = Rng.Next16();
            seg.Aux1 = Rng.Next16();
        }
        uint volX0 = seg.Aux0;
        uint hueX0 = seg.Aux1;
        var hueOffset0 = (byte)(volX0 + hueX0); // a third random value derived from the first two

        // "cozy" folds the hue so the palette ends get more of the range, which reads as calmer
        bool cozy = seg.Check3;

        uint volSpeed = (uint)(1 + seg.Speed);
        uint hueSpeed = (uint)(1 + seg.Intensity);
        uint volSqueeze = (uint)(8 + seg.Custom1);  // more squeeze means more, smaller clouds
        uint hueSqueeze = seg.Custom2;              // more squeeze means more colourful clouds
        int volCutoff = 12500 + seg.Custom3 * 900;  // larger gaps between clouds
        const int volSaturate = 52000;              // must stay above volCutoff

        uint now = seg.Now;
        uint volT = now * volSpeed / 8;
        uint hueT = now * hueSpeed / 8;
        var hueOffset = (byte)(Beat.Beat88(64) >> 8);

        for (int i = 0; i < seg.Length; i++)
        {
            uint volX = (uint)i * volSqueeze * 64;
            int vol = Perlin.Noise16(volX0 + volX, volT);
            vol = FastMath.Clamp(FastMath.Map(vol, volCutoff, volSaturate, 0, 255), 0, 255);

            uint hueX = (uint)i * hueSqueeze * 8;
            var hue = (byte)(Perlin.Noise16(hueX0 + hueX, hueT) >> 7);
            hue += hueOffset0;
            hue += hueOffset;
            if (cozy) hue = FastMath.Cos8((byte)(128 + hue / 2));

            Rgbw pixel = seg.Palette != 0
                ? seg.ColorFromPalette(hue, false, true, 0, (byte)vol)
                : new Chsv32(hue << 8, 255, (byte)vol).ToRgb();

            // very dark pixels flicker between plain red, green and blue, so drop them entirely
            if (pixel.R + pixel.G + pixel.B <= 2) pixel = Rgbw.Black;

            seg.SetPixelColor(i, pixel);
        }
    }
}
