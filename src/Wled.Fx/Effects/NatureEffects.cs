namespace Wled.Fx;

/// <summary>
/// Effects that imitate something physical: fire, candles, lightning, sunrise, ocean and aurora.
/// Port of the corresponding blocks of <c>FX.cpp</c>.
/// </summary>
public static class NatureEffects
{
    internal static void Register()
    {
        EffectRegistry.Register(EffectId.Fire2012, "Fire 2012@Cooling,Spark rate,,2D Blur,Boost;;!;1;pal=35,sx=64,ix=160,m12=1,c2=128", Fire2012);
        EffectRegistry.Register(EffectId.Candle, "Candle@!,!;!,!;!;01;sx=96,ix=224,pal=0", Candle);
        EffectRegistry.Register(EffectId.CandleMulti, "Candle Multi@!,!;!,!;!;;sx=96,ix=224,pal=0", CandleMulti);
        EffectRegistry.Register(EffectId.Lightning, "Lightning@!,!,,,,,Overlay;!,!;!", Lightning);
        EffectRegistry.Register(EffectId.Sunrise, "Sunrise@Time [min],Width;;!;;pal=35,sx=60", Sunrise);
        EffectRegistry.Register(EffectId.Pacifica, "Pacifica@!,Angle;;!;;pal=51", Pacifica);
        EffectRegistry.Register(EffectId.Aurora, "Aurora@!,!;1,2,3;!;;sx=24,pal=50", Aurora);
        EffectRegistry.Register(EffectId.Tetrix, "Tetrix@!,Width,,,,One color;!,!;!;;sx=0,ix=0,pal=11,m12=1", Tetrix);
        EffectRegistry.Register(EffectId.HalloweenEyes, "Halloween Eyes@Eye off time,Eye on time,,,,,Overlay;!,!;!;12", HalloweenEyes);
    }

    /// <summary>
    /// A one-dimensional fire simulation: cells cool, heat drifts upward and diffuses, and sparks
    /// ignite near the bottom.
    /// </summary>
    /// <remarks>
    /// On a matrix in bar mode each column burns independently, and the 2D blur slider smears the
    /// flames sideways.
    /// </remarks>
    public static void Fire2012(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        int strips = seg.VerticalStripCount;
        byte[] heat = seg.GetData<byte>(strips * seg.Length);
        uint it = seg.Now >> 5;

        for (int stripNr = 0; stripNr < strips; stripNr++)
            Fire2012Strip(seg, stripNr, heat, stripNr * seg.Length, it);

        if (seg.Is2D)
        {
            var blurAmount = (byte)(seg.Custom2 >> 2);
            if (blurAmount > 48) blurAmount += (byte)(blurAmount - 48); // extra smear at the top of the slider
            if (blurAmount < 16) seg.BlurCols((byte)(seg.Custom2 >> 1)); // no side burn below a quarter
            else seg.Blur(blurAmount);
        }

        if (it != seg.Step) seg.Step = it;
    }

    private static void Fire2012Strip(Segment seg, int stripNr, byte[] heat, int offset, uint it)
    {
        int ignition = System.Math.Max(3, seg.Length / 10); // the bottom 10% never goes fully dark

        for (int i = 0; i < seg.Length; i++)
        {
            byte cool = it != seg.Step
                ? Rng.Next8((uint)((20 + seg.Speed / 3) * 16 / seg.Length + 2))
                : Rng.Next8(4);
            var minTemp = (byte)(i < ignition ? (ignition - i) / 4 + 16 : 0);
            byte temp = FastMath.QSub8(heat[offset + i], cool);
            heat[offset + i] = temp < minTemp ? minTemp : temp;
        }

        if (it != seg.Step)
        {
            for (int k = seg.Length - 1; k > 1; k--)
                heat[offset + k] = (byte)((heat[offset + k - 1] + (heat[offset + k - 2] << 1)) / 3);

            if (Rng.Next8() <= seg.Intensity)
            {
                // clamped to the strip: on a very short segment the ignition area is the whole of it
                byte y = Rng.Next8((uint)System.Math.Min(ignition, seg.Length));
                int boost = (17 + seg.Custom3) * (ignition - y / 2) / ignition;
                heat[offset + y] = FastMath.QAdd8(heat[offset + y], Rng.Next8((uint)(96 + 2 * boost), (uint)(207 + boost)));
            }
        }

        for (int j = 0; j < seg.Length; j++)
        {
            seg.SetPixelColor(Segment.IndexToVStrip(j, stripNr), ColorUtil.ColorFromPalette(
                seg.CurrentPalette, System.Math.Min(heat[offset + j], (byte)240), 255, BlendType.NoBlend));
        }
    }

    /// <summary>
    /// Candle flicker: the brightness walks towards a fresh random target, and the step size is set
    /// so it always arrives in roughly the same number of frames.
    /// </summary>
    /// <param name="seg">The segment to render into.</param>
    /// <param name="multi">Give every pixel its own flame instead of flickering the segment as one.</param>
    private static void CandleBase(Segment seg, bool multi)
    {
        // state layout: the shared flame lives in aux0/aux1/step, per-pixel flames in the buffer
        int perPixel = multi && seg.Length > 1 ? System.Math.Max(1, seg.Length - 1) * 3 : 0;
        byte[] candleData = seg.GetData<byte>(perPixel + 4);

        uint lastCall = BitConverter.ToUInt32(candleData, perPixel);
        if (seg.Now - lastCall < 1000 / LedStrip.DefaultFps) return; // hold a steady rate
        BitConverter.TryWriteBytes(candleData.AsSpan(perPixel), seg.Now);

        int valRange = seg.Intensity;      // how far the flame may swing
        int rndVal = valRange >> 1;        // at most 127

        int speedFactor = 4;               // how much closer to the target each frame gets
        if (seg.Speed > 252) speedFactor = 1;      // epilepsy
        else if (seg.Speed > 99) speedFactor = 2;  // a regular candle
        else if (seg.Speed > 49) speedFactor = 3;  // a slower fade

        int numCandles = multi ? seg.Length : 1;

        for (int i = 0; i < numCandles; i++)
        {
            int d = 0;
            int s = seg.Aux0, target = seg.Aux1, fadeStep = (int)seg.Step;
            if (i > 0)
            {
                d = (i - 1) * 3;
                if (d + 2 >= perPixel) continue;
                s = candleData[d];
                target = candleData[d + 1];
                fadeStep = candleData[d + 2];
            }
            if (fadeStep == 0) // first frame for this flame
            {
                s = 128;
                target = 130 + Rng.Next8(4);
                fadeStep = 1;
            }

            bool newTarget;
            if (target > s)
            {
                s = FastMath.QAdd8((byte)s, (byte)fadeStep);
                newTarget = s >= target;
            }
            else
            {
                s = FastMath.QSub8((byte)s, (byte)fadeStep);
                newTarget = s <= target;
            }

            if (newTarget)
            {
                // two random draws give a bell-ish distribution, so extremes are rarer
                target = Rng.Next8((uint)rndVal) + Rng.Next8((uint)rndVal);
                if (target < rndVal >> 1) target = (rndVal >> 1) + Rng.Next8((uint)rndVal);
                target += 255 - valRange;

                int diff = target > s ? target - s : s - target;
                fadeStep = diff >> speedFactor;
                if (fadeStep == 0) fadeStep = 1;
            }

            if (i > 0)
            {
                seg.SetPixelColor(i, Rgbw.Blend(seg.Color(1),
                    seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0), (byte)s));
                candleData[d] = (byte)s;
                candleData[d + 1] = (byte)target;
                candleData[d + 2] = (byte)fadeStep;
            }
            else
            {
                for (int j = 0; j < seg.Length; j++)
                    seg.SetPixelColor(j, Rgbw.Blend(seg.Color(1),
                        seg.ColorFromPalette(j, true, seg.PaletteSolidWrap, 0), (byte)s));

                seg.Aux0 = (ushort)s;
                seg.Aux1 = (ushort)target;
                seg.Step = (uint)fadeStep;
            }
        }
    }

    /// <summary>The whole segment flickering as one candle flame.</summary>
    public static void Candle(Segment seg) => CandleBase(seg, false);

    /// <summary>Every pixel flickering as its own candle flame.</summary>
    public static void CandleMulti(Segment seg) => CandleBase(seg, true);

    /// <summary>
    /// Lightning: a dim leader flash, then a burst of brighter strokes, then a long dark pause.
    /// </summary>
    public static void Lightning(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        int ledStart = Rng.Next16((uint)seg.Length);
        int ledLen = 1 + Rng.Next16((uint)(seg.Length - ledStart));
        var bri = (byte)(255 / Rng.Next8(1, 3));

        if (seg.Aux1 == 0) // the leader stroke
        {
            seg.Aux1 = (ushort)(Rng.Next8(4, (uint)(4 + seg.Intensity / 20)) * 2);
            bri = 52;        // the leader is dimmer than the strokes that follow
            seg.Aux0 = 200;  // pause after the leader
        }

        if (!seg.Check2) seg.Fill(seg.Color(1));

        if (seg.Aux1 > 3 && (seg.Aux1 & 0x01) == 0) // flash on even counts above two
        {
            for (int i = ledStart; i < ledStart + ledLen; i++)
                seg.SetPixelColor(i, seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0, bri));
            seg.Aux1--;
            seg.Step = seg.Now;
        }
        else if (seg.Now - seg.Step > seg.Aux0)
        {
            seg.Aux1--;
            if (seg.Aux1 < 2) seg.Aux1 = 0;

            seg.Aux0 = (ushort)(50 + Rng.Next8(100)); // between strokes
            if (seg.Aux1 == 2) seg.Aux0 = (ushort)(Rng.Next8((uint)(255 - seg.Speed)) * 100); // between strikes
            seg.Step = seg.Now;
        }
    }

    /// <summary>
    /// A gradual sunrise or sunset.
    /// </summary>
    /// <remarks>
    /// Speed 0 holds a static sun; 1-60 is a sunrise over that many minutes, 61-120 a sunset, and
    /// anything above breathes in and out continuously.
    /// </remarks>
    public static void Sunrise(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        if (seg.Call == 0 || seg.Speed != seg.Aux0)
        {
            seg.Step = Clock.Millis; // wall clock, since the effect time base can be re-synced
            seg.Aux0 = seg.Speed;
        }

        seg.Fill(Rgbw.Black);
        int stage = 0xFFFF;
        uint tenthsSinceStart = (Clock.Millis - seg.Step) / 100;

        if (seg.Speed > 120)
        {
            var counter = (ushort)((seg.Now >> 1) * (uint)(((seg.Speed - 120) >> 1) + 1));
            stage = FastMath.TriWave16(counter);
        }
        else if (seg.Speed != 0)
        {
            int durationMinutes = seg.Speed;
            if (durationMinutes > 60) durationMinutes -= 60;
            uint target = (uint)durationMinutes * 600;
            if (tenthsSinceStart > target) tenthsSinceStart = target;
            stage = FastMath.Map((int)tenthsSinceStart, 0, (int)System.Math.Max(target, 1), 0, 0xFFFF);
            if (seg.Speed > 60) stage = 0xFFFF - stage; // sunset
        }

        for (int i = 0; i <= seg.Length / 2; i++)
        {
            int wave = FastMath.TriWave16((ushort)(i * stage / seg.Length));
            wave = (wave >> 8) + ((wave * seg.Intensity) >> 15);
            Rgbw c = seg.ColorFromPalette(System.Math.Min(wave, 240), false, true, 255);
            seg.SetPixelColor(i, c);
            seg.SetPixelColor(seg.Length - i - 1, c);
        }
    }

    private static readonly Palette16 PacificaPalette1 = BuildPalette(
        0x002229, 0x001E2F, 0x001934, 0x001938, 0x00143F, 0x001443, 0x00B047, 0x00B04C,
        0x00004F, 0x000054, 0x000062, 0x00006F, 0x00007A, 0x000085, 0x47938A, 0x64D08E);

    private static readonly Palette16 PacificaPalette2 = BuildPalette(
        0x002229, 0x001E2F, 0x001934, 0x001938, 0x00143F, 0x001443, 0x00B047, 0x00B04C,
        0x00004F, 0x000054, 0x000062, 0x00006F, 0x00007A, 0x000085, 0x369B90, 0x4FDC9B);

    private static readonly Palette16 PacificaPalette3 = BuildPalette(
        0x00142C, 0x00193B, 0x002247, 0x002551, 0x002C5A, 0x002F63, 0x00346B, 0x003671,
        0x003B78, 0x003F7F, 0x00478E, 0x004D9C, 0x0054A9, 0x005AB4, 0x3F7FDC, 0x5A9CFF);

    private static Palette16 BuildPalette(params uint[] codes)
    {
        var entries = new Crgb[Palette16.Size];
        for (int i = 0; i < Palette16.Size; i++) entries[i] = new Crgb(codes[i]);
        return new Palette16(entries);
    }

    /// <summary>Renders one of the four wave layers Pacifica is built from.</summary>
    private static Crgb PacificaOneLayer(Segment seg, int i, Palette16 p, ushort ciStart,
                                         ushort waveScale, byte bri, ushort iOff)
    {
        uint ci = ciStart;
        uint waveAngle = iOff;
        uint waveScaleHalf = (uint)((waveScale >> 1) + 20);

        waveAngle += (uint)((120 + seg.Intensity) * i);
        var s16 = (uint)(FastMath.Sin16((ushort)waveAngle) + 32768);
        uint cs = FastMath.Scale16((ushort)s16, (ushort)waveScaleHalf) + waveScaleHalf;
        ci += cs * (uint)i;
        var sIndex16 = (uint)(FastMath.Sin16((ushort)ci) + 32768);
        ushort sIndex8 = FastMath.Scale16((ushort)sIndex16, 240);
        return (Crgb)ColorUtil.ColorFromPalette(p, sIndex8, bri, BlendType.LinearBlend);
    }

    /// <summary>
    /// Gentle blue-green ocean waves: four independently moving layers added together, with
    /// whitecaps where they happen to line up.
    /// </summary>
    public static void Pacifica(Segment seg)
    {
        Palette16 p1 = PacificaPalette1, p2 = PacificaPalette2, p3 = PacificaPalette3;
        if (seg.Palette != 0)
        {
            p1 = p2 = p3 = seg.CurrentPalette;
        }

        uint ciStart1 = seg.Aux0, ciStart2 = seg.Aux1;
        uint ciStart3 = seg.Step & 0xFFFF, ciStart4 = seg.Step >> 16;
        uint deltaMs = (uint)((seg.FrameTime >> 2) + ((seg.FrameTime * seg.Speed) >> 7));

        // the beat helpers read the clock, so speed it up for the duration of this frame
        uint clockWas = Clock.Millis;
        Clock.Freeze((clockWas >> 2) + ((clockWas * seg.Speed) >> 7));
        try
        {
            ushort speedFactor1 = Beat.Sin16(3, 179, 269);
            ushort speedFactor2 = Beat.Sin16(4, 179, 269);
            uint deltaMs1 = deltaMs * speedFactor1 / 256;
            uint deltaMs2 = deltaMs * speedFactor2 / 256;
            uint deltaMs21 = (deltaMs1 + deltaMs2) / 2;
            ciStart1 += deltaMs1 * Beat.Sin88(1011, 10, 13);
            ciStart2 -= deltaMs21 * Beat.Sin88(777, 8, 11);
            ciStart3 -= deltaMs1 * Beat.Sin88(501, 5, 7);
            ciStart4 -= deltaMs2 * Beat.Sin88(257, 4, 6);
            seg.Aux0 = (ushort)ciStart1;
            seg.Aux1 = (ushort)ciStart2;
            seg.Step = ((ciStart4 & 0xFFFF) << 16) | (ciStart3 & 0xFFFF);

            byte baseThreshold = Beat.Sin8(9, 55, 65);
            byte wave = Beat.Beat8(7);

            for (int i = 0; i < seg.Length; i++)
            {
                var c = new Crgb(2, 6, 10);
                c += PacificaOneLayer(seg, i, p1, (ushort)ciStart1, Beat.Sin16(3, 11 * 256, 14 * 256), Beat.Sin8(10, 70, 130), (ushort)(0 - Beat.Beat16(301)));
                c += PacificaOneLayer(seg, i, p2, (ushort)ciStart2, Beat.Sin16(4, 6 * 256, 9 * 256), Beat.Sin8(17, 40, 80), Beat.Beat16(401));
                c += PacificaOneLayer(seg, i, p3, (ushort)ciStart3, 6 * 256, Beat.Sin8(9, 10, 38), (ushort)(0 - Beat.Beat16(503)));
                c += PacificaOneLayer(seg, i, p3, (ushort)ciStart4, 5 * 256, Beat.Sin8(8, 10, 28), Beat.Beat16(601));

                // whitecaps where the four layers happen to add up brightly
                int threshold = FastMath.Scale8(FastMath.Sin8(wave), 20) + baseThreshold;
                wave += 7;
                int l = c.AverageLight;
                if (l > threshold)
                {
                    var overage = (byte)(l - threshold);
                    byte overage2 = FastMath.QAdd8(overage, overage);
                    c += new Crgb(overage, overage2, FastMath.QAdd8(overage2, overage2));
                }

                seg.SetPixelColor(i, c);
            }
        }
        finally
        {
            Clock.Freeze(null);
        }
    }

    private const int AuroraMaxWaves = 20;
    private const int AuroraMaxSpeed = 6;
    private const int AuroraWidthFactor = 6;
    private const int AuroraShift = 16;
    private const int AuroraScale = 1 << AuroraShift;

    /// <summary>One aurora wave: a soft band of colour drifting along the segment.</summary>
    private struct AuroraWave
    {
        public int Center;         // scaled by AuroraScale
        public uint AgeFactor;     // scaled by AuroraScale
        public ushort Ttl;
        public ushort Age;
        public ushort Width;
        public ushort BaseAlpha;   // scaled by AuroraScale
        public ushort SpeedFactor; // scaled by AuroraScale
        public short WaveStart;
        public short WaveEnd;
        public bool GoingLeft;
        public bool Alive;
        public Rgbw BaseColor;

        public void Init(int segmentLength, Rgbw color)
        {
            Ttl = (ushort)Rng.Next16(500, 1501);
            BaseColor = color;
            BaseAlpha = (ushort)(Rng.Next8(60, 100) * AuroraScale / 100); // never quite 100%, to avoid overflow
            Age = 0;
            int minWidth = segmentLength / 20;
            int maxWidth = System.Math.Max(segmentLength / AuroraWidthFactor, minWidth + 1);
            Width = (ushort)(Rng.Next16(minWidth, maxWidth) + 1);
            Center = (int)(((uint)Rng.Next8(101) << AuroraShift) / 100 * (uint)segmentLength);
            GoingLeft = (Rng.Next8() & 0x01) != 0;
            SpeedFactor = (ushort)(((uint)Rng.Next8(10, 31) * AuroraMaxSpeed << AuroraShift) / (100 * 255));
            Alive = true;
        }

        public void UpdateCachedValues()
        {
            uint halfTtl = (uint)(Ttl >> 1);
            if (halfTtl == 0) halfTtl = 1;
            AgeFactor = Age < halfTtl
                ? ((uint)Age << AuroraShift) / halfTtl
                : ((uint)(Ttl - Age) << AuroraShift) / halfTtl;
            if (AgeFactor >= AuroraScale) AgeFactor = AuroraScale - 1;

            uint centerLed = (uint)Center >> AuroraShift;
            WaveStart = (short)(centerLed - Width);
            WaveEnd = (short)(centerLed + Width);
        }

        public readonly Rgbw ColorForLed(int ledIndex)
        {
            // brightness falls off linearly from the centre of the wave to its edge
            if (ledIndex < WaveStart || ledIndex > WaveEnd) return Rgbw.Black;
            int offset = System.Math.Abs((ledIndex << AuroraShift) - Center);
            uint offsetFactor = (uint)(offset / System.Math.Max((int)Width, 1));
            if (offsetFactor > AuroraScale) return Rgbw.Black;

            uint brightness = AuroraScale - offsetFactor;
            brightness = (brightness * AgeFactor) >> AuroraShift;
            brightness = (brightness * BaseAlpha) >> AuroraShift;

            return new Rgbw(
                (int)((BaseColor.R * brightness) >> AuroraShift),
                (int)((BaseColor.G * brightness) >> AuroraShift),
                (int)((BaseColor.B * brightness) >> AuroraShift),
                (int)((BaseColor.W * brightness) >> AuroraShift));
        }

        public void Update(int segmentLength, int speed)
        {
            int step = SpeedFactor * speed;
            Center += GoingLeft ? -step : step;
            Age++;

            if (Age > Ttl) { Alive = false; return; }

            int widthScaled = Width << AuroraShift;
            long lengthScaled = (long)segmentLength << AuroraShift;
            if (GoingLeft) { if (Center < -widthScaled) Alive = false; }
            else if (Center > lengthScaled + widthScaled) Alive = false;
        }
    }

    /// <summary>Softly overlapping waves of colour drifting along the segment.</summary>
    public static void Aurora(Segment seg)
    {
        seg.Aux1 = (ushort)FastMath.Map(seg.Intensity, 0, 255, 2, AuroraMaxWaves);
        AuroraWave[] waves = seg.GetData<AuroraWave>(seg.Aux1);

        for (int i = 0; i < seg.Aux1; i++)
        {
            waves[i].Update(seg.Length, seg.Speed);
            // a fresh buffer starts every wave dead, so they all get initialised on the first frame
            if (!waves[i].Alive) waves[i].Init(seg.Length, seg.ColorFromPalette(Rng.Next8(), false, false, Rng.Next8(0, 3)));
            waves[i].UpdateCachedValues();
        }

        int backlight = 0;
        if (!seg.Color(0).IsBlack) backlight++;
        if (!seg.Color(1).IsBlack) backlight++;
        if (!seg.Color(2).IsBlack) backlight++;
        // inverse gamma keeps the faint backlight visible once gamma correction is applied
        backlight = Gamma.RawInverse8((byte)backlight);

        for (int i = 0; i < seg.Length; i++)
        {
            Rgbw mixed = new(backlight, backlight, backlight);
            for (int j = 0; j < seg.Aux1; j++) mixed = mixed.Add(waves[j].ColorForLed(i));
            seg.SetPixelColor(i, mixed);
        }
    }

    /// <summary>One falling brick.</summary>
    private struct Brick
    {
        public float Position;
        public float Speed;
        public byte Color;
        public int Size;
        public int Stack;
        public uint State; // 0 init, 1 forming, 2 falling, anything larger is a fade-out timestamp
    }

    /// <summary>Bricks falling one at a time and stacking up, then fading away once full.</summary>
    public static void Tetrix(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        int strips = seg.VerticalStripCount;
        Brick[] drops = seg.GetData<Brick>(strips);

        for (int stripNr = 0; stripNr < strips; stripNr++) TetrixStrip(seg, stripNr, drops);
    }

    private static void TetrixStrip(Segment seg, int stripNr, Brick[] drops)
    {
        ref Brick drop = ref drops[stripNr];

        if (seg.Call == 0)
        {
            drop.Stack = 0;
            drop.State = seg.Now + 2000; // start by fading the segment out
            if (seg.Check1) drop.Color = 0;
        }

        if (drop.State == 0) // start a new brick
        {
            // a brick should cross the whole segment in 5s at speed 1 and 0.25s at speed 255
            int speed = seg.Speed != 0 ? seg.Speed : Rng.Next8(1, 255);
            speed = FastMath.Map(speed, 1, 255, 5000, 250);
            drop.Speed = seg.Length * (float)seg.FrameTime / speed;
            drop.Position = seg.Length;
            if (!seg.Check1) drop.Color = (byte)(Rng.Next8(0, 15) << 4); // spaced out so hues differ
            drop.State = 1;
            drop.Size = (seg.Intensity != 0 ? (seg.Intensity >> 5) + 1 : Rng.Next8(1, 5)) * (1 + (seg.Length >> 6));
        }

        if (drop.State == 1 && Rng.Next8() >> 6 != 0) drop.State = 2; // let go at a random moment

        if (drop.State == 2)
        {
            if (drop.Position > drop.Stack)
            {
                drop.Position -= drop.Speed;
                if ((int)drop.Position < drop.Stack) drop.Position = drop.Stack;
                for (int i = (int)drop.Position; i < seg.Length; i++)
                {
                    Rgbw col = i < (int)drop.Position + drop.Size
                        ? seg.ColorFromPalette(drop.Color, false, false, 0)
                        : seg.Color(1);
                    seg.SetPixelColor(Segment.IndexToVStrip(i, stripNr), col);
                }
            }
            else // landed
            {
                drop.State = 0;
                drop.Stack += drop.Size;
                if (drop.Stack >= seg.Length) drop.State = seg.Now + 2000; // full, so fade it out
            }
        }

        if (drop.State > 2)
        {
            drop.Size = 0;
            if (drop.State > seg.Now)
            {
                for (int i = 0; i < seg.Length; i++)
                    seg.BlendPixelColor(Segment.IndexToVStrip(i, stripNr), seg.Color(1), 25);
            }
            else
            {
                drop.Stack = 0;
                drop.State = 0;
                if (seg.Check1) drop.Color += 8; // walk the palette index along
            }
        }
    }

    private enum EyeState : byte
    {
        InitializeOn = 0,
        On,
        Blink,
        InitializeOff,
        Off,
        Count,
    }

    private struct EyeData
    {
        public EyeState State;
        public byte Color;
        public ushort StartPos;
        public ushort Duration;
        public uint StartTime;
        public uint BlinkEndTime;
        public int Row;
    }

    /// <summary>A pair of eyes that fade in, blink, and vanish again.</summary>
    public static void HalloweenEyes(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        bool isMatrix = seg.Is2D;
        int maxWidth = isMatrix ? seg.Width : seg.Length;
        int eyeSpace = System.Math.Max(2, isMatrix ? seg.Width >> 4 : seg.Length >> 5);
        int eyeWidth = eyeSpace / 2;
        int eyeLength = 2 * eyeWidth + eyeSpace;
        if (eyeLength >= maxWidth) { BasicEffects.Static(seg); return; }

        EyeData[] state = seg.GetData<EyeData>(1);
        ref EyeData data = ref state[0];

        if (!seg.Check2) seg.Fill(seg.Color(1));

        if (data.State >= EyeState.Count) data.State = EyeState.InitializeOn;
        int duration = System.Math.Max(1, (int)data.Duration);
        uint elapsed = seg.Now - data.StartTime;

        switch (data.State)
        {
            case EyeState.InitializeOn:
                data.StartPos = (ushort)Rng.Next16(0, maxWidth - eyeLength - 1);
                data.Color = Rng.Next8();
                if (isMatrix) data.Row = Rng.Next16((uint)System.Math.Max(seg.Height - 1, 1));
                duration = 128 + Rng.Next16((uint)(seg.Intensity * 64));
                data.Duration = (ushort)duration;
                data.State = EyeState.On;
                goto case EyeState.On;

            case EyeState.On:
            {
                int start2ndEye = data.StartPos + eyeWidth + eyeSpace;
                // clamp in case the slider was turned down while this state was running
                duration = System.Math.Min(duration, 128 + seg.Intensity * 64);

                const uint minimumOnTimeBegin = 1024;
                const uint minimumOnTimeEnd = 1024;
                uint fadeInState = elapsed * (256u * 8u) / (uint)duration;
                Rgbw background = seg.Color(1);
                Rgbw eyeColor = seg.ColorFromPalette(data.Color, false, false, 0);
                Rgbw c = eyeColor;

                if (fadeInState < 256) c = Rgbw.Blend(background, eyeColor, (byte)fadeInState);
                else if (elapsed > minimumOnTimeBegin)
                {
                    uint remaining = elapsed >= duration ? 0 : (uint)duration - elapsed;
                    // never blink right at the start or the end of the on phase
                    if (remaining > minimumOnTimeEnd && Rng.Next8() < 4)
                    {
                        c = background;
                        data.State = EyeState.Blink;
                        data.BlinkEndTime = seg.Now + Rng.Next8(8, 128);
                    }
                }

                if (c != background)
                {
                    for (int i = 0; i < eyeWidth; i++)
                    {
                        if (isMatrix)
                        {
                            seg.SetPixelColorXY(data.StartPos + i, data.Row, c);
                            seg.SetPixelColorXY(start2ndEye + i, data.Row, c);
                        }
                        else
                        {
                            seg.SetPixelColor(data.StartPos + i, c);
                            seg.SetPixelColor(start2ndEye + i, c);
                        }
                    }
                }
                break;
            }

            case EyeState.Blink:
                if (seg.Now >= data.BlinkEndTime) data.State = EyeState.On;
                break;

            case EyeState.InitializeOff:
            {
                int eyeOffTimeBase = seg.Speed * 128;
                duration = eyeOffTimeBase + Rng.Next16((uint)eyeOffTimeBase);
                data.Duration = (ushort)duration;
                data.State = EyeState.Off;
                goto case EyeState.Off;
            }

            case EyeState.Off:
                duration = System.Math.Min(duration, 2 * seg.Speed * 128);
                break;
        }

        if (elapsed > duration)
        {
            data.State = data.State switch
            {
                EyeState.InitializeOn or EyeState.On or EyeState.Blink => EyeState.InitializeOff,
                _ => EyeState.InitializeOn,
            };
            data.StartTime = seg.Now;
        }
    }
}
