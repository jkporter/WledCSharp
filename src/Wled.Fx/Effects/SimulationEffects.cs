namespace Wled.Fx;

/// <summary>
/// Effects that run a little simulation: fireworks, starbursts, moving shadows, cellular automata
/// and a fluid-like warp.
/// Port of the corresponding effects in <c>FX.cpp</c>.
/// </summary>
public static class SimulationEffects
{
    internal static void Register()
    {
        EffectRegistry.Register(EffectId.ExplodingFireworks, "Fireworks 1D@Gravity,Firing side;!,!;!;12;pal=11,ix=128", ExplodingFireworks);
        EffectRegistry.Register(EffectId.Starburst, "Fireworks Starburst@Chance,Fragments,,,,,Overlay;,!;!;;pal=11,m12=0", Starburst);
        EffectRegistry.Register(EffectId.DancingShadows, "Dancing Shadows@!,# of shadows;!;!", DancingShadows);
        EffectRegistry.Register(EffectId.TwoDGameOfLife, "Game Of Life@!,,Blur,,,,,Mutation;!,!;!;2;pal=11,sx=128", GameOfLife);
        EffectRegistry.Register(EffectId.TwoDSoap, "Soap@!,Smoothness,Density;;!;2;pal=11", Soap);
    }

    // ------------------------------------------------------------------ fireworks

    /// <summary>
    /// A shell that launches, arcs over and bursts into sparks. Works along a strip or up a matrix.
    /// </summary>
    /// <remarks>
    /// The first spark doubles as the flare, and its stored state also drives the little state
    /// machine in <see cref="Segment.Aux0"/>: 0-1 launching, 2-3 exploding, above that a countdown
    /// of idle frames before the next shell.
    /// </remarks>
    public static void ExplodingFireworks(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        int cols = seg.Is2D ? seg.Width : 1;
        int rows = seg.Is2D ? seg.Height : seg.Length;

        int numSparks = System.Math.Min(5 + ((rows * cols) >> 1), 255);
        // one spark past the end carries the decaying gravity, keeping all state on the segment
        MotionEffects.Spark[] sparks = seg.GetData<MotionEffects.Spark>(numSparks + 1);
        ref MotionEffects.Spark flare = ref sparks[0];
        ref float dyingGravity = ref sparks[numSparks].Velocity;

        if (seg.Aux1 != numSparks) // the segment was resized, so start a fresh shell
        {
            dyingGravity = 0.0f;
            seg.Aux0 = 0;
            seg.Aux1 = (ushort)numSparks;
        }

        seg.FadeOut(252);

        float gravity = -0.0004f - (seg.Speed / 800000.0f);
        gravity *= rows;

        if (seg.Aux0 < 2) // launching
        {
            if (seg.Aux0 == 0)
            {
                flare.Position = 0;
                // on a strip the intensity slider biases which end fires
                flare.PositionX = seg.Is2D ? Rng.Next16(2, cols - 3) : (seg.Intensity > Rng.Next8() ? 1 : 0);
                int peakHeight = 75 + Rng.Next8(180);
                peakHeight = peakHeight * (rows - 1) >> 8;
                flare.Velocity = MathF.Sqrt(-2.0f * gravity * peakHeight);
                flare.VelocityX = seg.Is2D ? (Rng.Next8(9) - 4) / 64.0f : 0;
                flare.Color = 255; // brightness
                seg.Aux0 = 1;
            }

            if (flare.Velocity > 12 * gravity)
            {
                var bright = (byte)flare.Color;
                Rgbw white = new(bright, bright, bright);
                if (seg.Is2D) seg.SetPixelColorXY((int)flare.PositionX, rows - (int)flare.Position - 1, white);
                else seg.SetPixelColor(flare.PositionX > 0.0f ? rows - (int)flare.Position - 1 : (int)flare.Position, white);

                flare.Position = System.Math.Clamp(flare.Position + flare.Velocity, 0, rows - 1);
                if (seg.Is2D) flare.PositionX = System.Math.Clamp(flare.PositionX + flare.VelocityX, 0, cols - 1);
                flare.Velocity += gravity;
                flare.Color -= 2;
            }
            else seg.Aux0 = 2; // at the top of the arc
        }
        else if (seg.Aux0 < 4) // exploding
        {
            // the burst is as big as the shell was high
            int nSparks = System.Math.Clamp((int)flare.Position + Rng.Next8(4), 4, numSparks);

            if (seg.Aux0 == 2)
            {
                for (int i = 1; i < nSparks; i++)
                {
                    sparks[i].Position = flare.Position;
                    sparks[i].PositionX = flare.PositionX;
                    sparks[i].Velocity = Rng.Next16(20001) / 10000.0f - 0.9f; // -0.9 to 1.1
                    if (rows < 32) sparks[i].Velocity *= 0.5f;                // calmer on a short strip
                    sparks[i].VelocityX = seg.Is2D ? Rng.Next16(20001) / 10000.0f - 1.0f : 0;
                    sparks[i].Color = 345; // set before scaling the velocity, so sparks start bright
                    sparks[i].ColorIndex = Rng.Next8();
                    sparks[i].Velocity *= flare.Position / rows;              // proportional to height
                    sparks[i].VelocityX *= seg.Is2D ? flare.PositionX / cols : 0;
                    sparks[i].Velocity *= -gravity * 50;
                }
                dyingGravity = gravity / 2;
                seg.Aux0 = 3;
            }

            if (sparks[1].Color > 4) // the first spark stands in for all of them
            {
                for (int i = 1; i < nSparks; i++)
                {
                    sparks[i].Position += sparks[i].Velocity;
                    sparks[i].PositionX += sparks[i].VelocityX;
                    sparks[i].Velocity += dyingGravity;
                    if (seg.Is2D) sparks[i].VelocityX += dyingGravity;
                    if (sparks[i].Color > 3) sparks[i].Color -= 4;

                    if (sparks[i].Position <= 0 || sparks[i].Position >= rows) continue;
                    if (seg.Is2D && !(sparks[i].PositionX >= 0 && sparks[i].PositionX < cols)) continue;

                    int progress = sparks[i].Color;
                    Rgbw sparkColor = seg.Palette != 0 ? seg.ColorWheel(sparks[i].ColorIndex) : seg.Color(0);
                    Rgbw c = Rgbw.Black;
                    if (progress > 300) c = Rgbw.Blend(sparkColor, Colors.White, (byte)((progress - 300) * 5));
                    else if (progress > 45)
                    {
                        c = Rgbw.Blend(Rgbw.Black, sparkColor, (byte)(progress - 45));
                        // cool towards red as the spark burns out
                        var cooling = (byte)((300 - progress) >> 5);
                        c = new Rgbw(c.R, FastMath.QSub8(c.G, cooling), FastMath.QSub8(c.B, (byte)(cooling * 2)), c.W);
                    }

                    if (seg.Is2D) seg.SetPixelColorXY((int)sparks[i].PositionX, rows - (int)sparks[i].Position - 1, c);
                    else seg.SetPixelColor((int)sparks[i].PositionX != 0 ? rows - (int)sparks[i].Position - 1 : (int)sparks[i].Position, c);
                }
                if (seg.Check3) seg.Blur(16);
                dyingGravity *= 0.8f; // burnt-out sparks fall more slowly
            }
            else seg.Aux0 = (ushort)(6 + Rng.Next8(10)); // idle for a few frames
        }
        else
        {
            seg.Aux0--;
            if (seg.Aux0 < 4) seg.Aux0 = 0; // back to the launch
        }
    }

    private const int StarburstMaxFragments = 10;

    /// <summary>One starburst: a colour, a birth time and the positions of its fragments.</summary>
    private struct Star
    {
        public Crgb Color;
        public uint Birth;
        public uint Last;
        public float Velocity;
        public int Position;
        public FragmentBuffer Fragments;

        /// <summary>Inline storage for the fragment positions, so a star stays one flat struct.</summary>
        [System.Runtime.CompilerServices.InlineArray(StarburstMaxFragments)]
        public struct FragmentBuffer
        {
            private float _first;
        }
    }

    /// <summary>Bursts that flash white, then fly apart symmetrically and fade.</summary>
    public static void Starburst(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        int numStars = 1 + (seg.Length >> 3);
        Star[] stars = seg.GetData<Star>(numStars);
        uint now = seg.Now;

        const float maxSpeed = 375.0f;
        const float ignitionTime = 250.0f;  // how long the white flash lasts
        const float fadeTime = 1500.0f;

        for (int j = 0; j < numStars; j++)
        {
            // the speed slider sets how likely a new burst is on any given frame
            if (Rng.Next8((uint)(144 - (seg.Speed >> 1))) != 0 || stars[j].Birth != 0) continue;

            int startPos = Rng.Next16((uint)(seg.Length - 1));
            float multiplier = Rng.Next8() / 255.0f;

            stars[j].Color = (Crgb)seg.ColorWheel(Rng.Next8());
            stars[j].Position = startPos;
            stars[j].Velocity = maxSpeed * (Rng.Next8() / 255.0f) * multiplier;
            stars[j].Birth = now;
            stars[j].Last = now;
            int fragments = Rng.Next8(3, (uint)(6 + (seg.Intensity >> 5))); // more fragments, bigger burst

            for (int i = 0; i < StarburstMaxFragments; i++)
                stars[j].Fragments[i] = i < fragments ? startPos : -1;
        }

        if (!seg.Check2) seg.Fill(seg.Color(1));

        for (int j = 0; j < numStars; j++)
        {
            if (stars[j].Birth != 0)
            {
                float dt = (now - stars[j].Last) / 1000.0f;
                for (int i = 0; i < StarburstMaxFragments; i++)
                {
                    int spread = i >> 1;
                    // fragments all travel one way and are mirrored when drawn
                    if (stars[j].Fragments[i] > 0)
                        stars[j].Fragments[i] += stars[j].Velocity * dt * (spread / 3.0f);
                }
                stars[j].Last = now;
                stars[j].Velocity -= 3 * stars[j].Velocity * dt;
            }

            Rgbw c = stars[j].Color;
            float fade = 0.0f;
            float age = now - stars[j].Birth;

            if (age < ignitionTime) c = Rgbw.Blend(Colors.White, c, (byte)(254.5f * (age / ignitionTime)));
            else if (age > ignitionTime + fadeTime)
            {
                fade = 1.0f; // burnt out
                stars[j].Birth = 0;
                c = seg.Color(1);
            }
            else
            {
                age -= ignitionTime;
                fade = age / fadeTime;
                c = Rgbw.Blend(c, seg.Color(1), (byte)(254.5f * fade));
            }

            float particleSize = (1.0f - fade) * 2.0f;

            for (int index = 0; index < StarburstMaxFragments * 2; index++)
            {
                bool mirrored = (index & 0x1) != 0;
                int i = index >> 1;
                if (stars[j].Fragments[i] <= 0) continue;

                float loc = stars[j].Fragments[i];
                if (mirrored) loc -= (loc - stars[j].Position) * 2;
                int start = System.Math.Max((int)(loc - particleSize), 0);
                int end = (int)(loc + particleSize);
                if (start == end) end++;
                if (end > seg.Length) end = seg.Length;
                for (int p = start; p < end; p++) seg.SetPixelColor(p, c);
            }
        }
    }

    // -------------------------------------------------------------- moving shadows

    private const int SpotTypeCount = 6;
    private const int SpotMaxCount = 49;

    /// <summary>One spotlight sliding along the segment.</summary>
    private struct Spotlight
    {
        public float Speed;
        public byte ColorIndex;
        public int Position;
        public uint LastUpdateTime;
        public byte Width;
        public byte Type;
    }

    /// <summary>
    /// Spotlights of assorted shapes sliding past each other. Shine it through leaves or a lattice
    /// and the shadows dance.
    /// </summary>
    public static void DancingShadows(Segment seg)
    {
        if (seg.Length <= 1) { BasicEffects.Static(seg); return; }

        int numSpotlights = FastMath.Map(seg.Intensity, 0, 255, 2, SpotMaxCount);
        bool initialize = seg.Aux0 != numSpotlights;
        seg.Aux0 = (ushort)numSpotlights;

        Spotlight[] spotlights = seg.GetData<Spotlight>(numSpotlights);
        seg.Fill(Rgbw.Black);

        uint time = seg.Now;

        for (int i = 0; i < numSpotlights; i++)
        {
            bool respawn = false;
            if (!initialize)
            {
                var delta = (int)((time - spotlights[i].LastUpdateTime) * (spotlights[i].Speed * ((1.0f + seg.Speed) / 100.0f)));
                if (System.Math.Abs(delta) >= 1)
                {
                    spotlights[i].Position += delta;
                    spotlights[i].LastUpdateTime = time;
                }

                respawn = (spotlights[i].Speed > 0.0f && spotlights[i].Position > seg.Length + 2)
                          || (spotlights[i].Speed < 0.0f && spotlights[i].Position < -(spotlights[i].Width + 2));
            }

            if (initialize || respawn)
            {
                spotlights[i].ColorIndex = Rng.Next8();
                spotlights[i].Width = Rng.Next8(1, 10);
                spotlights[i].Speed = 1.0f / Rng.Next8(4, 50);

                if (initialize)
                {
                    spotlights[i].Position = Rng.Next16((uint)seg.Length);
                    spotlights[i].Speed *= Rng.Next8(2) != 0 ? 1.0f : -1.0f;
                }
                else if (Rng.Next8(2) != 0) // come back in from whichever end
                {
                    spotlights[i].Position = seg.Length + spotlights[i].Width;
                    spotlights[i].Speed *= -1.0f;
                }
                else spotlights[i].Position = -spotlights[i].Width;

                spotlights[i].LastUpdateTime = time;
                spotlights[i].Type = Rng.Next8(SpotTypeCount);
            }

            Rgbw color = seg.ColorFromPalette(spotlights[i].ColorIndex, false, false, 255);
            int start = spotlights[i].Position;
            int width = spotlights[i].Width;

            if (width <= 1)
            {
                if (start >= 0 && start < seg.Length) seg.BlendPixelColor(start, color, 128);
                continue;
            }

            switch (spotlights[i].Type)
            {
                case 0: // solid
                    for (int j = 0; j < width; j++) BlendIfVisible(seg, start + j, color, 128);
                    break;
                case 1: // gradient
                    for (int j = 0; j < width; j++)
                        BlendIfVisible(seg, start + j, color, FastMath.CubicWave8((byte)FastMath.Map(j, 0, width - 1, 0, 255)));
                    break;
                case 2: // two gradients
                    for (int j = 0; j < width; j++)
                        BlendIfVisible(seg, start + j, color, FastMath.CubicWave8((byte)(2 * FastMath.Map(j, 0, width - 1, 0, 255))));
                    break;
                case 3: // every second pixel
                    for (int j = 0; j < width; j += 2) BlendIfVisible(seg, start + j, color, 128);
                    break;
                case 4: // every third
                    for (int j = 0; j < width; j += 3) BlendIfVisible(seg, start + j, color, 128);
                    break;
                default: // every fourth
                    for (int j = 0; j < width; j += 4) BlendIfVisible(seg, start + j, color, 128);
                    break;
            }
        }

        static void BlendIfVisible(Segment seg, int index, Rgbw color, byte blend)
        {
            if (index >= 0 && index < seg.Length) seg.BlendPixelColor(index, color, blend);
        }
    }

    // ------------------------------------------------------------ cellular automata

    /// <summary>One cell of the Game of Life grid.</summary>
    private struct Cell
    {
        public bool Alive;
        public bool Faded;
        public bool Toggle;
        public bool EdgeCell;
        public bool OscillatorCheck;
        public bool SpaceshipCheck;
    }

    /// <summary>
    /// Conway's Game of Life, with cells taking their colour from a living neighbour.
    /// </summary>
    /// <remarks>
    /// The board restarts when it dies out or settles into a repeat. Oscillators are caught by
    /// comparing the grid with its state 16 generations ago, and gliders by comparing it after the
    /// number of generations it takes one to cross the board and return.
    /// </remarks>
    public static void GameOfLife(Segment seg)
    {
        if (!seg.Is2D) { BasicEffects.Static(seg); return; }

        int cols = seg.Width, rows = seg.Height;
        int maxIndex = cols * rows;
        Cell[] cells = seg.GetData<Cell>(maxIndex);

        bool mutate = seg.Check3;
        var blur = (byte)FastMath.Map(seg.Custom1, 0, 255, 255, 4);
        Rgbw bgColor = seg.Color(1);
        Rgbw birthColor = seg.ColorFromPalette(128, false, seg.PaletteSolidWrap, 255);

        bool setup = seg.Call == 0;
        if (setup)
        {
            // a glider returns to its starting cell after lcm(rows, cols) * 4 generations
            int a = rows, b = cols;
            while (b != 0) { int t = b; b = a % b; a = t; }
            seg.Aux1 = (ushort)((cols * rows / System.Math.Max(a, 1)) << 2);
        }

        if (System.Math.Abs((long)seg.Now - seg.Step) > 2000) seg.Step = 0; // the time base jumped
        bool paused = seg.Step > seg.Now;

        if ((!paused && seg.Aux0 == 0) || setup)
        {
            seg.Step = seg.Now + 1280; // hold the starting position for a moment
            seg.Aux0 = 1;
            paused = true;
            Array.Clear(cells);

            for (int i = 0; i < maxIndex; i++)
            {
                bool isAlive = Rng.Next8(3) == 0; // roughly a third of the board
                cells[i].Alive = isAlive;
                cells[i].Faded = !isAlive;
                int x = i % cols, y = i / cols;
                cells[i].EdgeCell = x == 0 || x == cols - 1 || y == 0 || y == rows - 1;
                seg.SetPixelColorXY(x, y, isAlive
                    ? seg.ColorFromPalette(Rng.Next8(), false, seg.PaletteSolidWrap, 0)
                    : bgColor);
            }
        }

        if (paused || seg.Now - seg.Step < 1000 / (uint)FastMath.Map(seg.Speed, 0, 255, 1, 42))
        {
            // between generations, keep fading the dead cells so a blur cannot build up
            for (int i = maxIndex; i-- > 0;)
            {
                if (cells[i].Alive) continue;
                Rgbw cellColor = seg.GetPixelColorXY(i % cols, i / cols);
                if (cellColor == bgColor) continue;
                if (cells[i].Faded) seg.SetPixelColorXY(i % cols, i / cols, bgColor);
                else
                {
                    Rgbw blended = Rgbw.Blend(cellColor, bgColor, 2);
                    if (blended == cellColor) { blended = bgColor; cells[i].Faded = true; }
                    seg.SetPixelColorXY(i % cols, i / cols, blended);
                }
            }
            return;
        }

        bool updateOscillator = seg.Aux0 % 16 == 0;
        bool updateSpaceship = seg.Aux1 != 0 && seg.Aux0 % seg.Aux1 == 0;
        bool repeatingOscillator = true, repeatingSpaceship = true, emptyGrid = true;

        Span<int> parents = stackalloc int[3];
        int index = maxIndex - 1;
        for (int y = rows; y-- > 0;)
        {
            for (int x = cols; x-- > 0; index--)
            {
                ref Cell cell = ref cells[index];

                if (cell.Alive) emptyGrid = false;
                if (cell.OscillatorCheck != cell.Alive) repeatingOscillator = false;
                if (cell.SpaceshipCheck != cell.Alive) repeatingSpaceship = false;
                if (updateOscillator) cell.OscillatorCheck = cell.Alive;
                if (updateSpaceship) cell.SpaceshipCheck = cell.Alive;

                int neighbours = 0, aliveParents = 0;
                for (int i = -1; i <= 1; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        if (i == 0 && j == 0) continue;
                        int nX = x + j, nY = y + i;
                        if (cell.EdgeCell) // only the border wraps, which keeps the inner loop cheap
                        {
                            nX = (nX + cols) % cols;
                            nY = (nY + rows) % rows;
                        }
                        if (nX < 0 || nX >= cols || nY < 0 || nY >= rows) continue;
                        int nIndex = nX + nY * cols;
                        if (!cells[nIndex].Alive) continue;
                        neighbours++;
                        if (!cells[nIndex].Toggle && neighbours < 4 && aliveParents < 3)
                            parents[aliveParents++] = nIndex;
                    }
                }

                if (cell.Alive && (neighbours < 2 || neighbours > 3)) // loneliness or overcrowding
                {
                    cell.Toggle = true;
                    if (blur == 255) cell.Faded = true;
                    seg.SetPixelColorXY(x, y, cell.Faded
                        ? bgColor
                        : Rgbw.Blend(seg.GetPixelColorXY(x, y), bgColor, blur));
                }
                else if (!cell.Alive)
                {
                    // with mutation on, three neighbours occasionally fail and two occasionally breed
                    byte mutationRoll = mutate ? Rng.Next8(128) : (byte)1;
                    if ((neighbours == 3 && mutationRoll != 0) || (mutate && neighbours == 2 && mutationRoll == 0))
                    {
                        cell.Toggle = true;
                        cell.Faded = false;
                        Rgbw color = birthColor;
                        if (aliveParents > 0)
                        {
                            int parentIndex = parents[Rng.Next8((uint)aliveParents)];
                            color = seg.GetPixelColorXY(parentIndex % cols, parentIndex / cols);
                        }
                        seg.SetPixelColorXY(x, y, color);
                    }
                    else if (!cell.Faded)
                    {
                        Rgbw cellColor = seg.GetPixelColorXY(x, y);
                        Rgbw blended = Rgbw.Blend(cellColor, bgColor, blur);
                        if (blended == cellColor) { blended = bgColor; cell.Faded = true; }
                        seg.SetPixelColorXY(x, y, blended);
                    }
                }
            }
        }

        for (int i = maxIndex; i-- > 0;)
        {
            cells[i].Alive ^= cells[i].Toggle;
            cells[i].Toggle = false;
        }

        if (repeatingOscillator || repeatingSpaceship || emptyGrid)
        {
            seg.Aux0 = 0;      // start a new board on the next frame
            seg.Step += 1024;  // after holding the final generation for a second
        }
        else
        {
            seg.Aux0++;
            seg.Step = seg.Now;
        }
    }

    // ------------------------------------------------------------------------ soap

    /// <summary>One cell of the soap field: the noise value driving it and the colour it carries.</summary>
    private struct SoapCell
    {
        public byte Noise;
        public Crgb Pixel;
    }

    /// <summary>The soap field: cells plus the drifting coordinates of the noise volume.</summary>
    private sealed class SoapState
    {
        public SoapCell[] Cells = [];
        public uint[] NoiseCoordinates = new uint[3];
    }

    /// <summary>
    /// Colour smeared around by a drifting noise field, like the swirl on a soap bubble.
    /// </summary>
    /// <remarks>
    /// Each row is displaced along X and each column along Y by the local noise value, with the
    /// fractional part of the displacement resolved by blending the two pixels it falls between.
    /// </remarks>
    public static void Soap(Segment seg)
    {
        if (!seg.Is2D) { BasicEffects.Static(seg); return; }

        int cols = seg.Width, rows = seg.Height;
        SoapState state = seg.GetObjects(1, () => new SoapState())[0];
        if (state.Cells.Length != cols * rows) state.Cells = new SoapCell[cols * rows];
        SoapCell[] cells = state.Cells;
        uint[] noiseCoordinates = state.NoiseCoordinates;

        uint scaleX = 160000u / (uint)cols;
        uint scaleY = 160000u / (uint)rows;
        uint movement = (uint)(System.Math.Min(cols, rows) * (seg.Speed + 2) / 2);
        var smoothness = (byte)System.Math.Min(250, (int)seg.Intensity); // above 250 almost nothing moves

        if (seg.Call == 0) for (int i = 0; i < 3; i++) noiseCoordinates[i] = Rng.Next();
        else for (int i = 0; i < 3; i++) noiseCoordinates[i] += movement;

        for (int i = 0; i < cols; i++)
        {
            uint iOffset = scaleX * (uint)(i - cols / 2);
            for (int j = 0; j < rows; j++)
            {
                uint jOffset = scaleY * (uint)(j - rows / 2);
                var data = (byte)(Perlin.Noise16(noiseCoordinates[0] + iOffset, noiseCoordinates[1] + jOffset, noiseCoordinates[2]) >> 8);
                // ease towards the new noise value rather than snapping, which is what smooths it
                cells[i + j * cols].Noise = (byte)(FastMath.Scale8(cells[i + j * cols].Noise, smoothness)
                                                   + FastMath.Scale8(data, (byte)(255 - smoothness)));
            }
        }

        if (seg.Call == 0 || seg.Aux0 != cols || seg.Aux1 != rows)
        {
            seg.Aux0 = (ushort)cols;
            seg.Aux1 = (ushort)rows;
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                    seg.SetPixelColorXY(i, j, ColorUtil.ColorFromPalette(seg.CurrentPalette, (byte)~cells[i + j * cols].Noise * 3));
        }

        SoapDisplace(seg, cells, isRow: true);
        SoapDisplace(seg, cells, isRow: false);
    }

    /// <summary>Displaces every row (or column) of the field by its local noise value.</summary>
    private static void SoapDisplace(Segment seg, SoapCell[] cells, bool isRow)
    {
        int cols = seg.Width, rows = seg.Height;
        int outer = isRow ? rows : cols;
        int inner = isRow ? cols : rows;
        int amplitude = System.Math.Max(1, (inner - 8) >> 3) * (1 + (seg.Custom1 >> 5));

        var line = new Crgb[inner];

        for (int i = 0; i < outer; i++)
        {
            int amount = (cells[isRow ? i * cols : i].Noise - 128) * amplitude;
            int delta = System.Math.Abs(amount) >> 8;
            var fraction = (byte)(System.Math.Abs(amount) & 255);

            for (int j = 0; j < inner; j++)
            {
                int zD, zF;
                if (amount < 0) { zD = j - delta; zF = zD - 1; }
                else { zD = j + delta; zF = zD + 1; }

                int yA = System.Math.Abs(zD) % inner;
                int yB = System.Math.Abs(zF) % inner;
                int xA = i, xB = i;
                if (isRow) { (xA, yA) = (yA, xA); (xB, yB) = (yB, xB); }

                int indexA = xA + yA * cols;
                int indexB = xB + yB * cols;
                // sampling off the end of the line falls back to the noise value itself
                Crgb pixelA = zD >= 0 && zD < inner ? cells[indexA].Pixel : (Crgb)ColorUtil.ColorFromPalette(seg.CurrentPalette, (byte)~cells[indexA].Noise * 3);
                Crgb pixelB = zF >= 0 && zF < inner ? cells[indexB].Pixel : (Crgb)ColorUtil.ColorFromPalette(seg.CurrentPalette, (byte)~cells[indexB].Noise * 3);
                line[j] = pixelA.Scale8(FastMath.Ease8InOutCubic((byte)(255 - fraction)))
                          + pixelB.Scale8(FastMath.Ease8InOutCubic(fraction));
            }

            for (int j = 0; j < inner; j++)
            {
                int x = isRow ? j : i;
                int y = isRow ? i : j;
                cells[x + y * cols].Pixel = line[j];
                seg.SetPixelColorXY(x, y, line[j]);
            }
        }
    }
}
