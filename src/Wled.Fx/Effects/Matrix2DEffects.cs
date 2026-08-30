namespace Wled.Fx;

/// <summary>
/// Two-dimensional effects that paint the whole matrix from a field function: noise, plasma,
/// fractals and moving points.
/// Port of the 2D block of <c>FX.cpp</c>.
/// </summary>
/// <remarks>
/// Every effect here bails out to <see cref="BasicEffects.Static"/> on a segment that is not two
/// dimensional, exactly as the firmware does.
/// </remarks>
public static class Matrix2DEffects
{
    internal static void Register()
    {
        EffectRegistry.Register(EffectId.TwoDBlackHole, "Black Hole@Fade rate,Outer Y freq.,Outer X freq.,Inner X freq.,Inner Y freq.,Solid,,Blur;!;!;2;pal=11", BlackHole);
        EffectRegistry.Register(EffectId.TwoDColoredBursts, "Colored Bursts@Speed,# of lines,,,Blur,Gradient,Smear,Dots;;!;2;c3=16", ColoredBursts);
        EffectRegistry.Register(EffectId.TwoDDna, "DNA@Scroll speed,Blur,,,,Smear;;!;2;ix=0", Dna);
        EffectRegistry.Register(EffectId.TwoDDnaSpiral, "DNA Spiral@Scroll speed,Y frequency,Blur,,,Smear;;!;2;c1=0", DnaSpiral);
        EffectRegistry.Register(EffectId.TwoDDrift, "Drift@Rotation speed,Blur,,,,Twin,Smear;;!;2;ix=0", Drift);
        EffectRegistry.Register(EffectId.TwoDFireNoise, "Firenoise@X scale,Y scale,,,,Palette;;!;2;pal=66", FireNoise);
        EffectRegistry.Register(EffectId.TwoDFrizzles, "Frizzles@X frequency,Y frequency,Blur,,,Smear;;!;2", Frizzles);
        EffectRegistry.Register(EffectId.TwoDHiphotic, "Hiphotic@X scale,Y scale,,,Speed;!;!;2", Hiphotic);
        EffectRegistry.Register(EffectId.TwoDJulia, "Julia@,Max iterations per pixel,X center,Y center,Area size, Blur;!;!;2;ix=24,c1=128,c2=128,c3=16", Julia);
        EffectRegistry.Register(EffectId.TwoDLissajous, "Lissajous@X frequency,Fade rate,Blur,,Speed,Smear;!;!;2;c1=0", Lissajous);
        EffectRegistry.Register(EffectId.TwoDMatrix, "Matrix@!,Spawning rate,Trail,,,Custom color;Spawn,Trail;;2", MatrixRain);
        EffectRegistry.Register(EffectId.TwoDMetaballs, "Metaballs@!;;!;2", Metaballs);
        EffectRegistry.Register(EffectId.TwoDNoise, "Noise2D@!,Scale;;!;2", Noise2D);
        EffectRegistry.Register(EffectId.TwoDPlasmaBall, "Plasma Ball@Speed,,Fade,Blur;;!;2", PlasmaBall);
        EffectRegistry.Register(EffectId.TwoDPolarLights, "Polar Lights@!,Scale,,,,Flip Palette;;!;2;pal=71", PolarLights);
        EffectRegistry.Register(EffectId.TwoDPulser, "Pulser@!,Blur;;!;2", Pulser);
        EffectRegistry.Register(EffectId.TwoDSinDots, "Sindots@!,Dot distance,Fade rate,Blur,,Smear;;!;2;", SinDots);
        EffectRegistry.Register(EffectId.TwoDSquaredSwirl, "Squared Swirl@,Fade,,,Blur;;!;2", SquaredSwirl);
        EffectRegistry.Register(EffectId.TwoDSunRadiation, "Sun Radiation@Variance,Brightness;;;2", SunRadiation);
        EffectRegistry.Register(EffectId.TwoDTartan, "Tartan@X scale,Y scale,,,Sharpness;;!;2", Tartan);
        EffectRegistry.Register(EffectId.TwoDSpaceships, "Spaceships@!,Blur,,,,Smear;;!;2", Spaceships);
    }

    /// <summary>True when the segment can actually render a 2D effect.</summary>
    private static bool Requires2D(Segment seg)
    {
        if (seg.Is2D) return true;
        BasicEffects.Static(seg);
        return false;
    }

    /// <summary>Stars orbiting a bright centre, leaving fading trails.</summary>
    public static void BlackHole(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        seg.FadeToBlackBy((byte)(16 + (seg.Speed >> 3))); // trails
        uint t = seg.Now / 128;

        for (int i = 0; i < 8; i++) // outer stars
        {
            int x = Beat.Sin8((uint)(seg.Custom1 >> 3), 0, (byte)(cols - 1), 0, (byte)((i % 2 != 0 ? 128 : 0) + t * i));
            int y = Beat.Sin8((uint)(seg.Intensity >> 3), 0, (byte)(rows - 1), 0, (byte)((i % 2 != 0 ? 192 : 64) + t * i));
            seg.AddPixelColorXY(x, y, seg.ColorFromPalette(i * 32, false, seg.PaletteSolidWrap, seg.Check1 ? (byte)0 : (byte)255));
        }
        for (int i = 0; i < 4; i++) // inner stars
        {
            int x = Beat.Sin8((uint)(seg.Custom2 >> 3), (byte)(cols / 4), (byte)(cols - 1 - cols / 4), 0, (byte)((i % 2 != 0 ? 128 : 0) + t * i));
            int y = Beat.Sin8(seg.Custom3, (byte)(rows / 4), (byte)(rows - 1 - rows / 4), 0, (byte)((i % 2 != 0 ? 192 : 64) + t * i));
            seg.AddPixelColorXY(x, y, seg.ColorFromPalette(255 - i * 64, false, seg.PaletteSolidWrap, seg.Check1 ? (byte)0 : (byte)255));
        }

        seg.SetPixelColorXY(cols / 2, rows / 2, Colors.White); // the hole itself
        if (seg.Check3) seg.Blur(16, cols * rows < 100);
    }

    /// <summary>Gradient lines sweeping across the matrix and bursting into colour.</summary>
    public static void ColoredBursts(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        if (seg.Call == 0) seg.Aux0 = 0;

        bool dot = seg.Check3;
        bool gradient = seg.Check1;
        int numLines = seg.Intensity / 16 + 1;

        seg.Aux0++; // hue
        seg.FadeToBlackBy((byte)(40 - (seg.Check2 ? 8 : 0)));

        for (int i = 0; i < numLines; i++)
        {
            byte x1 = Beat.Sin8((uint)(2 + seg.Speed / 16), 0, (byte)(cols - 1));
            byte x2 = Beat.Sin8((uint)(1 + seg.Speed / 16), 0, (byte)(rows - 1));
            byte y1 = Beat.Sin8((uint)(5 + seg.Speed / 16), 0, (byte)(cols - 1), 0, (byte)(i * 24));
            byte y2 = Beat.Sin8((uint)(3 + seg.Speed / 16), 0, (byte)(rows - 1), 0, (byte)(i * 48 + 64));
            Rgbw color = ColorUtil.ColorFromPalette(seg.CurrentPalette, i * 255 / numLines + (seg.Aux0 & 0xFF));

            byte xSteps = (byte)(System.Math.Abs((sbyte)(x1 - y1)) + 1);
            byte ySteps = (byte)(System.Math.Abs((sbyte)(x2 - y2)) + 1);
            byte steps = xSteps >= ySteps ? xSteps : ySteps;

            for (int j = 1; j <= steps; j++)
            {
                var rate = (byte)(j * 255 / steps);
                byte dx = FastMath.Lerp8By8(x1, y1, rate);
                byte dy = FastMath.Lerp8By8(x2, y2, rate);
                seg.AddPixelColorXY(dx, dy, color);
                if (gradient) seg.FadePixelColorXY(dx, dy, rate);
            }

            if (dot) // bright points at both ends of the line
            {
                seg.SetPixelColorXY(x1, x2, Colors.White);
                seg.SetPixelColorXY(y1, y2, Colors.DarkSlateGrey);
            }
        }
        seg.Blur((byte)(seg.Custom3 >> 1), seg.Check2);
    }

    /// <summary>Two sine waves winding around each other like a DNA helix.</summary>
    public static void Dna(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        seg.FadeToBlackBy(64);
        for (int i = 0; i < cols; i++)
        {
            seg.SetPixelColorXY(i, Beat.Sin8((uint)(seg.Speed / 8), 0, (byte)(rows - 1), 0, (byte)(i * 4)),
                ColorUtil.ColorFromPalette(seg.CurrentPalette, (int)(i * 5 + seg.Now / 17),
                    Beat.Sin8(5, 55, 255, 0, (byte)(i * 10))));
            seg.SetPixelColorXY(i, Beat.Sin8((uint)(seg.Speed / 8), 0, (byte)(rows - 1), 0, (byte)(i * 4 + 128)),
                ColorUtil.ColorFromPalette(seg.CurrentPalette, (int)(i * 5 + 128 + seg.Now / 17),
                    Beat.Sin8(5, 55, 255, 0, (byte)(i * 10 + 128))));
        }
        seg.Blur((byte)(seg.Intensity / (8 - (seg.Check1 ? 2 : 0))), seg.Check1);
    }

    /// <summary>A helix drawn row by row, with a bright strand on each side.</summary>
    public static void DnaSpiral(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        if (seg.Call == 0) seg.Fill(Rgbw.Black);

        int speeds = seg.Speed / 2 + 7;
        int freq = seg.Intensity / 8;
        uint ms = seg.Now / 20;
        seg.FadeToBlackBy(135);

        for (int i = 0; i < rows; i++)
        {
            int x = Beat.Sin8((uint)speeds, 0, (byte)(cols - 1), 0, (byte)(i * freq))
                  + Beat.Sin8((uint)(speeds - 7), 0, (byte)(cols - 1), 0, (byte)(i * freq + 128));
            int x1 = Beat.Sin8((uint)speeds, 0, (byte)(cols - 1), 0, (byte)(128 + i * freq))
                   + Beat.Sin8((uint)(speeds - 7), 0, (byte)(cols - 1), 0, (byte)(128 + 64 + i * freq));
            var hue = (byte)(i * 128 / rows + ms);

            if (((i + ms / 8) & 3) == 0) continue; // fade every fourth row now and then

            x /= 2;
            x1 /= 2;
            int steps = System.Math.Abs((sbyte)(x - x1)) + 1;
            bool positive = x1 >= x;
            for (int k = 1; k <= steps; k++)
            {
                var rate = (byte)(k * 255 / steps);
                // stepping rather than interpolating avoids leaving holes in the line
                int dx = positive ? x + k - 1 : x - k + 1;
                seg.AddPixelColorXY(dx, i, ColorUtil.ColorFromPalette(seg.CurrentPalette, hue));
                seg.FadePixelColorXY(dx, i, rate);
            }
            seg.SetPixelColorXY(x, i, Colors.DarkSlateGrey);
            seg.SetPixelColorXY(x1, i, Colors.White);
        }
        seg.Blur((byte)(seg.Custom1 * 3 / (6 + (seg.Check1 ? 1 : 0))), seg.Check1);
    }

    /// <summary>An expanding spiral drawn from the centre outwards.</summary>
    public static void Drift(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        int colsCenter = (cols >> 1) + (cols % 2);
        int rowsCenter = (rows >> 1) + (rows % 2);

        seg.FadeToBlackBy(128);
        float maxDim = System.Math.Max(cols, rows) / 2f;
        uint t = seg.Now / (uint)(32 - (seg.Speed >> 3));
        uint t20 = t / 20;

        for (float i = 1.0f; i < maxDim; i += 0.25f)
        {
            float angle = t * (maxDim - i) * (FastMath.Pi / 180f);
            var mySin = (int)(FastMath.Sin(angle) * i);
            var myCos = (int)(FastMath.Cos(angle) * i);
            Rgbw color = ColorUtil.ColorFromPalette(seg.CurrentPalette, (int)(i * 20 + t20));
            seg.SetPixelColorXY(colsCenter + mySin, rowsCenter + myCos, color);
            if (seg.Check1) seg.SetPixelColorXY(colsCenter + myCos, rowsCenter + mySin, color);
        }
        seg.Blur((byte)(seg.Intensity >> (3 - (seg.Check2 ? 1 : 0))), seg.Check2);
    }

    private static readonly Palette16 FirePalette = new(
    [
        new Crgb(0x000000), new Crgb(0x000000), new Crgb(0x000000), new Crgb(0x000000),
        new Crgb(0xFF0000), new Crgb(0xFF0000), new Crgb(0xFF0000), new Crgb(0xFF8C00),
        new Crgb(0xFF8C00), new Crgb(0xFF8C00), new Crgb(0xFFA500), new Crgb(0xFFA500),
        new Crgb(0xFFFF00), new Crgb(0xFFA500), new Crgb(0xFFFF00), new Crgb(0xFFFF00),
    ]);

    /// <summary>Flames rising up the matrix, driven by a scrolling noise field.</summary>
    public static void FireNoise(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        if (seg.Call == 0) seg.Fill(Rgbw.Black);

        int xScale = seg.Intensity * 4;
        int yScale = seg.Speed * 8;
        Palette16 pal = seg.Check1 ? seg.CurrentPalette : FirePalette;

        for (int j = 0; j < cols; j++)
        {
            for (int i = 0; i < rows; i++)
            {
                byte index = Perlin.Noise8((ushort)(j * yScale * rows / 255), (ushort)(i * xScale + seg.Now / 4));
                seg.SetPixelColorXY(j, i, ColorUtil.ColorFromPalette(pal,
                    System.Math.Min(i * index / 11, 225), (byte)(i * 255 / rows)));
            }
        }
    }

    /// <summary>Points bouncing around the matrix on out-of-phase sines, blurred into curls.</summary>
    public static void Frizzles(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        seg.FadeToBlackBy((byte)(16 + (seg.Check1 ? 10 : 0)));
        for (int i = 8; i > 0; i--)
        {
            seg.AddPixelColorXY(
                Beat.Sin8((uint)(seg.Speed / 8 + i), 0, (byte)(cols - 1)),
                Beat.Sin8((uint)(seg.Intensity / 8 - i), 0, (byte)(rows - 1)),
                ColorUtil.ColorFromPalette(seg.CurrentPalette, Beat.Sin8(12, 0, 255)));
        }
        seg.Blur((byte)(seg.Custom1 >> (3 + (seg.Check1 ? 1 : 0))), seg.Check1);
    }

    /// <summary>Interfering sine fields producing a hypnotic moire pattern.</summary>
    public static void Hiphotic(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        uint a = seg.Now / (uint)((seg.Custom3 >> 1) + 1);

        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                byte index = FastMath.Sin8((byte)(
                    FastMath.Cos8((byte)(x * seg.Speed / 16 + a / 3))
                    + FastMath.Sin8((byte)(y * seg.Intensity / 16 + a / 4))
                    + a));
                seg.SetPixelColorXY(x, y, seg.ColorFromPalette(index, false, seg.PaletteSolidWrap, 0));
            }
        }
    }

    /// <summary>Julia set state: the viewport the fractal is sampled through.</summary>
    private struct JuliaState
    {
        public float CenterX;
        public float CenterY;
        public float Magnification;
    }

    /// <summary>An animated Julia set; the sliders pan and zoom the viewport.</summary>
    public static void Julia(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        JuliaState[] state = seg.GetData<JuliaState>(1);
        ref JuliaState julia = ref state[0];

        if (seg.Call == 0)
        {
            julia.CenterX = 0f;
            julia.CenterY = 0f;
            julia.Magnification = 1.0f;
            seg.Custom1 = 128; // centre the pan controls
            seg.Custom2 = 128;
            seg.Custom3 = 16;
            seg.Intensity = 24;
        }

        // the sliders steer a velocity rather than a position, so the view drifts smoothly
        julia.CenterX += (seg.Custom1 - 128) / 100000f;
        julia.CenterY += (seg.Custom2 - 128) / 100000f;
        julia.Magnification += ((seg.Custom3 - 16) << 3) / 100000f;
        julia.Magnification = System.Math.Clamp(julia.Magnification, 0.01f, 1.0f);

        // the interesting part of the set lives inside these bounds
        float xMin = System.Math.Clamp(julia.CenterX - julia.Magnification, -1.2f, 1.2f);
        float xMax = System.Math.Clamp(julia.CenterX + julia.Magnification, -1.2f, 1.2f);
        float yMin = System.Math.Clamp(julia.CenterY - julia.Magnification, -0.8f, 1.0f);
        float yMax = System.Math.Clamp(julia.CenterY + julia.Magnification, -0.8f, 1.0f);

        int maxIterations = seg.Intensity / 2;
        const float maxCalc = 16.0f;

        float reAl = -0.94299f + FastMath.Sin16((ushort)(seg.Now * 34)) / 655340f;
        float imAg = 0.3162f + FastMath.Sin16((ushort)(seg.Now * 26)) / 655340f;

        float dx = (xMax - xMin) / cols;
        float dy = (yMax - yMin) / rows;

        float y = yMin;
        for (int j = 0; j < rows; j++)
        {
            float x = xMin;
            for (int i = 0; i < cols; i++)
            {
                float a = x, b = y;
                int iter = 0;
                while (iter < maxIterations)
                {
                    float aa = a * a;
                    float bb = b * b;
                    if (aa + bb > maxCalc) break; // comparing squares avoids a square root
                    b = 2 * a * b + imAg;         // z -> z^2 + c
                    a = aa - bb + reAl;
                    iter++;
                }

                seg.SetPixelColorXY(i, j, iter == maxIterations
                    ? Rgbw.Black
                    : seg.ColorFromPalette(iter * 255 / System.Math.Max(maxIterations, 1), false, seg.PaletteSolidWrap, 0));
                x += dx;
            }
            y += dy;
        }
        if (seg.Check1) seg.Blur(100, true);
    }

    /// <summary>A Lissajous curve traced across the matrix.</summary>
    public static void Lissajous(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        seg.FadeToBlackBy(seg.Intensity);
        uint phase = seg.Now * (uint)(1 + seg.Custom3) / 32;

        for (int i = 0; i < 256; i++)
        {
            int xLocn = FastMath.Sin8((byte)(phase / 2 + i * seg.Speed / 32));
            int yLocn = FastMath.Cos8((byte)(phase / 2 + i * 2));
            // doubling before the map and halving after gives proper rounding
            xLocn = cols < 2 ? 1 : (FastMath.Map(2 * xLocn, 0, 511, 0, 2 * (cols - 1)) + 1) / 2;
            yLocn = rows < 2 ? 1 : (FastMath.Map(2 * yLocn, 0, 511, 0, 2 * (rows - 1)) + 1) / 2;
            seg.SetPixelColorXY(xLocn, yLocn, seg.ColorFromPalette((int)(seg.Now / 100 + (uint)i), false, seg.PaletteSolidWrap, 0));
        }
        seg.Blur((byte)(seg.Custom1 >> (1 + (seg.Check1 ? 3 : 0))), seg.Check1);
    }

    /// <summary>Columns of code falling down the matrix.</summary>
    public static void MatrixRain(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        byte[] falling = seg.GetData<byte>((cols * rows + 7) >> 3); // one bit per pixel

        if (seg.Call == 0)
        {
            seg.Fill(Rgbw.Black);
            seg.Step = 0;
        }

        var fade = (byte)FastMath.Map(seg.Custom1, 0, 255, 30, 250); // trail length
        int speed = (256 - seg.Speed) >> FastMath.Map(System.Math.Min(rows, 150), 0, 150, 0, 3);

        Rgbw spawnColor, trailColor;
        if (seg.Check1)
        {
            spawnColor = seg.Color(0);
            trailColor = seg.Color(1);
        }
        else
        {
            // inverse gamma keeps the classic green of the pre-0.16 firmware
            spawnColor = new Rgbw(Gamma.RawInverse8(175), Gamma.RawInverse8(255), Gamma.RawInverse8(175));
            trailColor = new Rgbw(Gamma.RawInverse8(27), Gamma.RawInverse8(130), Gamma.RawInverse8(39));
        }

        if (seg.Now - seg.Step < speed) return;
        seg.Step = seg.Now;

        int Xy(int x, int y) => x % cols + y % rows * cols;

        bool emptyScreen = true;
        seg.FadeToBlackBy(fade);
        for (int row = rows - 1; row >= 0; row--)
        {
            for (int col = 0; col < cols; col++)
            {
                int index = Xy(col, row) >> 3;
                int bitNum = Xy(col, row) & 0x07;
                if ((falling[index] & (1 << bitNum)) == 0) continue;

                seg.SetPixelColorXY(col, row, trailColor);
                falling[index] &= (byte)~(1 << bitNum);
                if (row >= rows - 1) continue;

                seg.SetPixelColorXY(col, row + 1, spawnColor);
                falling[Xy(col, row + 1) >> 3] |= (byte)(1 << (Xy(col, row + 1) & 0x07));
                emptyScreen = false;
            }
        }

        if (Rng.Next8() <= seg.Intensity || emptyScreen)
        {
            byte spawnX = Rng.Next8((uint)cols);
            seg.SetPixelColorXY(spawnX, 0, spawnColor);
            falling[Xy(spawnX, 0) >> 3] |= (byte)(1 << (Xy(spawnX, 0) & 0x07));
        }
    }

    /// <summary>Three moving points whose distance fields merge into blobs.</summary>
    public static void Metaballs(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        float speed = 0.25f * (1 + (seg.Speed >> 6));

        // two points wander on noise, the third traces a Lissajous curve
        int x2 = FastMath.Map(Perlin.Noise8((ushort)(seg.Now * speed), 25355, 685), 0, 255, 0, cols - 1);
        int y2 = FastMath.Map(Perlin.Noise8((ushort)(seg.Now * speed), 355, 11685), 0, 255, 0, rows - 1);
        int x3 = FastMath.Map(Perlin.Noise8((ushort)(seg.Now * speed), 55355, 6685), 0, 255, 0, cols - 1);
        int y3 = FastMath.Map(Perlin.Noise8((ushort)(seg.Now * speed), 25355, 22685), 0, 255, 0, rows - 1);
        int x1 = Beat.Sin8((uint)(23 * speed), 0, (byte)(cols - 1));
        int y1 = Beat.Sin8((uint)(28 * speed), 0, (byte)(rows - 1));

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int dx = System.Math.Abs(x - x1), dy = System.Math.Abs(y - y1);
                uint dist = 2 * FastMath.Sqrt32((uint)(dx * dx + dy * dy)); // the first point weighs double

                dx = System.Math.Abs(x - x2); dy = System.Math.Abs(y - y2);
                dist += FastMath.Sqrt32((uint)(dx * dx + dy * dy));

                dx = System.Math.Abs(x - x3); dy = System.Math.Abs(y - y3);
                dist += FastMath.Sqrt32((uint)(dx * dx + dy * dy));

                int color = dist != 0 ? (int)(1000 / dist) : 255;
                seg.SetPixelColorXY(x, y, color is > 0 and < 60
                    ? seg.ColorFromPalette(FastMath.Map(color * 9, 9, 531, 0, 255), false, seg.PaletteSolidWrap, 0)
                    : seg.ColorFromPalette(0, false, seg.PaletteSolidWrap, 0));
            }
        }

        seg.SetPixelColorXY(x1, y1, Colors.White);
        seg.SetPixelColorXY(x2, y2, Colors.White);
        seg.SetPixelColorXY(x3, y3, Colors.White);
    }

    /// <summary>A drifting field of Perlin noise.</summary>
    public static void Noise2D(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        int scale = seg.Intensity + 2;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                byte hue = Perlin.Noise8((ushort)(x * scale), (ushort)(y * scale),
                    (ushort)(seg.Now / (uint)(16 - seg.Speed / 16)));
                seg.SetPixelColorXY(x, y, ColorUtil.ColorFromPalette(seg.CurrentPalette, hue));
            }
        }
    }

    /// <summary>Arcs of light snapping between the edges of the matrix.</summary>
    public static void PlasmaBall(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        seg.FadeToBlackBy((byte)(seg.Custom1 >> 2));
        uint t = seg.Now * 8 / (uint)(256 - seg.Speed);

        for (int i = 0; i < cols; i++)
        {
            byte thisVal = Perlin.Noise8((ushort)(i * 30), (ushort)t, (ushort)t);
            int thisMax = FastMath.Map(thisVal, 0, 255, 0, cols - 1);
            for (int j = 0; j < rows; j++)
            {
                byte thisValY = Perlin.Noise8((ushort)t, (ushort)(j * 30), (ushort)t);
                int thisMaxY = FastMath.Map(thisValY, 0, 255, 0, rows - 1);
                int x = i + thisMaxY - cols / 2;
                int y = j + thisMax - cols / 2;
                int cx = i + thisMaxY;
                int cy = j + thisMax;

                // light the pixel only where it sits on one of the arc diagonals or edges
                bool onArc = (x - y > -2 && x - y < 2)
                             || (cols - 1 - x - y > -2 && cols - 1 - x - y < 2)
                             || cols - cx == 0
                             || cols - 1 - cx == 0
                             || rows - cy == 0
                             || rows - 1 - cy == 0;
                seg.AddPixelColorXY(i, j, onArc
                    ? ColorUtil.ColorFromPalette(seg.CurrentPalette, Beat.Beat8(5), thisVal)
                    : Rgbw.Black);
            }
        }
        seg.Blur((byte)(seg.Custom2 >> 5));
    }

    /// <summary>Curtains of light rising up the matrix, like an aurora seen edge on.</summary>
    public static void PolarLights(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        if (seg.Call == 0)
        {
            seg.Fill(Rgbw.Black);
            seg.Step = 0;
        }

        // the curtain is squeezed towards the middle row, and more so on a short matrix
        float adjustHeight = FastMath.Map(rows, 8, 32, 28, 12);
        int adjScale = FastMath.Map(cols, 8, 64, 310, 63);
        int scale = FastMath.Map(seg.Intensity, 0, 255, 30, adjScale);
        int speed = FastMath.Map(seg.Speed, 0, 255, 128, 16);

        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                seg.Step++;
                byte paletteIndex = FastMath.QSub8(
                    Perlin.Noise8((ushort)(seg.Step % 2 + x * scale), (ushort)(y * 16 + seg.Step % 16), (ushort)(seg.Step / (uint)speed)),
                    (byte)(System.Math.Abs(rows / 2.0f - y) * adjustHeight));
                byte brightness = paletteIndex;
                if (seg.Check1) paletteIndex = (byte)(255 - paletteIndex);
                seg.SetPixelColorXY(x, y, seg.ColorFromPalette(paletteIndex, false, false, 255, brightness));
            }
        }
    }

    /// <summary>A dot pulsing up and down as it scans across the matrix.</summary>
    public static void Pulser(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        seg.FadeToBlackBy((byte)(8 - (seg.Intensity >> 5)));
        uint a = seg.Now / (uint)(18 - seg.Speed / 16);
        var x = (int)(a / 14 % (uint)cols);
        // three sines added together give the vertical motion its bounce
        int y = FastMath.Map(FastMath.Sin8((byte)(a * 5)) + FastMath.Sin8((byte)(a * 4)) + FastMath.Sin8((byte)(a * 2)),
            0, 765, rows - 1, 0);
        seg.SetPixelColorXY(x, y, ColorUtil.ColorFromPalette(seg.CurrentPalette, FastMath.Map(y, 0, rows - 1, 0, 255)));
        seg.Blur((byte)(seg.Intensity >> 4));
    }

    /// <summary>Thirteen dots tracing out sine figures.</summary>
    public static void SinDots(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        if (seg.Call == 0) seg.Fill(Rgbw.Black);

        seg.FadeToBlackBy((byte)((seg.Custom1 >> 3) + (seg.Check1 ? 24 : 0)));

        var t1 = (byte)(seg.Now / (uint)(257 - seg.Speed));
        var t2 = (byte)(FastMath.Sin8(t1) / 4 * 2);
        for (int i = 0; i < 13; i++)
        {
            int x = FastMath.Sin8((byte)(t1 + i * seg.Intensity / 8)) * (cols - 1) / 255;
            int y = FastMath.Sin8((byte)(t2 + i * seg.Intensity / 8)) * (rows - 1) / 255;
            seg.SetPixelColorXY(x, y, ColorUtil.ColorFromPalette(seg.CurrentPalette, i * 255 / 13));
        }
        seg.Blur((byte)(seg.Custom2 >> (3 + (seg.Check1 ? 1 : 0))), seg.Check1);
    }

    /// <summary>Three points blurred into swirling ribbons of colour.</summary>
    public static void SquaredSwirl(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        const byte borderWidth = 2;

        seg.FadeToBlackBy((byte)(1 + seg.Intensity / 5));
        seg.Blur((byte)(seg.Custom3 >> 1));

        // six out-of-sync sines give three points that never repeat their path
        int i = Beat.Sin8(19, borderWidth, (byte)(cols - borderWidth));
        int j = Beat.Sin8(22, borderWidth, (byte)(cols - borderWidth));
        int k = Beat.Sin8(17, borderWidth, (byte)(cols - borderWidth));
        int m = Beat.Sin8(18, borderWidth, (byte)(rows - borderWidth));
        int n = Beat.Sin8(15, borderWidth, (byte)(rows - borderWidth));
        int p = Beat.Sin8(20, borderWidth, (byte)(rows - borderWidth));

        seg.AddPixelColorXY(i, m, ColorUtil.ColorFromPalette(seg.CurrentPalette, (int)(seg.Now / 29)));
        seg.AddPixelColorXY(j, n, ColorUtil.ColorFromPalette(seg.CurrentPalette, (int)(seg.Now / 41)));
        seg.AddPixelColorXY(k, p, ColorUtil.ColorFromPalette(seg.CurrentPalette, (int)(seg.Now / 73)));
    }

    /// <summary>
    /// A bump-mapped surface lit from a moving point, which reads as the churning surface of a sun.
    /// </summary>
    public static void SunRadiation(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        byte[] bump = seg.GetData<byte>((cols + 2) * (rows + 2)); // one pixel of margin all round

        if (seg.Call == 0) seg.Fill(Rgbw.Black);

        uint t = seg.Now / 4;
        int index = 0;
        var someVal = (byte)(seg.Speed / 4);
        for (int j = 0; j < rows + 2; j++)
        {
            for (int i = 0; i < cols + 2; i++)
            {
                var col = (byte)((Perlin.Noise8((ushort)(i * someVal), (ushort)(j * someVal), (ushort)t) - 127) >> 2);
                bump[index++] = col;
            }
        }

        int yIndex = cols + 3;
        int vly = -(rows / 2 + 1);
        for (int y = 0; y < rows; y++)
        {
            vly++;
            int vlx = -(cols / 2 + 1);
            for (int x = 0; x < cols; x++)
            {
                vlx++;
                // the local gradient of the bump map is the surface normal
                int nx = bump[x + yIndex + 1] - bump[x + yIndex - 1];
                int ny = bump[x + yIndex + (cols + 2)] - bump[x + yIndex - (cols + 2)];
                int difX = System.Math.Abs((sbyte)(vlx * 7 - nx));
                int difY = System.Math.Abs((sbyte)(vly * 7 - ny));
                int temp = difX * difX + difY * difY;
                int col = System.Math.Max(255 - temp / 8, 0);
                seg.SetPixelColorXY(x, y, ColorUtil.HeatColor((byte)(col / (3.0f - seg.Intensity / 128.0f))));
            }
            yIndex += cols + 2;
        }
    }

    /// <summary>Crossed bands of colour, like woven tartan.</summary>
    public static void Tartan(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        if (seg.Call == 0) seg.Fill(Rgbw.Black);

        int offsetX = (short)Beat.Sin16(3, unchecked((ushort)-360), 360);
        int offsetY = (short)Beat.Sin16(2, unchecked((ushort)-360), 360);
        int sharpness = seg.Custom3 / 8; // 0-3

        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                var hue = (byte)(x * Beat.Sin16(10, 1, 10) + offsetY);
                int bri = FastMath.Sin8((byte)(x * seg.Speed / 2 + offsetX));
                int intensity = bri;
                // raising the band to a power sharpens its edges
                for (int i = 0; i < sharpness; i++) intensity *= bri;
                intensity >>= 8 * sharpness;
                seg.SetPixelColorXY(x, y, ColorUtil.ColorFromPalette(seg.CurrentPalette, hue, (byte)intensity));

                hue = (byte)(y * 3 + offsetX);
                bri = FastMath.Sin8((byte)(y * seg.Intensity / 2 + offsetY));
                intensity = bri;
                for (int i = 0; i < sharpness; i++) intensity *= bri;
                intensity >>= 8 * sharpness;
                seg.AddPixelColorXY(x, y, ColorUtil.ColorFromPalette(seg.CurrentPalette, hue, (byte)intensity));
            }
        }
    }

    /// <summary>Points of light drifting across the matrix in a slowly changing direction.</summary>
    public static void Spaceships(Segment seg)
    {
        if (!Requires2D(seg)) return;

        int cols = seg.Width, rows = seg.Height;
        uint tb = seg.Now >> 12; // roughly every four seconds
        if (tb > seg.Step)
        {
            int dir = ++seg.Aux0;
            dir += Rng.Next8(3) - 1;
            seg.Aux0 = (ushort)(dir > 7 ? 0 : dir < 0 ? 7 : dir);
            seg.Step = tb + Rng.Next8(4);
        }

        seg.FadeToBlackBy((byte)FastMath.Map(seg.Speed, 0, 255, 248, 16));
        seg.Move(seg.Aux0, 1);

        for (int i = 0; i < 8; i++)
        {
            int x = Beat.Sin8((uint)(12 + i), 2, (byte)(cols - 3));
            int y = Beat.Sin8((uint)(15 + i), 2, (byte)(rows - 3));
            Rgbw color = ColorUtil.ColorFromPalette(seg.CurrentPalette, Beat.Sin8((uint)(12 + i), 0, 255));
            seg.AddPixelColorXY(x, y, color);
            if (cols > 24 || rows > 24) // draw a plus sign rather than a dot on a large matrix
            {
                seg.AddPixelColorXY(x + 1, y, color);
                seg.AddPixelColorXY(x - 1, y, color);
                seg.AddPixelColorXY(x, y + 1, color);
                seg.AddPixelColorXY(x, y - 1, color);
            }
        }
        seg.Blur((byte)(seg.Intensity >> 3), seg.Check1);
    }
}
