namespace Wled.Fx;

/// <summary>
/// Effects built on simulated motion: comets, meteors, ripples, oscillators and bouncing balls.
/// Port of the corresponding blocks of <c>FX.cpp</c>.
/// </summary>
public static class MotionEffects
{
    internal static void Register()
    {
        EffectRegistry.Register(EffectId.Icu, "ICU@!,!,,,,,Overlay;!,!;!", Icu);
        EffectRegistry.Register(EffectId.TricolorChase, "Chase 3@!,Size;1,2,3;!", TricolorChase);
        EffectRegistry.Register(EffectId.TricolorWipe, "Tri Wipe@!;1,2,3;!", TricolorWipe);
        EffectRegistry.Register(EffectId.TricolorFade, "Tri Fade@!;1,2,3;!", TricolorFade);
        EffectRegistry.Register(EffectId.MultiComet, "Multi Comet@!,Fade;!,!;!;1", MultiComet);
        EffectRegistry.Register(EffectId.RandomChase, "Stream 2@!;;", RandomChase);
        EffectRegistry.Register(EffectId.Oscillate, "Oscillate", Oscillate);
        EffectRegistry.Register(EffectId.Meteor, "Meteor@!,Trail,,,,Gradient,,Smooth;;!;1", Meteor);
        EffectRegistry.Register(EffectId.Railway, "Railway@!,Smoothness;1,2;!;;pal=3", Railway);
        EffectRegistry.Register(EffectId.Ripple, "Ripple@!,Wave #,Blur,,,,Overlay;,!;!;12;c1=0", Ripple);
        EffectRegistry.Register(EffectId.RippleRainbow, "Ripple Rainbow@!,Wave #;;!;12", RippleRainbow);
        EffectRegistry.Register(EffectId.Bouncingballs, "Bouncing Balls@Gravity,# of balls,,,,,Overlay;!,!,!;!;1;m12=1", BouncingBalls);
        EffectRegistry.Register(EffectId.Rollingballs, "Rolling Balls@!,# of balls,,,,Collide,Overlay,Trails;!,!,!;!;1;m12=1", RollingBalls);
        EffectRegistry.Register(EffectId.Sinelon, "Sinelon@!,Trail;!,!,!;!", Sinelon);
        EffectRegistry.Register(EffectId.SinelonDual, "Sinelon Dual@!,Trail;!,!,!;!", SinelonDual);
        EffectRegistry.Register(EffectId.SinelonRainbow, "Sinelon Rainbow@!,Trail;!,!,!;!", SinelonRainbow);
        EffectRegistry.Register(EffectId.Popcorn, "Popcorn@!,!,,,,,Overlay;!,!,!;!;;m12=1", Popcorn);
        EffectRegistry.Register(EffectId.Drip, "Drip@Gravity,# of drips,,,,,Overlay;!,!;!;;m12=1", Drip);
        EffectRegistry.Register(EffectId.Heartbeat, "Heartbeat@!,!;!,!;!;01;m12=1", Heartbeat);
        EffectRegistry.Register(EffectId.Percent, "Percent@,% of fill,,,,One color;!,!;!", Percent);
        EffectRegistry.Register(EffectId.WashingMachine, "Washing Machine@!,!;;!", WashingMachine);
    }

    /// <summary>A pair of eyes that pause, blink and dart to a new position.</summary>
    public static void Icu(Segment seg)
    {
        var now = (ushort)seg.Now;
        int dest = seg.Aux1;
        int space = (seg.Intensity >> 3) + 2;
        // the upper half of step holds the state machine, the lower half the next update time
        var state = (ushort)(seg.Step >> 16);
        var nextUpdate = (ushort)(seg.Step & 0xFFFF);

        var pIndex = (byte)FastMath.Map(dest, 0, System.Math.Max(seg.Length - seg.Length / space, 1), 0, 255);
        Rgbw col = seg.ColorFromPalette(pIndex, false, false, 0);
        seg.Fill(seg.Check2 ? Rgbw.Black : seg.Color(1));

        if (state != 1) // not blinking
        {
            seg.SetPixelColor(dest, col);
            seg.SetPixelColor(dest + seg.Length / space, col);
            if (state == 3) // moving: draw the next position too, so the motion reads as smooth
            {
                if (seg.Aux0 > seg.Aux1) dest++;
                else if (seg.Aux0 < seg.Aux1) dest--;
                seg.SetPixelColor(dest, col);
                seg.SetPixelColor(dest + seg.Length / space, col);
            }
        }

        if ((short)(now - nextUpdate) >= 0) // signed compare handles the 16-bit wrap
        {
            switch (state)
            {
                case 0: // end of the first pause: blink, or pause some more
                    state++;
                    if (Rng.Next8(6) == 0)
                    {
                        nextUpdate = (ushort)(now + 200);
                        break;
                    }
                    goto case 1;
                case 1: // done blinking
                    nextUpdate = (ushort)(now + 500 + Rng.Next16(1000));
                    state++;
                    break;
                case 2: // pause over, pick somewhere to look
                    seg.Aux0 = Rng.Next16((uint)System.Math.Max(seg.Length - seg.Length / space, 1));
                    nextUpdate = now;
                    state++;
                    break;
                default: // moving
                    seg.Aux1 = (ushort)dest;
                    nextUpdate = (ushort)(now + 5 + 50 * (255 - seg.Speed) / seg.Length);
                    if (seg.Aux0 == dest)
                    {
                        nextUpdate = (ushort)(now + 500 + Rng.Next16(1000));
                        state = 0;
                    }
                    break;
            }
        }

        seg.Step = ((uint)state << 16) | nextUpdate;
    }

    /// <summary>Three colours chasing each other in equal bands.</summary>
    public static void TricolorChase(Segment seg)
    {
        Rgbw color1 = seg.Color(2);
        Rgbw color2 = seg.Color(0);
        uint cycleTime = (uint)(50 + ((255 - seg.Speed) << 1));
        uint it = seg.Now / cycleTime;
        int width = 1 + (seg.Intensity >> 4); // 1-16 pixels per colour
        var index = (int)(it % (uint)(width * 3));

        for (int i = 0; i < seg.Length; i++, index++)
        {
            if (index > width * 3 - 1) index = 0;

            Rgbw color = color1;
            if (index > (width << 1) - 1) color = seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 1);
            else if (index > width - 1) color = color2;

            seg.SetPixelColor(seg.Length - i - 1, color);
        }
    }

    /// <summary>A wipe that cycles through all three segment colours.</summary>
    public static void TricolorWipe(Segment seg)
    {
        uint cycleTime = (uint)(1000 + (255 - seg.Speed) * 200);
        uint perc = seg.Now % cycleTime;
        uint prog = perc * 65535 / cycleTime;
        var ledIndex = (int)(prog * (uint)(seg.Length * 3) >> 16);

        for (int i = 0; i < seg.Length; i++)
            seg.SetPixelColor(i, seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 2));

        if (ledIndex < seg.Length) // wiping colour 0 over colour 1
        {
            for (int i = 0; i < seg.Length; i++)
                seg.SetPixelColor(i, i > ledIndex ? seg.Color(0) : seg.Color(1));
        }
        else if (ledIndex < seg.Length * 2) // wiping colour 1 over colour 2
        {
            int offset = ledIndex - seg.Length;
            for (int i = offset + 1; i < seg.Length; i++) seg.SetPixelColor(i, seg.Color(1));
        }
        else // wiping colour 2 over colour 0
        {
            int offset = ledIndex - seg.Length * 2;
            for (int i = 0; i <= offset; i++) seg.SetPixelColor(i, seg.Color(0));
        }
    }

    /// <summary>The whole segment fading around the three segment colours in turn.</summary>
    public static void TricolorFade(Segment seg)
    {
        var counter = (ushort)(seg.Now * (uint)((seg.Speed >> 3) + 1));
        uint prog = (uint)counter * 768 >> 16;

        Rgbw color1, color2;
        int stage;
        if (prog < 256) { color1 = seg.Color(0); color2 = seg.Color(1); stage = 0; }
        else if (prog < 512) { color1 = seg.Color(1); color2 = seg.Color(2); stage = 1; }
        else { color1 = seg.Color(2); color2 = seg.Color(0); stage = 2; }

        var stp = (byte)prog;
        for (int i = 0; i < seg.Length; i++)
        {
            Rgbw color = stage switch
            {
                2 => Rgbw.Blend(seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 2), color2, stp),
                1 => Rgbw.Blend(color1, seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 2), stp),
                _ => Rgbw.Blend(color1, color2, stp),
            };
            seg.SetPixelColor(i, color);
        }
    }

    private const int MaxComets = 8;

    /// <summary>Several comets launched at random and racing along the segment.</summary>
    public static void MultiComet(Segment seg)
    {
        uint cycleTime = (uint)(10 + (255 - seg.Speed));
        uint it = seg.Now / cycleTime;
        if (seg.Step == it) return;

        ushort[] comets = seg.GetData<ushort>(MaxComets);
        seg.FadeOut((byte)(seg.Intensity / 2 + 128));

        for (int i = 0; i < MaxComets; i++)
        {
            if (comets[i] < seg.Length)
            {
                int index = comets[i];
                seg.SetPixelColor(index, !seg.Color(2).IsBlack && i % 2 == 0
                    ? seg.Color(2)
                    : seg.ColorFromPalette(index, true, seg.PaletteSolidWrap, 0));
                comets[i]++;
            }
            else if (Rng.Next16((uint)seg.Length) == 0) comets[i] = 0; // launch a new one
        }

        seg.Step = it;
    }

    /// <summary>
    /// A stream of colours where each pixel usually inherits its neighbour channel values, so the
    /// colours drift rather than jump.
    /// </summary>
    public static void RandomChase(Segment seg)
    {
        if (seg.Call == 0)
        {
            var seed = new Prng();
            seg.Step = new Rgbw(seed.Next8(), seed.Next8(), seed.Next8()).Value;
            seg.Aux0 = seed.Next16();
        }
        // a fixed seed makes the whole stream reproducible from the two stored values
        var prng = new Prng(seg.Aux0);
        uint cycleTime = (uint)(25 + 3 * (255 - seg.Speed));
        uint it = seg.Now / cycleTime;
        Rgbw color = seg.Step;

        for (int i = seg.Length - 1; i >= 0; i--)
        {
            byte r = prng.Next8(6) != 0 ? color.R : prng.Next8();
            byte g = prng.Next8(6) != 0 ? color.G : prng.Next8();
            byte b = prng.Next8(6) != 0 ? color.B : prng.Next8();
            color = new Rgbw(r, g, b);
            seg.SetPixelColor(i, color);
            if (i == seg.Length - 1 && seg.Aux1 != (it & 0xFFFF)) // the next frame starts from here
            {
                seg.Step = color.Value;
                seg.Aux0 = prng.Seed;
            }
        }

        seg.Aux1 = (ushort)(it & 0xFFFF);
    }

    /// <summary>One oscillating bar of colour.</summary>
    private struct Oscillator
    {
        public int Position;
        public int Size;
        public int Direction;
        public byte Speed;
    }

    /// <summary>Three bars of colour bouncing along the segment and blending where they meet.</summary>
    public static void Oscillate(Segment seg)
    {
        const int count = 3;
        Oscillator[] oscillators = seg.GetData<Oscillator>(count);

        if (seg.Call == 0)
        {
            oscillators[0] = new Oscillator { Position = seg.Length / 4, Size = seg.Length / 8, Direction = 1, Speed = 1 };
            oscillators[1] = new Oscillator { Position = seg.Length / 4 * 3, Size = seg.Length / 8, Direction = 1, Speed = 2 };
            oscillators[2] = new Oscillator { Position = seg.Length / 4 * 2, Size = seg.Length / 8, Direction = -1, Speed = 1 };
        }

        uint cycleTime = (uint)(20 + 2 * (255 - seg.Speed));
        uint it = seg.Now / cycleTime;

        for (int i = 0; i < count; i++)
        {
            if (it != seg.Step) oscillators[i].Position += oscillators[i].Direction * oscillators[i].Speed;
            oscillators[i].Size = seg.Length / (3 + seg.Intensity / 8);
            if (oscillators[i].Direction == -1 && oscillators[i].Position < 0)
            {
                oscillators[i].Position = 0;
                oscillators[i].Direction = 1;
                // bigger steps at higher speeds, so the bars do not crawl
                oscillators[i].Speed = seg.Speed > 100 ? Rng.Next8(2, 4) : Rng.Next8(1, 3);
            }
            if (oscillators[i].Direction == 1 && oscillators[i].Position >= seg.Length - 1)
            {
                oscillators[i].Position = seg.Length - 1;
                oscillators[i].Direction = -1;
                oscillators[i].Speed = seg.Speed > 100 ? Rng.Next8(2, 4) : Rng.Next8(1, 3);
            }
        }

        for (int i = 0; i < seg.Length; i++)
        {
            Rgbw color = Rgbw.Black;
            for (int j = 0; j < count; j++)
            {
                if (i >= oscillators[j].Position - oscillators[j].Size && i <= oscillators[j].Position + oscillators[j].Size)
                    color = color.IsBlack ? seg.Color(j) : Rgbw.Blend(color, seg.Color(j), 128);
            }
            seg.SetPixelColor(i, color);
        }

        seg.Step = it;
    }

    /// <summary>A meteor racing along the segment leaving a decaying trail.</summary>
    public static void Meteor(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        bool smooth = seg.Check3;
        byte[] trail = seg.GetData<byte>(seg.Length);

        int meteorSize = 1 + seg.Length / 20; // 5% of the segment
        int meteorStart;
        if (smooth) meteorStart = FastMath.Map((int)(seg.Step >> 6 & 0xFF), 0, 255, 0, seg.Length - 1);
        else
        {
            uint counter = seg.Now * (uint)((seg.Speed >> 2) + 8);
            meteorStart = (int)(counter * (uint)seg.Length >> 16);
        }

        int max = seg.Palette == 5 || !seg.Check1 ? 240 : 255;

        for (int i = 0; i < seg.Length; i++)
        {
            if (Rng.Next8() > 255 - seg.Intensity) continue;
            Rgbw col;
            if (smooth)
            {
                if (trail[i] > 0)
                {
                    int change = trail[i] + 4 - Rng.Next8(24); // between -20 and +4 each frame
                    trail[i] = (byte)FastMath.Clamp(change, 0, max);
                }
                col = seg.Check1
                    ? seg.ColorFromPalette(i, true, false, 0, trail[i])
                    : seg.ColorFromPalette(trail[i], false, true, 255);
            }
            else
            {
                trail[i] = FastMath.Scale8(trail[i], (byte)(128 + Rng.Next8(127)));
                int index = trail[i];
                int slot = 255;
                int bri = seg.Palette is 35 or 36 ? 255 : trail[i]; // fire palettes stay at full brightness
                if (!seg.Check1)
                {
                    slot = 0;
                    index = FastMath.Map(i, 0, seg.Length, 0, max);
                    bri = trail[i];
                }
                col = seg.ColorFromPalette(index, false, false, (byte)slot, (byte)bri);
            }
            seg.SetPixelColor(i, col);
        }

        for (int j = 0; j < meteorSize; j++)
        {
            int index = (meteorStart + j) % seg.Length;
            if (smooth)
            {
                trail[index] = (byte)max;
                Rgbw col = seg.Check1
                    ? seg.ColorFromPalette(index, true, false, 0, trail[index])
                    : seg.ColorFromPalette(trail[index], false, true, 255);
                seg.SetPixelColor(index, col);
            }
            else
            {
                int slot = 255;
                trail[index] = (byte)max;
                int i = max;
                if (!seg.Check1)
                {
                    i = FastMath.Map(index, 0, seg.Length, 0, max);
                    slot = 0;
                }
                seg.SetPixelColor(index, seg.ColorFromPalette(i, false, false, (byte)slot, 255));
            }
        }

        seg.Step += (uint)(seg.Speed + 1);
    }

    /// <summary>Alternate pixels fading in and out in opposition, like a level crossing signal.</summary>
    public static void Railway(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        uint duration = (uint)((256 - seg.Speed) * 40);
        var rampDuration = (ushort)(duration * seg.Intensity >> 8);
        if (seg.Step > duration)
        {
            seg.Step = 0;
            seg.Aux0 = (ushort)(seg.Aux0 != 0 ? 0 : 1); // swap which side is lit
        }

        int pos = 255;
        if (rampDuration != 0)
        {
            var p0 = (int)(seg.Step * 255 / rampDuration);
            if (p0 < 255) pos = p0;
        }
        if (seg.Aux0 != 0) pos = 255 - pos;

        for (int i = 0; i < seg.Length; i += 2)
        {
            // always sample the palette here: the two colour slots would defeat the crossfade
            seg.SetPixelColor(i, seg.ColorFromPalette(255 - pos, false, false, 255));
            if (i < seg.Length - 1) seg.SetPixelColor(i + 1, seg.ColorFromPalette(pos, false, false, 255));
        }
        seg.Step += (uint)seg.FrameTime;
    }

    /// <summary>One expanding ripple.</summary>
    private struct RippleState
    {
        public byte State;
        public byte Color;
        public ushort Position;
    }

    private const int MaxRipples = 100;

    /// <summary>Expanding rings of light, spawned at random. Works in one and two dimensions.</summary>
    private static void RippleBase(Segment seg, byte blurAmount = 0)
    {
        int maxRipples = System.Math.Min(1 + (seg.Length >> 2), MaxRipples);
        RippleState[] ripples = seg.GetData<RippleState>(maxRipples);

        for (int i = 0; i < maxRipples; i++)
        {
            int state = ripples[i].State;
            if (state == 0)
            {
                // spawn rate is scaled down on a matrix, where a ripple covers far more pixels
                if (Rng.Next16(5100 + 10000) <= (uint)(seg.Intensity >> (seg.Is2D ? 3 : 0)))
                {
                    ripples[i].State = 1;
                    ripples[i].Position = seg.Is2D
                        ? (ushort)((Rng.Next8((uint)seg.Width) << 8) | Rng.Next8((uint)seg.Height))
                        : Rng.Next16((uint)seg.Length);
                    ripples[i].Color = Rng.Next8();
                }
                continue;
            }

            int decay = (seg.Speed >> 4) + 1; // faster propagation decays faster
            int origin = ripples[i].Position;
            Rgbw col = seg.ColorFromPalette(ripples[i].Color, false, false, 255);
            int propagation = (state / decay - 1) * (seg.Speed + 1);
            int propI = propagation >> 8;
            int propF = propagation & 0xFF;
            var amp = (byte)(state < 17 ? FastMath.TriWave8((byte)((state - 1) * 8)) : FastMath.Map(state, 17, 255, 255, 2));

            if (seg.Is2D)
            {
                propI /= 2;
                int cx = origin >> 8;
                int cy = origin & 0xFF;
                byte mag = FastMath.Scale8(FastMath.Sin8((byte)(propF >> 2)), amp);
                if (propI > 0)
                    seg.DrawCircle(cx, cy, propI, Rgbw.Blend(seg.GetPixelColorXY(cx + propI, cy), col, mag), true);
            }
            else
            {
                int left = origin - propI - 1;
                int right = origin + propI + 2;
                for (int v = 0; v < 4; v++)
                {
                    byte mag = FastMath.Scale8(FastMath.CubicWave8((byte)((propF >> 2) + v * 64)), amp);
                    seg.SetPixelColor(left + v, Rgbw.Blend(seg.GetPixelColor(left + v), col, mag));
                    seg.SetPixelColor(right - v, Rgbw.Blend(seg.GetPixelColor(right - v), col, mag));
                }
            }
            state += decay;
            ripples[i].State = (byte)(state > 254 ? 0 : state);
        }
        seg.Blur(blurAmount);
    }

    /// <summary>Ripples spreading over the segment.</summary>
    public static void Ripple(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }
        if (seg.Custom1 != 0 || seg.Check2) seg.FadeOut(250);
        else seg.Fill(seg.Color(1));
        RippleBase(seg, (byte)(seg.Custom1 >> 1));
    }

    /// <summary>Ripples over a slowly cycling rainbow background.</summary>
    public static void RippleRainbow(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }
        if (seg.Call == 0)
        {
            seg.Aux0 = Rng.Next8();
            seg.Aux1 = Rng.Next8();
        }
        // walk the background hue towards a new random target rather than jumping to it
        if (seg.Aux0 == seg.Aux1) seg.Aux1 = Rng.Next8();
        else if (seg.Aux1 > seg.Aux0) seg.Aux0++;
        else seg.Aux0--;

        seg.Fill(Rgbw.Blend(seg.ColorWheel((byte)seg.Aux0), Rgbw.Black, 235));
        RippleBase(seg);
    }

    /// <summary>One ball under gravity.</summary>
    private struct Ball
    {
        public uint LastBounceTime;
        public float ImpactVelocity;
        public float Height;
    }

    private const int MaxNumBalls = 16;

    /// <summary>
    /// Balls dropped under gravity, losing a little energy at every bounce.
    /// </summary>
    /// <remarks>
    /// On a matrix in bar mode each column runs its own set of balls; the column number is packed
    /// into the pixel index by <see cref="Segment.IndexToVStrip"/>.
    /// </remarks>
    public static void BouncingBalls(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        int strips = seg.VerticalStripCount;
        Ball[] balls = seg.GetData<Ball>(MaxNumBalls * strips);

        if (!seg.Check2) seg.Fill(seg.Color(2).IsBlack ? seg.Color(1) : Rgbw.Black);

        for (int stripNr = 0; stripNr < strips; stripNr++)
            BouncingBallsStrip(seg, stripNr, balls, stripNr * MaxNumBalls);
    }

    private static void BouncingBallsStrip(Segment seg, int stripNr, Ball[] balls, int offset)
    {
        int numBalls = seg.Intensity * (MaxNumBalls - 1) / 255 + 1; // at least one ball
        const float gravity = -9.81f;
        bool hasCol2 = !seg.Color(2).IsBlack;
        uint time = seg.Now;

        if (seg.Call == 0)
        {
            for (int i = 0; i < MaxNumBalls; i++) balls[offset + i].LastBounceTime = time;
        }

        for (int i = 0; i < numBalls; i++)
        {
            ref Ball ball = ref balls[offset + i];
            float timeSinceLastBounce = (time - ball.LastBounceTime) / (float)((255 - seg.Speed) / 64 + 1);
            float timeSec = timeSinceLastBounce / 1000.0f;
            ball.Height = (0.5f * gravity * timeSec + ball.ImpactVelocity) * timeSec;

            if (ball.Height <= 0.0f)
            {
                ball.Height = 0.0f;
                // damping varies per ball so a group of them does not stay in lockstep
                float damping = 0.9f - i / (float)(numBalls * numBalls);
                ball.ImpactVelocity *= damping;
                ball.LastBounceTime = time;

                if (ball.ImpactVelocity < 0.015f)
                    ball.ImpactVelocity = MathF.Sqrt(-2.0f * gravity) * Rng.Next8(5, 11) / 10.0f;
            }
            else if (ball.Height > 1.0f) continue; // out of bounds, do not draw

            Rgbw color = seg.Color(0);
            if (seg.Palette != 0) color = seg.ColorWheel((byte)(i * (256 / System.Math.Max(numBalls, 8))));
            else if (hasCol2) color = seg.Color(i % Segment.ColorCount);

            var pos = (int)MathF.Round(ball.Height * (seg.Length - 1));
            seg.SetPixelColor(Segment.IndexToVStrip(pos, stripNr), color);
        }
    }

    /// <summary>One rolling ball.</summary>
    private struct RollingBall
    {
        public uint LastBounceUpdate;
        public float Mass;
        public float Velocity;
        public float Height;
    }

    /// <summary>
    /// Balls rolling along the segment, bouncing off the ends and, optionally, off each other.
    /// </summary>
    /// <remarks>
    /// Collisions are solved by working out when the two balls actually met rather than at the
    /// frame boundary, which keeps the exchange of momentum stable at any frame rate.
    /// </remarks>
    public static void RollingBalls(Segment seg)
    {
        const int maxNumBalls = 16;
        RollingBall[] balls = seg.GetData<RollingBall>(maxNumBalls);

        int numBalls = seg.Intensity / 16 + 1;
        bool hasCol2 = !seg.Color(2).IsBlack;

        if (seg.Call == 0)
        {
            seg.Fill(hasCol2 ? Rgbw.Black : seg.Color(1));
            for (int i = 0; i < maxNumBalls; i++)
            {
                balls[i].LastBounceUpdate = seg.Now;
                balls[i].Velocity = 20.0f * Rng.Next16(1000, 10000) / 10000.0f; // 1 to 10
                if (Rng.Next8() < 128) balls[i].Velocity = -balls[i].Velocity;  // half start in reverse
                balls[i].Height = Rng.Next16(0, 10000) / 10000.0f;
                balls[i].Mass = Rng.Next16(1000, 10000) / 10000.0f;
            }
        }

        // the Aircoookie time-scaling factor, so the speed slider behaves like the other effects
        float cfac = (FastMath.Scale8(8, (byte)(255 - seg.Speed)) + 1) * 20000.0f;

        if (seg.Check3) seg.FadeOut(250); // optional trails
        else if (!seg.Check2) seg.Fill(hasCol2 ? Rgbw.Black : seg.Color(1));

        for (int i = 0; i < numBalls; i++)
        {
            float timeSinceLastUpdate = (seg.Now - balls[i].LastBounceUpdate) / cfac;
            float thisHeight = balls[i].Height + balls[i].Velocity * timeSinceLastUpdate;

            // a ball far off the track (after the ball count changed) is put back on it
            if (thisHeight < -0.5f || thisHeight > 1.5f)
            {
                thisHeight = balls[i].Height = Rng.Next16(0, 10000) / 10000.0f;
                balls[i].LastBounceUpdate = seg.Now;
            }

            if ((thisHeight <= 0.0f && balls[i].Velocity < 0.0f) || (thisHeight >= 1.0f && balls[i].Velocity > 0.0f))
            {
                balls[i].Velocity = -balls[i].Velocity;
                balls[i].LastBounceUpdate = seg.Now;
                balls[i].Height = thisHeight;
            }

            if (seg.Check1)
            {
                for (int j = i + 1; j < numBalls; j++)
                {
                    if (balls[j].Velocity == balls[i].Velocity) continue;
                    float tCollided = (cfac * (balls[i].Height - balls[j].Height)
                                       + balls[i].Velocity * (balls[j].LastBounceUpdate - balls[i].LastBounceUpdate))
                                      / (balls[j].Velocity - balls[i].Velocity);

                    // a 2ms floor stops a single meeting being counted as several bounces
                    if (tCollided <= 2.0f || tCollided >= seg.Now - balls[j].LastBounceUpdate) continue;

                    balls[i].Height += balls[i].Velocity * (tCollided + (balls[j].LastBounceUpdate - balls[i].LastBounceUpdate)) / cfac;
                    balls[j].Height = balls[i].Height;
                    balls[i].LastBounceUpdate = (uint)(tCollided + 0.5f) + balls[j].LastBounceUpdate;
                    balls[j].LastBounceUpdate = balls[i].LastBounceUpdate;
                    float vtmp = balls[i].Velocity;
                    balls[i].Velocity = ((balls[i].Mass - balls[j].Mass) * vtmp + 2.0f * balls[j].Mass * balls[j].Velocity) / (balls[i].Mass + balls[j].Mass);
                    balls[j].Velocity = ((balls[j].Mass - balls[i].Mass) * balls[j].Velocity + 2.0f * balls[i].Mass * vtmp) / (balls[i].Mass + balls[j].Mass);
                    thisHeight = balls[i].Height + balls[i].Velocity * (seg.Now - balls[i].LastBounceUpdate) / cfac;
                }
            }

            Rgbw color = seg.Color(0);
            if (seg.Palette != 0) color = seg.ColorFromPalette(i * 255 / numBalls, false, seg.PaletteSolidWrap, 0);
            else if (hasCol2) color = seg.Color(i % Segment.ColorCount);

            thisHeight = System.Math.Clamp(thisHeight, 0.0f, 1.0f);
            var pos = (int)MathF.Round(thisHeight * (seg.Length - 1));
            seg.SetPixelColor(pos, color);
            balls[i].LastBounceUpdate = seg.Now;
            balls[i].Height = thisHeight;
        }
    }

    /// <summary>A dot swinging back and forth along a sine, leaving a trail.</summary>
    private static void SinelonBase(Segment seg, bool dual, bool rainbow = false)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        seg.FadeOut(seg.Intensity);
        int pos = Beat.Sin16((uint)(seg.Speed / 10), 0, (ushort)(seg.Length - 1));
        if (seg.Call == 0) seg.Aux0 = (ushort)pos;

        Rgbw color1 = seg.ColorFromPalette(pos, true, false, 0);
        Rgbw color2 = seg.Color(2);
        if (rainbow) color1 = seg.ColorWheel((byte)((pos & 0x07) * 32));

        seg.SetPixelColor(pos, color1);
        if (dual)
        {
            if (color2.IsBlack) color2 = seg.ColorFromPalette(pos, true, false, 0);
            if (rainbow) color2 = color1;
            seg.SetPixelColor(seg.Length - 1 - pos, color2);
        }

        // paint over the pixels skipped since the last frame so a fast dot stays continuous
        if (seg.Aux0 != pos)
        {
            int step = seg.Aux0 < pos ? 1 : -1;
            for (int i = seg.Aux0; i != pos; i += step)
            {
                seg.SetPixelColor(i, color1);
                if (dual) seg.SetPixelColor(seg.Length - 1 - i, color2);
            }
            seg.Aux0 = (ushort)pos;
        }
    }

    /// <summary>A dot swinging along the segment with a fading trail.</summary>
    public static void Sinelon(Segment seg) => SinelonBase(seg, false);

    /// <summary>Two dots swinging in opposite directions.</summary>
    public static void SinelonDual(Segment seg) => SinelonBase(seg, true);

    /// <summary>A swinging dot that cycles through the rainbow.</summary>
    public static void SinelonRainbow(Segment seg) => SinelonBase(seg, false, true);

    /// <summary>
    /// A particle used by the popcorn, firework and drip effects: a position, a velocity and a
    /// colour, with the second field doubling as a state counter where an effect needs one.
    /// </summary>
    internal struct Spark
    {
        public float Position;
        public float PositionX;
        public float Velocity;
        public float VelocityX;
        public ushort Color;
        public byte ColorIndex;
    }

    private const int MaxNumPopcorn = 21;

    /// <summary>Kernels popping up from the bottom of the segment and falling back down.</summary>
    public static void Popcorn(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        int strips = seg.VerticalStripCount;
        Spark[] popcorn = seg.GetData<Spark>(MaxNumPopcorn * strips);

        bool hasCol2 = !seg.Color(2).IsBlack;
        if (!seg.Check2) seg.Fill(hasCol2 ? Rgbw.Black : seg.Color(1));

        for (int stripNr = 0; stripNr < strips; stripNr++)
            PopcornStrip(seg, stripNr, popcorn, stripNr * MaxNumPopcorn);
    }

    private static void PopcornStrip(Segment seg, int stripNr, Spark[] popcorn, int offset)
    {
        float gravity = -0.0001f - (seg.Speed / 200000.0f);
        gravity *= seg.Length;

        int numPopcorn = seg.Intensity * MaxNumPopcorn / 255;
        if (numPopcorn == 0) numPopcorn = 1;

        for (int i = 0; i < numPopcorn; i++)
        {
            ref Spark kernel = ref popcorn[offset + i];
            if (kernel.Position >= 0.0f)
            {
                kernel.Position += kernel.Velocity;
                kernel.Velocity += gravity;
            }
            else if (Rng.Next8() < 2) // pop
            {
                kernel.Position = 0.01f;
                int peakHeight = 128 + Rng.Next8(128);
                peakHeight = peakHeight * (seg.Length - 1) >> 8;
                kernel.Velocity = MathF.Sqrt(-2.0f * gravity * peakHeight);

                if (seg.Palette != 0) kernel.ColorIndex = Rng.Next8();
                else
                {
                    byte col = Rng.Next8(0, Segment.ColorCount);
                    if (seg.Color(2).IsBlack || seg.Color(col).IsBlack) col = 0;
                    kernel.ColorIndex = col;
                }
            }

            if (kernel.Position < 0.0f) continue;

            Rgbw col2 = seg.ColorWheel(kernel.ColorIndex);
            if (seg.Palette == 0 && kernel.ColorIndex < Segment.ColorCount) col2 = seg.Color(kernel.ColorIndex);
            var ledIndex = (int)kernel.Position;
            if (ledIndex < seg.Length) seg.SetPixelColor(Segment.IndexToVStrip(ledIndex, stripNr), col2);
        }
    }

    /// <summary>Drops that swell at the top, fall, and bounce once when they land.</summary>
    public static void Drip(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        const int maxNumDrops = 4;
        int strips = seg.VerticalStripCount;
        Spark[] drops = seg.GetData<Spark>(maxNumDrops * strips);

        if (!seg.Check2) seg.Fill(seg.Color(1));

        for (int stripNr = 0; stripNr < strips; stripNr++)
            DripStrip(seg, stripNr, drops, stripNr * maxNumDrops);
    }

    private static void DripStrip(Segment seg, int stripNr, Spark[] drops, int offset)
    {
        int numDrops = 1 + (seg.Intensity >> 6); // up to four
        float gravity = -0.0005f - (seg.Speed / 50000.0f);
        gravity *= System.Math.Max(1, seg.Length - 1);
        const int sourceDrop = 12;

        for (int j = 0; j < numDrops; j++)
        {
            ref Spark drop = ref drops[offset + j];
            if (drop.ColorIndex == 0) // state 0: start a new drop at the source
            {
                drop.Position = seg.Length - 1;
                drop.Velocity = 0;
                drop.Color = sourceDrop; // brightness
                drop.ColorIndex = 1;     // 1 forming, 2 falling, 5 bouncing
            }

            // the water source at the far end is always faintly lit
            seg.SetPixelColor(Segment.IndexToVStrip(seg.Length - 1, stripNr),
                Rgbw.Blend(Rgbw.Black, seg.Color(0), sourceDrop));

            if (drop.ColorIndex == 1)
            {
                if (drop.Color > 255) drop.Color = 255;
                seg.SetPixelColor(Segment.IndexToVStrip((int)drop.Position, stripNr),
                    Rgbw.Blend(Rgbw.Black, seg.Color(0), (byte)drop.Color));

                drop.Color += (ushort)FastMath.Map(seg.Speed, 0, 255, 1, 6); // swelling

                if (Rng.Next8() < drop.Color / 10) // the bigger it gets, the likelier it falls
                {
                    drop.ColorIndex = 2;
                    drop.Color = 255;
                }
            }

            if (drop.ColorIndex <= 1) continue;

            if (drop.Position > 0)
            {
                drop.Position += drop.Velocity;
                if (drop.Position < 0) drop.Position = 0;
                drop.Velocity += gravity; // gravity is negative

                // a short tail behind the drop, shorter once it is bouncing
                for (int i = 1; i < 7 - drop.ColorIndex; i++)
                {
                    int pos = FastMath.Clamp((int)drop.Position + i, 0, seg.Length - 1);
                    seg.SetPixelColor(Segment.IndexToVStrip(pos, stripNr),
                        Rgbw.Blend(Rgbw.Black, seg.Color(0), (byte)(drop.Color / i)));
                }

                if (drop.ColorIndex > 2) // some water stays on the floor during the bounce
                    seg.SetPixelColor(Segment.IndexToVStrip(0, stripNr),
                        Rgbw.Blend(seg.Color(0), Rgbw.Black, (byte)drop.Color));
            }
            else if (drop.ColorIndex > 2) // second landing: start forming again
            {
                drop.ColorIndex = 0;
                drop.Color = sourceDrop;
            }
            else
            {
                if (drop.ColorIndex == 2) // first landing: bounce
                {
                    drop.Velocity = -drop.Velocity / 4; // reversed and damped
                    drop.Position += drop.Velocity;
                }
                drop.Color = sourceDrop * 2;
                drop.ColorIndex = 5;
            }
        }
    }

    /// <summary>Brightness modulated like a heartbeat, with a strong beat and a weaker second one.</summary>
    public static void Heartbeat(Segment seg)
    {
        int bpm = 40 + (seg.Speed >> 3);
        uint msPerBeat = (uint)(60000 / bpm);
        uint secondBeat = msPerBeat / 3;
        uint beatTimer = seg.Now - seg.Step;

        // exponential-ish decay; the intensity slider sets how long the pulse lingers
        uint briLower = (uint)seg.Aux1 * 2042 / (uint)(2048 + seg.Intensity);
        seg.Aux1 = (ushort)briLower;

        if (beatTimer > secondBeat && seg.Aux0 == 0)
        {
            seg.Aux1 = ushort.MaxValue;
            seg.Aux0 = 1;
        }
        if (beatTimer > msPerBeat)
        {
            seg.Aux1 = ushort.MaxValue;
            seg.Aux0 = 0;
            seg.Step = seg.Now;
        }

        for (int i = 0; i < seg.Length; i++)
            seg.SetPixelColor(i, Rgbw.Blend(seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0),
                seg.Color(1), (byte)(255 - (seg.Aux1 >> 8))));
    }

    /// <summary>A bar filling a percentage of the segment, sliding smoothly to the target level.</summary>
    public static void Percent(Segment seg)
    {
        int percent = FastMath.Clamp(seg.Intensity, 0, 200);
        var activeLeds = (int)MathF.Round(percent < 100
            ? seg.Length * percent / 100.0f
            : seg.Length * (200 - percent) / 100.0f);

        int size = 1 + ((seg.Speed * seg.Length) >> 11);
        if (seg.Speed == 255) size = 255;

        if (percent <= 100)
        {
            for (int i = 0; i < seg.Length; i++)
            {
                if (i < seg.Aux1)
                {
                    seg.SetPixelColor(i, seg.Check1
                        ? seg.ColorFromPalette(FastMath.Map(percent, 0, 100, 0, 255), false, false, 0)
                        : seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0));
                }
                else seg.SetPixelColor(i, seg.Color(1));
            }
        }
        else
        {
            for (int i = 0; i < seg.Length; i++)
            {
                if (i < seg.Length - seg.Aux1) seg.SetPixelColor(i, seg.Color(1));
                else
                {
                    seg.SetPixelColor(i, seg.Check1
                        ? seg.ColorFromPalette(FastMath.Map(percent, 100, 200, 255, 0), false, false, 0)
                        : seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0));
                }
            }
        }

        // ease towards the target level rather than snapping to it
        if (activeLeds > seg.Aux1) seg.Aux1 = (ushort)System.Math.Min(seg.Aux1 + size, activeLeds);
        else if (activeLeds < seg.Aux1)
            seg.Aux1 = (ushort)System.Math.Max(seg.Aux1 > size ? seg.Aux1 - size : 0, activeLeds);
    }

    /// <summary>
    /// Generates a tristate square wave with an attack and a decay.
    /// </summary>
    /// <param name="x">Input, 0-255.</param>
    /// <param name="pulseWidth">Width of the pulse, 0-127.</param>
    /// <param name="attackDecay">Attack and decay time, at most half the pulse width.</param>
    internal static int TristateSquare8(byte x, byte pulseWidth, byte attackDecay)
    {
        int a = 127;
        if (x > 127)
        {
            a = -127;
            x -= 127;
        }

        if (x < attackDecay) return x * a / attackDecay;           // ramping up
        if (x < pulseWidth - attackDecay) return a;                // held
        if (x < pulseWidth) return (pulseWidth - x) * a / attackDecay; // ramping down
        return 0;
    }

    /// <summary>A palette rocking back and forth like the drum of a washing machine.</summary>
    public static void WashingMachine(Segment seg)
    {
        int speed = TristateSquare8((byte)(seg.Now >> 7), 90, 15);

        seg.Step += (uint)(speed * 2048 / (512 - seg.Speed));

        for (int i = 0; i < seg.Length; i++)
        {
            byte col = FastMath.Sin8((byte)((seg.Intensity / 25 + 1) * 255 * i / seg.Length + (seg.Step >> 7)));
            seg.SetPixelColor(i, seg.ColorFromPalette(col, false, seg.PaletteSolidWrap, 3));
        }
    }
}
