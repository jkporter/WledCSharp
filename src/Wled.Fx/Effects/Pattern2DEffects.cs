namespace Wled.Fx;

/// <summary>
/// Two-dimensional effects driven by simulated objects and by geometric transforms: bees, blobs,
/// rotozoomers and radial patterns.
/// Port of the second 2D block of <c>FX.cpp</c>.
/// </summary>
public static class Pattern2DEffects
{
    internal static void Register()
    {
        EffectRegistry.Register(EffectId.TwoDCrazyBees, "Crazy Bees@!,Blur,,,,Smear;;!;2;pal=11,ix=0", CrazyBees);
        EffectRegistry.Register(EffectId.TwoDGhostRider, "Ghost Rider@Fade rate,Blur;;!;2", GhostRider);
        EffectRegistry.Register(EffectId.TwoDBlobs, "Blobs@!,# blobs,Blur,Trail;!;!;2;c1=8", Blobs);
        EffectRegistry.Register(EffectId.TwoDDriftRose, "Drift Rose@Fade,Blur,,,,Smear;;!;2;pal=11", DriftRose);
        EffectRegistry.Register(EffectId.TwoDPlasmaRotoZoom, "Rotozoomer@!,Scale,,,,Alt;;!;2;pal=54", PlasmaRotoZoom);
        EffectRegistry.Register(EffectId.TwoDDistortionWaves, "Distortion Waves@!,Scale,,,,Fill,Zoom,Alt;;!;2;pal=0", DistortionWaves);
        EffectRegistry.Register(EffectId.TwoDOctopus, "Octopus@!,,Offset X,Offset Y,Legs,fasttan;;!;2;", Octopus);
        EffectRegistry.Register(EffectId.TwoDWavingCell, "Waving Cell@!,Blur,Amplitude 1,Amplitude 2,Amplitude 3,,Flow;;!;2;ix=0", WavingCell);
    }

    private static bool Requires2D(Segment seg)
    {
        if (seg.Is2D) return true;
        BasicEffects.Static(seg);
        return false;
    }

    private const int MaxBees = 5;

    /// <summary>One bee, flying towards a flower by Bresenham.</summary>
    private struct Bee
    {
        public byte PosX, PosY, AimX, AimY, Hue;
        public int DeltaX, DeltaY, SignX, SignY, Error;

        public void Aim(Prng prng, int w, int h)
        {
            AimX = prng.Next8(0, (byte)w);
            AimY = prng.Next8(0, (byte)h);
            Hue = prng.Next8();
            DeltaX = System.Math.Abs(AimX - PosX);
            DeltaY = System.Math.Abs(AimY - PosY);
            SignX = PosX < AimX ? 1 : -1;
            SignY = PosY < AimY ? 1 : -1;
            Error = DeltaX - DeltaY;
        }
    }

    /// <summary>Bees darting between flowers that appear at random across the matrix.</summary>
    public static void CrazyBees(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        int n = System.Math.Min(MaxBees, rows * cols / 256 + 1);
        Bee[] bees = seg.GetData<Bee>(MaxBees);
        var prng = new Prng((ushort)seg.Now);

        if (seg.Call == 0)
        {
            for (int i = 0; i < n; i++)
            {
                bees[i].PosX = prng.Next8(0, (byte)cols);
                bees[i].PosY = prng.Next8(0, (byte)rows);
                bees[i].Aim(prng, cols, rows);
            }
        }

        if (seg.Now <= seg.Step) return;
        seg.Step = seg.Now + (uint)(seg.FrameTime * 16 / ((seg.Speed >> 4) + 1));
        seg.FadeToBlackBy((byte)(32 + (seg.Check1 ? seg.Intensity / 25 : 0)));
        seg.Blur((byte)(seg.Intensity / (2 + (seg.Check1 ? 9 : 0))), seg.Check1);

        for (int i = 0; i < n; i++)
        {
            // the flower is drawn as a diamond of four pixels around the target
            Rgbw flowerColor = seg.ColorFromPalette(bees[i].Hue, false, true, 255);
            seg.AddPixelColorXY(bees[i].AimX + 1, bees[i].AimY, flowerColor);
            seg.AddPixelColorXY(bees[i].AimX, bees[i].AimY + 1, flowerColor);
            seg.AddPixelColorXY(bees[i].AimX - 1, bees[i].AimY, flowerColor);
            seg.AddPixelColorXY(bees[i].AimX, bees[i].AimY - 1, flowerColor);

            if (bees[i].PosX != bees[i].AimX || bees[i].PosY != bees[i].AimY)
            {
                seg.SetPixelColorXY(bees[i].PosX, bees[i].PosY, new Crgb(new Chsv(bees[i].Hue, 60, 255)));
                int error2 = bees[i].Error * 2;
                if (error2 > -bees[i].DeltaY)
                {
                    bees[i].Error -= bees[i].DeltaY;
                    bees[i].PosX = (byte)(bees[i].PosX + bees[i].SignX);
                }
                if (error2 < bees[i].DeltaX)
                {
                    bees[i].Error += bees[i].DeltaX;
                    bees[i].PosY = (byte)(bees[i].PosY + bees[i].SignY);
                }
            }
            else bees[i].Aim(prng, cols, rows);
        }
    }

    private const int MaxLighters = 64;

    /// <summary>The rider and the trail of sparks it throws off.</summary>
    private struct Lighter
    {
        public int PosX;
        public int PosY;
        public ushort Angle;
        public int AngleSpeed;
        public int VerticalSpeed;
        public ushort[]? LighterPosX;
        public ushort[]? LighterPosY;
        public ushort[]? LighterAngle;
        public ushort[]? Time;
        public bool[]? Respawn;
    }

    /// <summary>A point racing around the matrix, throwing off a trail of sparks.</summary>
    public static void GhostRider(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        Lighter[] state = seg.GetData<Lighter>(1);
        ref Lighter lighter = ref state[0];
        int maxLighters = System.Math.Min(cols + rows, MaxLighters);

        if (lighter.LighterPosX is null || seg.Aux0 != cols || seg.Aux1 != rows)
        {
            seg.Aux0 = (ushort)cols;
            seg.Aux1 = (ushort)rows;
            lighter.LighterPosX = new ushort[MaxLighters];
            lighter.LighterPosY = new ushort[MaxLighters];
            lighter.LighterAngle = new ushort[MaxLighters];
            lighter.Time = new ushort[MaxLighters];
            lighter.Respawn = new bool[MaxLighters];
            lighter.AngleSpeed = Rng.Next8(0, 20) - 10;
            lighter.Angle = Rng.Next16();
            lighter.VerticalSpeed = 5;
            // positions are kept in tenths of a pixel so the motion stays smooth
            lighter.PosX = cols / 2 * 10;
            lighter.PosY = rows / 2 * 10;
            for (int i = 0; i < maxLighters; i++)
            {
                lighter.LighterPosX[i] = (ushort)lighter.PosX;
                lighter.LighterPosY[i] = (ushort)(lighter.PosY + i);
                lighter.Time[i] = (ushort)(i * 2);
                lighter.Respawn[i] = false;
            }
        }

        if (seg.Now <= seg.Step) return;
        seg.Step = seg.Now + (uint)(1024 / (cols + rows));

        seg.FadeToBlackBy((byte)((seg.Speed >> 2) + 64));
        seg.WuPixel(lighter.PosX * 256 / 10, lighter.PosY * 256 / 10, new Crgb(255, 255, 255));

        lighter.PosX += (int)(lighter.VerticalSpeed * FastMath.Sin(lighter.Angle * (FastMath.Pi / 180f)));
        lighter.PosY += (int)(lighter.VerticalSpeed * FastMath.Cos(lighter.Angle * (FastMath.Pi / 180f)));
        lighter.Angle = (ushort)(lighter.Angle + lighter.AngleSpeed);
        if (lighter.PosX < 0) lighter.PosX = (cols - 1) * 10;
        if (lighter.PosX > (cols - 1) * 10) lighter.PosX = 0;
        if (lighter.PosY < 0) lighter.PosY = (rows - 1) * 10;
        if (lighter.PosY > (rows - 1) * 10) lighter.PosY = 0;

        for (int i = 0; i < maxLighters; i++)
        {
            lighter.Time![i] += Rng.Next8(5, 20);
            if (lighter.Time[i] >= 255
                || lighter.LighterPosX![i] <= 0 || lighter.LighterPosX[i] >= (cols - 1) * 10
                || lighter.LighterPosY![i] <= 0 || lighter.LighterPosY[i] >= (rows - 1) * 10)
                lighter.Respawn![i] = true;

            if (lighter.Respawn![i]) // a spent spark restarts at the rider
            {
                lighter.LighterPosY![i] = (ushort)lighter.PosY;
                lighter.LighterPosX![i] = (ushort)lighter.PosX;
                lighter.LighterAngle![i] = (ushort)(lighter.Angle + (Rng.Next8(20) - 10));
                lighter.Time[i] = 0;
                lighter.Respawn[i] = false;
            }
            else
            {
                float radians = lighter.LighterAngle![i] * (FastMath.Pi / 180f);
                lighter.LighterPosX![i] = (ushort)(lighter.LighterPosX[i] + (int)(-7 * FastMath.Sin(radians)));
                lighter.LighterPosY![i] = (ushort)(lighter.LighterPosY[i] + (int)(-7 * FastMath.Cos(radians)));
            }
            seg.WuPixel(lighter.LighterPosX![i] * 256 / 10, lighter.LighterPosY![i] * 256 / 10,
                (Crgb)ColorUtil.ColorFromPalette(seg.CurrentPalette, 256 - lighter.Time[i]));
        }
        seg.Blur((byte)(seg.Intensity >> 3));
    }

    private const int MaxBlobs = 8;

    /// <summary>One blob: a disc that drifts, bounces and pulses in size.</summary>
    private struct Blob
    {
        public float X, Y;
        public float SpeedX, SpeedY;
        public float Radius;
        public bool Growing;
        public byte Color;
    }

    /// <summary>Discs of colour drifting around the matrix and bouncing off its edges.</summary>
    public static void Blobs(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        int amount = (seg.Intensity >> 5) + 1;
        Blob[] blobs = seg.GetData<Blob>(MaxBlobs);

        if (seg.Aux0 != cols || seg.Aux1 != rows) // (re)initialise when the size changes
        {
            seg.Aux0 = (ushort)cols;
            seg.Aux1 = (ushort)rows;
            for (int i = 0; i < MaxBlobs; i++)
            {
                blobs[i].Radius = Rng.Next8(1, (uint)(cols > 8 ? cols / 4 : 2));
                blobs[i].SpeedX = Rng.Next8(3, (uint)cols) / (float)(256 - seg.Speed);
                blobs[i].SpeedY = Rng.Next8(3, (uint)rows) / (float)(256 - seg.Speed);
                blobs[i].X = Rng.Next8(0, (uint)cols - 1);
                blobs[i].Y = Rng.Next8(0, (uint)rows - 1);
                blobs[i].Color = Rng.Next8();
                blobs[i].Growing = blobs[i].Radius < 1f;
                if (blobs[i].SpeedX == 0) blobs[i].SpeedX = 1;
                if (blobs[i].SpeedY == 0) blobs[i].SpeedY = 1;
            }
        }

        seg.FadeToBlackBy((byte)((seg.Custom2 >> 3) + 1));

        for (int i = 0; i < amount && i < MaxBlobs; i++)
        {
            if (seg.Step < seg.Now) blobs[i].Color += 4; // drift the hue

            float step = System.Math.Max(System.Math.Abs(blobs[i].SpeedX), System.Math.Abs(blobs[i].SpeedY)) * 0.05f;
            if (blobs[i].Growing)
            {
                blobs[i].Radius += step;
                if (blobs[i].Radius >= System.Math.Min(cols / 4f, 2f)) blobs[i].Growing = false;
            }
            else
            {
                blobs[i].Radius -= step;
                if (blobs[i].Radius < 1f) blobs[i].Growing = true;
            }

            Rgbw c = seg.ColorFromPalette(blobs[i].Color, false, false, 0);
            if (blobs[i].Radius > 1f)
                seg.FillCircle((int)MathF.Round(blobs[i].X), (int)MathF.Round(blobs[i].Y), (int)MathF.Round(blobs[i].Radius), c);
            else
                seg.SetPixelColorXY((int)MathF.Round(blobs[i].X), (int)MathF.Round(blobs[i].Y), c);

            // slow down as the blob edge approaches a wall, so it seems to squash against it
            if (blobs[i].X + blobs[i].Radius >= cols - 1)
                blobs[i].X += blobs[i].SpeedX * ((cols - 1 - blobs[i].X) / blobs[i].Radius + 0.005f);
            else if (blobs[i].X - blobs[i].Radius <= 0)
                blobs[i].X += blobs[i].SpeedX * (blobs[i].X / blobs[i].Radius + 0.005f);
            else blobs[i].X += blobs[i].SpeedX;

            if (blobs[i].Y + blobs[i].Radius >= rows - 1)
                blobs[i].Y += blobs[i].SpeedY * ((rows - 1 - blobs[i].Y) / blobs[i].Radius + 0.005f);
            else if (blobs[i].Y - blobs[i].Radius <= 0)
                blobs[i].Y += blobs[i].SpeedY * (blobs[i].Y / blobs[i].Radius + 0.005f);
            else blobs[i].Y += blobs[i].SpeedY;

            if (blobs[i].X < 0.01f)
            {
                blobs[i].SpeedX = Rng.Next8(3, (uint)cols) / (float)(256 - seg.Speed);
                blobs[i].X = 0.01f;
            }
            else if (blobs[i].X > cols - 1.01f)
            {
                blobs[i].SpeedX = -(Rng.Next8(3, (uint)cols) / (float)(256 - seg.Speed));
                blobs[i].X = cols - 1.01f;
            }

            if (blobs[i].Y < 0.01f)
            {
                blobs[i].SpeedY = Rng.Next8(3, (uint)rows) / (float)(256 - seg.Speed);
                blobs[i].Y = 0.01f;
            }
            else if (blobs[i].Y > rows - 1.01f)
            {
                blobs[i].SpeedY = -(Rng.Next8(3, (uint)rows) / (float)(256 - seg.Speed));
                blobs[i].Y = rows - 1.01f;
            }
        }
        seg.Blur((byte)(seg.Custom1 >> 2));

        if (seg.Step < seg.Now) seg.Step = seg.Now + 2000; // new colours every two seconds
    }

    /// <summary>Petals of light drawn out from the centre in a rose pattern.</summary>
    public static void DriftRose(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        float cx = (cols - cols % 2) / 2f - 0.5f;
        float cy = (rows - rows % 2) / 2f - 0.5f;
        float l = System.Math.Min(cols, rows) / 2f;

        seg.FadeToBlackBy((byte)(32 + (seg.Speed >> 3)));
        for (int i = 1; i < 37; i++) // 36 petals, ten degrees apart
        {
            float angle = i * 10 * (FastMath.Pi / 180f);
            var x = (int)((cx + FastMath.Sin(angle) * (Beat.Sin8((uint)i, 0, (byte)(l * 2)) - l)) * 255f);
            var y = (int)((cy + FastMath.Cos(angle) * (Beat.Sin8((uint)i, 0, (byte)(l * 2)) - l)) * 255f);
            Crgb color = seg.Palette == 0
                ? new Crgb(new Chsv(i * 10, 255, 255))
                : (Crgb)ColorUtil.ColorFromPalette(seg.CurrentPalette, i * 10);
            seg.WuPixel(x, y, color);
        }
        seg.Blur((byte)(seg.Intensity >> 4), seg.Check1);
    }

    /// <summary>A plasma field sampled through a rotating, zooming transform.</summary>
    public static void PlasmaRotoZoom(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        byte[] plasma = seg.GetData<byte>(cols * rows + 4);
        float angle = BitConverter.ToSingle(plasma, cols * rows); // the rotation angle rides along at the end
        uint ms = seg.Now / 15;

        for (int j = 0; j < rows; j++)
        {
            int index = j * cols;
            for (int i = 0; i < cols; i++)
            {
                plasma[index + i] = seg.Check1
                    ? (byte)(i * 4 ^ j * 4 + ms / 6)
                    : Perlin.Noise8((ushort)(i * 40), (ushort)(j * 40), (ushort)ms);
            }
        }

        float f = (FastMath.Sin(angle / 2) + (128 - seg.Intensity) / 128.0f + 1.1f) / 1.5f; // scale factor
        float cosine = FastMath.Cos(angle) * f;
        float sine = FastMath.Sin(angle) * f;
        for (int i = 0; i < cols; i++)
        {
            float u1 = i * cosine;
            float v1 = i * sine;
            for (int j = 0; j < rows; j++)
            {
                int u = System.Math.Abs((sbyte)(u1 - j * sine)) % cols;
                int v = System.Math.Abs((sbyte)(v1 + j * cosine)) % rows;
                seg.SetPixelColorXY(i, j, seg.ColorFromPalette(plasma[v * cols + u], false, seg.PaletteSolidWrap, 255));
            }
        }

        angle -= 0.03f + (seg.Speed - 128) * 0.0002f;
        // keep the angle bounded, or the sine approximation loses all precision
        if (angle < -6283.18530718f) angle += 6283.18530718f;
        BitConverter.TryWriteBytes(plasma.AsSpan(cols * rows), angle);
    }

    /// <summary>Overlapping ripples in the three colour channels, warped by moving centres.</summary>
    public static void DistortionWaves(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        int speed = seg.Speed / 32;
        int scale = seg.Intensity / 32;
        if (seg.Check2) scale += 192 / (cols + rows); // zoom further out

        uint a = seg.Now / 32;
        uint a2 = a / 2;
        uint a3 = a / 3;
        int colsScaled = cols * scale;
        int rowsScaled = rows * scale;

        int cx = Beat.Sin16((uint)(10 - speed), 0, (ushort)colsScaled);
        int cy = Beat.Sin16((uint)(12 - speed), 0, (ushort)rowsScaled);
        int cx1 = Beat.Sin16((uint)(13 - speed), 0, (ushort)colsScaled);
        int cy1 = Beat.Sin16((uint)(15 - speed), 0, (ushort)rowsScaled);
        int cx2 = Beat.Sin16((uint)(17 - speed), 0, (ushort)colsScaled);
        int cy2 = Beat.Sin16((uint)(14 - speed), 0, (ushort)rowsScaled);

        int xOffs = 0;
        for (int x = 0; x < cols; x++)
        {
            xOffs += scale;
            int yOffs = 0;
            for (int y = 0; y < rows; y++)
            {
                yOffs += scale;

                byte rDistort, gDistort, bDistort;
                if (seg.Check3) // the simpler diagonal variant from the original code
                {
                    rDistort = (byte)(FastMath.Cos8((byte)((x + y) * 8 + a2)) >> 1);
                    gDistort = (byte)(FastMath.Cos8((byte)((x + y) * 8 + a3 + 32)) >> 1);
                    bDistort = (byte)(FastMath.Cos8((byte)((x + y) * 8 + a + 64)) >> 1);
                }
                else
                {
                    rDistort = (byte)(FastMath.Cos8((byte)(FastMath.Cos8((byte)((x << 3) + a)) + FastMath.Cos8((byte)((y << 3) - a2)) + a3)) >> 1);
                    gDistort = (byte)(FastMath.Cos8((byte)(FastMath.Cos8((byte)((x << 3) - a2)) + FastMath.Cos8((byte)((y << 3) + a3)) + a + 32)) >> 1);
                    bDistort = (byte)(FastMath.Cos8((byte)(FastMath.Cos8((byte)((x << 3) + a3)) + FastMath.Cos8((byte)((y << 3) - a)) + a2 + 64)) >> 1);
                }

                // each channel ripples out from its own moving centre
                var valueR = (byte)(rDistort + ((a - (uint)(((xOffs - cx) * (xOffs - cx) + (yOffs - cy) * (yOffs - cy)) >> 7)) << 1));
                var valueG = (byte)(gDistort + ((a2 - (uint)(((xOffs - cx1) * (xOffs - cx1) + (yOffs - cy1) * (yOffs - cy1)) >> 7)) << 1));
                var valueB = (byte)(bDistort + ((a3 - (uint)(((xOffs - cx2) * (xOffs - cx2) + (yOffs - cy2) * (yOffs - cy2)) >> 7)) << 1));

                valueR = FastMath.Cos8(valueR);
                valueG = FastMath.Cos8(valueG);
                valueB = FastMath.Cos8(valueB);

                if (seg.Palette == 0)
                {
                    seg.SetPixelColorXY(x, y, new Rgbw(valueR, valueG, valueB));
                }
                else
                {
                    var brightness = (byte)((valueR + valueG + valueB) / 3);
                    if (seg.Check1)
                    {
                        seg.SetPixelColorXY(x, y, ColorUtil.ColorFromPalette(seg.CurrentPalette, brightness, 255, BlendType.LinearBlendNoWrap));
                    }
                    else
                    {
                        // scale down first so the hue survives; saturated channels carry no hue
                        Chsv32 hsv = ColorUtil.RgbToHsv(new Rgbw(valueR >> 2, valueG >> 2, valueB >> 2));
                        seg.SetPixelColorXY(x, y, ColorUtil.ColorFromPalette(seg.CurrentPalette, hsv.H >> 8, brightness));
                    }
                }
            }
        }

        // a smear hides the seam where the palette wraps
        if (!seg.Check1 && seg.Palette != 0) seg.Blur(200, true);
    }

    /// <summary>Polar coordinates of one pixel, precomputed once per geometry.</summary>
    private struct PolarMap
    {
        public byte Angle;
        public byte Radius;
    }

    /// <summary>Waves radiating out from a movable centre, like the arms of an octopus.</summary>
    public static void Octopus(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        int mapp = 180 / System.Math.Max(cols, rows);
        // one entry past the end remembers the offsets the map was built for
        PolarMap[] map = seg.GetData<PolarMap>(cols * rows + 1);
        ref PolarMap cachedOffsets = ref map[cols * rows];

        // the polar map only depends on the geometry and the offsets, so it is cached
        if (seg.Call == 0 || seg.Aux0 != cols || seg.Aux1 != rows
            || seg.Custom1 != cachedOffsets.Angle || seg.Custom2 != cachedOffsets.Radius)
        {
            seg.Step = 0;
            seg.Aux0 = (ushort)cols;
            seg.Aux1 = (ushort)rows;
            cachedOffsets.Angle = seg.Custom1;
            cachedOffsets.Radius = seg.Custom2;
            int centreX = cols / 2 + (seg.Custom1 - 128) * cols / 255;
            int centreY = rows / 2 + (seg.Custom2 - 128) * rows / 255;
            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    int dx = x - centreX;
                    int dy = y - centreY;
                    map[x + y * cols].Angle = (byte)(int)(40.7436f * FastMath.Atan2(dy, dx)); // 128*atan2/PI
                    map[x + y * cols].Radius = (byte)(MathF.Sqrt(dx * dx + dy * dy) * mapp);
                }
            }
        }

        seg.Step += (uint)(seg.Speed / 32 + 1);
        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                byte angle = map[x + y * cols].Angle;
                byte radius = map[x + y * cols].Radius;
                byte intensity = FastMath.Sin8((byte)(
                    FastMath.Sin8((byte)((angle * 4 - radius) / 4 + seg.Step / 2))
                    + radius - seg.Step + angle * (seg.Custom3 / 4 + 1)));
                seg.SetPixelColorXY(x, y, ColorUtil.ColorFromPalette(seg.CurrentPalette,
                    (int)(seg.Step / 2 - radius), intensity));
            }
        }
    }

    /// <summary>A grid of cells rippling in a standing wave.</summary>
    public static void WavingCell(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        uint t = (seg.Now * (uint)(seg.Speed + 1)) >> 3;
        uint aX = (uint)(seg.Custom1 / 16 + 9);
        uint aY = (uint)(seg.Custom2 / 16 + 1);
        uint aZ = (uint)(seg.Custom3 + 1);

        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                // the shifts keep more temporal resolution than plain 8-bit phase math would
                int wave = FastMath.Sin8((byte)(x * aX + FastMath.Sin8((byte)((((uint)y << 8) + t) * aY >> 8))))
                           + FastMath.Cos8((byte)(y * aZ));
                var colorIndex = (byte)(wave + (t >> (8 - (seg.Check2 ? 3 : 0))));
                seg.SetPixelColorXY(x, y, ColorUtil.ColorFromPalette(seg.CurrentPalette, colorIndex));
            }
        }
        seg.Blur(seg.Intensity);
    }
}
