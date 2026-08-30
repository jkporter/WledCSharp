namespace Wled.Fx;

/// <summary>
/// The drawing surface half of <see cref="Segment"/>: pixel access, the 1D-onto-2D mappings and the
/// fade / blur / shape helpers effects are built from.
/// Port of the drawing members of <c>FX_fcn.cpp</c> and <c>FX_2Dfcn.cpp</c>.
/// </summary>
public sealed partial class Segment
{
    // Pinwheel mapping works in 14-bit fixed point so rays can be traced without floating point.
    private const int FixedScale = 16384;

    private readonly int[] _previousRays = [int.MaxValue, int.MaxValue];

    /// <summary>Encodes a virtual-strip number into a pixel index for <see cref="SetPixelColor"/>.</summary>
    /// <remarks>
    /// A 1D effect running in <see cref="Mapping1D2D.Bar"/> mode can address each column separately
    /// by tagging the strip number into the upper 16 bits of the index, which is what the C++
    /// <c>indexToVStrip()</c> macro does.
    /// </remarks>
    public static int IndexToVStrip(int index, int stripNumber) => index | ((stripNumber + 1) << 16);

    // ------------------------------------------------------------- raw pixel access

    /// <summary>Reads straight out of the pixel buffer with no mapping or bounds checks.</summary>
    public Rgbw GetPixelColorRaw(int i) => _pixels[i];

    /// <summary>Writes straight into the pixel buffer with no mapping or bounds checks.</summary>
    public void SetPixelColorRaw(int i, Rgbw color) => _pixels[i] = color;

    /// <summary>Reads a 2D pixel straight out of the buffer with no bounds checks.</summary>
    public Rgbw GetPixelColorXYRaw(int x, int y) => _pixels[x + y * Width];

    /// <summary>Writes a 2D pixel straight into the buffer with no bounds checks.</summary>
    public void SetPixelColorXYRaw(int x, int y, Rgbw color) => _pixels[x + y * Width] = color;

    /// <summary>Writes a pixel by physical index, ignoring every mapping.</summary>
    public void SetRawPixelColor(int i, Rgbw color)
    {
        if (i >= 0 && i < _pixels.Length) _pixels[i] = color;
    }

    /// <summary>The whole pixel buffer, for the engine to blend onto the strip.</summary>
    internal Rgbw[] PixelBuffer => _pixels;

    // ------------------------------------------------------------------- pinwheel

    /// <summary>Number of rays a pinwheel mapping uses; always a multiple of 8 to avoid overdraw.</summary>
    private static int PinwheelLength(int vW, int vH) => (System.Math.Max(vW, vH) + 15) & ~7;

    private static void SetPinwheelParameters(int i, int vW, int vH, out int startX, out int startY,
                                              Span<int> cosVal, Span<int> sinVal, bool forRead = false)
    {
        int steps = PinwheelLength(vW, vH);
        int baseAngle = (0xFFFF + steps / 2) / steps; // 360 degrees over all rays, in 16-bit scale
        int rotate = forRead ? baseAngle / 2 : 0;     // read from the middle of the ray, not its edge
        for (int k = 0; k < 2; k++) // two consecutive rays bound the wedge we fill
        {
            var angle = (ushort)((i + k) * baseAngle + rotate);
            cosVal[k] = (FastMath.Cos16(angle) * FixedScale) >> 15;
            sinVal[k] = (FastMath.Sin16(angle) * FixedScale) >> 15;
        }
        startX = vW * FixedScale / 2;
        startY = vH * FixedScale / 2;
    }

    // -------------------------------------------------------------------- clipping

    /// <summary>
    /// Whether a 1D pixel falls outside the clipping window a non-fade transition is currently
    /// wiping across the segment.
    /// </summary>
    public bool IsPixelClipped(int i)
    {
        if (Strip is not { } strip || strip.BlendingStyle == TransitionStyle.Fade || !IsInTransition) return false;
        if (strip.ClipStart == strip.ClipStop) return false;

        bool invert = strip.ClipStart > strip.ClipStop;
        int start = invert ? strip.ClipStop : strip.ClipStart;
        int stop = invert ? strip.ClipStart : strip.ClipStop;

        if (strip.BlendingStyle == TransitionStyle.FairyDust)
        {
            int len = stop - start;
            if (len < 2) return false;
            uint shuffled = FastMath.HashInt((uint)i) % (uint)len;
            uint pos = shuffled * 0xFFFF / (uint)len;
            return Progress <= pos;
        }

        bool inside = i >= start && i < stop;
        return !inside ^ invert;
    }

    /// <summary>Whether a 2D pixel falls outside the clipping window of a non-fade transition.</summary>
    public bool IsPixelXYClipped(int x, int y)
    {
        if (Strip is not { } strip || strip.BlendingStyle == TransitionStyle.Fade || !IsInTransition) return false;
        if (strip.ClipStart == strip.ClipStop) return false;

        bool invertX = strip.ClipStart > strip.ClipStop;
        bool invertY = strip.ClipStartY > strip.ClipStopY;
        int startX = invertX ? strip.ClipStop : strip.ClipStart;
        int stopX = invertX ? strip.ClipStart : strip.ClipStop;
        int startY = invertY ? strip.ClipStopY : strip.ClipStartY;
        int stopY = invertY ? strip.ClipStartY : strip.ClipStopY;

        if (strip.BlendingStyle == TransitionStyle.FairyDust)
        {
            int width = stopX - startX;
            int len = width * (stopY - startY);
            if (len < 2) return false;
            uint shuffled = FastMath.HashInt((uint)(x + y * width)) % (uint)len;
            uint pos = shuffled * 0xFFFF / (uint)len;
            return Progress <= pos;
        }

        if (strip.BlendingStyle is TransitionStyle.CircularIn or TransitionStyle.CircularOut)
        {
            int cx = (stopX - startX + 1) / 2;
            int cy = (stopY - startY + 1) / 2;
            bool outward = strip.BlendingStyle == TransitionStyle.CircularOut;
            int prog = outward ? Progress : 0xFFFF - Progress;
            int radius = System.Math.Max(cx, cy) * prog / 0xFFFF;
            int radiusSquared = 2 * radius * radius;
            if (radiusSquared == 0) return outward;
            int dx = x - cx, dy = y - cy;
            bool outside = dx * dx + dy * dy > radiusSquared;
            return outward ? outside : !outside;
        }

        bool xInside = x >= startX && x < stopX;
        if (invertX) xInside = !xInside;
        bool yInside = y >= startY && y < stopY;
        if (invertY) yInside = !yInside;
        bool clip = strip.BlendingStyle == TransitionStyle.OutsideIn ? xInside || yInside : xInside && yInside;
        return !clip;
    }

    // ------------------------------------------------------------ 1D pixel access

    /// <summary>
    /// Paints one virtual pixel. On a 2D segment the index is expanded according to
    /// <see cref="Map1D2D"/>, so a 1D effect can drive a matrix without knowing about it.
    /// </summary>
    public void SetPixelColor(int i, Rgbw color)
    {
        if (!IsActive || i < 0) return;

        int vStrip = 0;
        int vLength = Length;
        if (i >= vLength)
        {
            vStrip = i >> 16; // the caller tagged a virtual strip into the upper bits
            i &= 0xFFFF;
            if (i >= vLength) return;
        }

        if (Is2D)
        {
            SetPixelColorMapped(i, vStrip, color);
            return;
        }

        if (MatrixHeight != 1 && (PhysicalWidth == 1 || PhysicalHeight == 1)
            && Start < MatrixWidth * MatrixHeight)
        {
            // a one-pixel-wide row or column of a matrix; note that the virtual axes may be transposed
            int x = Width > 1 ? i : 0;
            int y = Height > 1 ? i : 0;
            SetPixelColorXY(x, y, color);
            return;
        }

        SetPixelColorRaw(i, color);
    }

    private void SetPixelColorMapped(int i, int vStrip, Rgbw color)
    {
        int vW = Width, vH = Height;

        switch (Map1D2D)
        {
            case Mapping1D2D.Pixels:
                SetPixelColorXYRaw(i % vW, i / vW, color);
                break;

            case Mapping1D2D.Bar:
                if (vStrip > 0) SetPixelColorXYRaw(vStrip - 1, vH - i - 1, color);
                else for (int x = 0; x < vW; x++) SetPixelColorXYRaw(x, vH - i - 1, color);
                break;

            case Mapping1D2D.Arc:
                if (i == 0) SetPixelColorXYRaw(0, 0, color);
                else
                {
                    float r = i;
                    float step = FastMath.HalfPi / (2.8284f * r + 4); // (PI/4)/(r/sqrt(2)+1) steps suffice
                    for (float rad = 0.0f; rad <= FastMath.HalfPi / 2 + step / 2; rad += step)
                    {
                        var x = (int)MathF.Round(FastMath.Sin(rad) * r);
                        var y = (int)MathF.Round(FastMath.Cos(rad) * r);
                        SetPixelColorXY(x, y, color); // exploit the symmetry of the octant
                        SetPixelColorXY(y, x, color);
                    }
                }
                break;

            case Mapping1D2D.Corner:
                for (int x = 0; x <= i; x++) SetPixelColorXY(x, i, color);
                for (int y = 0; y < i; y++) SetPixelColorXY(i, y, color);
                break;

            case Mapping1D2D.Pinwheel:
                SetPixelColorPinwheel(i, vW, vH, color);
                break;
        }
    }

    /// <summary>
    /// Draws ray <paramref name="i"/> of a pinwheel by tracing the two lines that bound it with
    /// Bresenham and filling the wedge between them.
    /// </summary>
    private void SetPixelColorPinwheel(int i, int vW, int vH, Rgbw color)
    {
        Span<int> cosVal = stackalloc int[2];
        Span<int> sinVal = stackalloc int[2];
        SetPinwheelParameters(i, vW, vH, out int startX, out int startY, cosVal, sinVal);

        int maxLineLength = System.Math.Max(vW, vH) + 2;
        Span<int> line0 = stackalloc int[maxLineLength * 2];
        Span<int> line1 = stackalloc int[maxLineLength * 2];
        Span<int> lineLength = stackalloc int[2];
        int closestEdgeIdx = int.MaxValue;

        for (int lineNr = 0; lineNr < 2; lineNr++)
        {
            Span<int> coordinates = lineNr == 0 ? line0 : line1;
            int x0 = startX, y0 = startY;
            int x1 = startX + (cosVal[lineNr] << 9); // deliberately off the grid
            int y1 = startY + (sinVal[lineNr] << 9);
            int dx = System.Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -System.Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            x0 /= FixedScale;
            y0 /= FixedScale;

            int idx = 0;
            int err = dx + dy;
            while (true)
            {
                if ((uint)x0 >= (uint)vW || (uint)y0 >= (uint)vH)
                {
                    closestEdgeIdx = System.Math.Min(closestEdgeIdx, idx - 2);
                    break;
                }
                if (idx + 1 >= coordinates.Length) break;
                coordinates[idx++] = x0;
                coordinates[idx++] = y0;
                lineLength[lineNr]++;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        if (lineLength[0] == 0 || lineLength[1] == 0) return;

        // pad the shorter line so the wedge can be filled row by row
        int diff = lineLength[0] - lineLength[1];
        int longLine = diff > 0 ? 0 : 1;
        int shortLine = longLine == 0 ? 1 : 0;
        if (diff != 0)
        {
            Span<int> shortCoords = shortLine == 0 ? line0 : line1;
            Span<int> longCoords = longLine == 0 ? line0 : line1;
            int idx = (lineLength[shortLine] - 1) * 2;
            int lastX = shortCoords[idx++];
            int lastY = shortCoords[idx++];
            bool keepX = lastX == 0 || lastX == vW - 1;
            for (int d = 0; d < System.Math.Abs(diff) && idx + 1 < shortCoords.Length; d++)
            {
                shortCoords[idx] = keepX ? lastX : longCoords[idx];
                idx++;
                shortCoords[idx] = keepX ? longCoords[idx] : lastY;
                idx++;
            }
        }

        closestEdgeIdx += 2;
        int maxRay = PinwheelLength(vW, vH) - 1;
        // skip the shared edge when the neighbouring ray was drawn in the same frame, else it doubles up
        bool drawFirst = !(_previousRays[0] == i - 1 || (i == 0 && _previousRays[0] == maxRay));
        bool drawLast = !(_previousRays[0] == i + 1 || (i == maxRay && _previousRays[0] == 0));

        for (int idx = 0; idx < lineLength[longLine] * 2;)
        {
            int x1 = line0[idx];
            int x2 = line1[idx++];
            int y1 = line0[idx];
            int y2 = line1[idx++];
            (int minX, int maxX) = x1 < x2 ? (x1, x2) : (x2, x1);
            (int minY, int maxY) = y1 < y2 ? (y1, y2) : (y2, y1);

            bool alwaysDraw = (drawFirst && drawLast)  // no adjacent ray, so nothing can double up
                              || idx > closestEdgeIdx  // edge pixels of uneven lines are always drawn
                              || (i == 0 && idx == 2)  // the centre pixel
                              || i == _previousRays[1]; // the effect drew twice in one frame

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    bool onLine1 = x == x1 && y == y1;
                    bool onLine2 = x == x2 && y == y2;
                    if (alwaysDraw
                        || (!onLine1 && (!onLine2 || drawLast))
                        || (!onLine2 && (!onLine1 || drawFirst)))
                        SetPixelColorXY(x, y, color);
                }
            }
        }
        _previousRays[1] = _previousRays[0];
        _previousRays[0] = i;
    }

    /// <summary>Reads back a virtual pixel, undoing whatever <see cref="Map1D2D"/> mapping applies.</summary>
    public Rgbw GetPixelColor(int i)
    {
        if (!IsActive || i < 0) return Rgbw.Black;

        int vStrip = i >> 16;
        i &= 0xFFFF;
        if (i >= Length) return Rgbw.Black;
        if (!Is2D) return GetPixelColorRaw(i);

        int vW = Width, vH = Height;
        int x = 0, y = 0;
        switch (Map1D2D)
        {
            case Mapping1D2D.Pixels:
                x = i % vW;
                y = i / vW;
                break;
            case Mapping1D2D.Bar:
                if (vStrip > 0) x = vStrip - 1;
                y = vH - i - 1;
                break;
            case Mapping1D2D.Arc when i > vW && i > vH:
                x = y = (int)FastMath.Sqrt32((uint)(i * i / 2)); // out on the diagonal
                break;
            case Mapping1D2D.Arc:
            case Mapping1D2D.Corner:
                if (vW > vH) x = i;
                else y = i;
                break;
            case Mapping1D2D.Pinwheel:
            {
                // approximate: walk the ray out from the centre and report the pixel where it leaves
                Span<int> cosVal = stackalloc int[2];
                Span<int> sinVal = stackalloc int[2];
                SetPinwheelParameters(i, vW, vH, out x, out y, cosVal, sinVal, forRead: true);
                int maxX = (vW - 1) * FixedScale;
                int maxY = (vH - 1) * FixedScale;
                while (x < maxX && y < maxY && x > FixedScale && y > FixedScale)
                {
                    x += cosVal[0];
                    y += sinVal[0];
                }
                x /= FixedScale;
                y /= FixedScale;
                break;
            }
        }
        return GetPixelColorXY(x, y);
    }

    // ------------------------------------------------------------ 2D pixel access

    /// <summary>Paints a pixel at 2D virtual coordinates; out-of-range coordinates are ignored.</summary>
    public void SetPixelColorXY(int x, int y, Rgbw color)
    {
        if (!IsActive) return;
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
        SetPixelColorXYRaw(x, y, color);
    }

    /// <summary>Reads a pixel at 2D virtual coordinates; out-of-range coordinates read as black.</summary>
    public Rgbw GetPixelColorXY(int x, int y)
    {
        if (!IsActive) return Rgbw.Black;
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return Rgbw.Black;
        return GetPixelColorXYRaw(x, y);
    }

    // ------------------------------------------------------------- pixel compositing

    /// <summary>Blends <paramref name="color"/> over the pixel already there.</summary>
    public void BlendPixelColor(int n, Rgbw color, byte blend)
        => SetPixelColor(n, Rgbw.Blend(GetPixelColor(n), color, blend));

    /// <summary>Adds <paramref name="color"/> to the pixel already there.</summary>
    public void AddPixelColor(int n, Rgbw color, bool preserveRatio = true)
        => SetPixelColor(n, GetPixelColor(n).Add(color, preserveRatio));

    /// <summary>Fades one pixel towards black.</summary>
    public void FadePixelColor(int n, byte fade)
        => SetPixelColor(n, GetPixelColor(n).Fade(fade, video: true));

    /// <summary>Blends <paramref name="color"/> over the pixel already there.</summary>
    public void BlendPixelColorXY(int x, int y, Rgbw color, byte blend)
        => SetPixelColorXY(x, y, Rgbw.Blend(GetPixelColorXY(x, y), color, blend));

    /// <summary>Adds <paramref name="color"/> to the pixel already there.</summary>
    public void AddPixelColorXY(int x, int y, Rgbw color, bool preserveRatio = true)
        => SetPixelColorXY(x, y, GetPixelColorXY(x, y).Add(color, preserveRatio));

    /// <summary>Fades one pixel towards black.</summary>
    public void FadePixelColorXY(int x, int y, byte fade)
        => SetPixelColorXY(x, y, GetPixelColorXY(x, y).Fade(fade, video: true));

    // ------------------------------------------------------------------ whole-segment

    /// <summary>Fills every pixel with one colour.</summary>
    public void Fill(Rgbw color)
    {
        if (!IsActive) return;
        // always fill the whole buffer: grouping, spacing and clipping are applied when blending out
        for (int i = 0; i < _pixels.Length; i++) _pixels[i] = color;
    }

    /// <summary>Blanks the segment.</summary>
    public void Clear() => Fill(Rgbw.Black);

    /// <summary>
    /// Fades every pixel towards the secondary colour. Higher rates fade faster; the result is
    /// frame-rate dependent by design, matching the firmware.
    /// </summary>
    public void FadeOut(byte rate)
    {
        if (!IsActive) return;
        rate = (byte)((256 - rate) >> 1);
        int mappedRate = 256 / (rate + 1);
        int length = RawLength;
        Rgbw background = Colors[1];

        for (int j = 0; j < length; j++)
        {
            uint color = GetPixelColorRaw(j).Value;
            if (color == background.Value) continue;
            for (int i = 0; i < 32; i += 8)
            {
                var c2 = (byte)(background.Value >> i);
                var c1 = (byte)(color >> i);
                int delta = (c2 - c1) * mappedRate / 256;
                // guarantee progress of at least one step, otherwise rounding can stall the fade
                if (delta == 0) delta = c2 == c1 ? 0 : c2 > c1 ? 1 : -1;
                color &= ~(0xFFu << i);
                color |= (uint)((c1 + delta) & 0xFF) << i;
            }
            SetPixelColorRaw(j, color);
        }
    }

    /// <summary>Blends every pixel towards the secondary colour.</summary>
    public void FadeToSecondaryBy(byte fadeBy)
    {
        if (!IsActive || fadeBy == 0) return;
        int length = RawLength;
        for (int i = 0; i < length; i++)
            SetPixelColorRaw(i, Rgbw.Blend(GetPixelColorRaw(i), Colors[1], fadeBy));
    }

    /// <summary>Scales every pixel towards black.</summary>
    public void FadeToBlackBy(byte fadeBy)
    {
        if (!IsActive || fadeBy == 0) return;
        int length = RawLength;
        for (int i = 0; i < length; i++)
            SetPixelColorRaw(i, GetPixelColorRaw(i).Scale((byte)(255 - fadeBy)));
    }

    /// <summary>
    /// Blurs the segment. <paramref name="smear"/> keeps the original brightness instead of
    /// dimming as it spreads, which gives a smoke-like look rather than a soft one.
    /// </summary>
    /// <remarks>Amounts above 215 produce an alternating pattern rather than a blur.</remarks>
    public void Blur(byte blurAmount, bool smear = false)
    {
        if (!IsActive || blurAmount == 0) return;
        if (Is2D)
        {
            Blur2D(blurAmount, blurAmount, smear);
            return;
        }

        byte keep = smear ? (byte)255 : (byte)(255 - blurAmount);
        var seep = (byte)(blurAmount >> 1);
        int length = Length;

        Rgbw current = GetPixelColorRaw(0);
        Rgbw carryover = current.Scale(seep);
        SetPixelColorRaw(0, current.Scale(keep));
        for (int i = 1; i < length; i++)
        {
            current = GetPixelColorRaw(i);
            Rgbw part = current.Scale(seep);
            current = current.Scale(keep).Add(carryover);
            SetPixelColorRaw(i - 1, GetPixelColorRaw(i - 1).Add(part));
            SetPixelColorRaw(i, current);
            carryover = part;
        }
    }

    /// <summary>Blurs along both axes independently; either amount may be zero.</summary>
    public void Blur2D(byte blurX, byte blurY, bool smear = false)
    {
        if (!IsActive) return;
        int cols = Width, rows = Height;

        if (blurX != 0)
        {
            byte keep = smear ? (byte)255 : (byte)(255 - blurX);
            var seep = (byte)(blurX >> 1);
            for (int row = 0; row < rows; row++)
            {
                Rgbw current = GetPixelColorXYRaw(0, row);
                Rgbw carryover = current.Scale(seep);
                SetPixelColorXYRaw(0, row, current.Scale(keep));
                for (int x = 1; x < cols; x++)
                {
                    current = GetPixelColorXYRaw(x, row);
                    Rgbw part = current.Scale(seep);
                    current = current.Scale(keep).Add(carryover);
                    SetPixelColorXYRaw(x - 1, row, GetPixelColorXYRaw(x - 1, row).Add(part));
                    SetPixelColorXYRaw(x, row, current);
                    carryover = part;
                }
            }
        }

        if (blurY != 0)
        {
            byte keep = smear ? (byte)255 : (byte)(255 - blurY);
            var seep = (byte)(blurY >> 1);
            for (int col = 0; col < cols; col++)
            {
                Rgbw current = GetPixelColorXYRaw(col, 0);
                Rgbw carryover = current.Scale(seep);
                SetPixelColorXYRaw(col, 0, current.Scale(keep));
                for (int y = 1; y < rows; y++)
                {
                    current = GetPixelColorXYRaw(col, y);
                    Rgbw part = current.Scale(seep);
                    current = current.Scale(keep).Add(carryover);
                    SetPixelColorXYRaw(col, y - 1, GetPixelColorXYRaw(col, y - 1).Add(part));
                    SetPixelColorXYRaw(col, y, current);
                    carryover = part;
                }
            }
        }
    }

    /// <summary>Blurs every row; half the cost of a full 2D blur.</summary>
    public void BlurRows(byte blurAmount, bool smear = false) => Blur2D(blurAmount, 0, smear);

    /// <summary>Blurs every column; half the cost of a full 2D blur.</summary>
    public void BlurCols(byte blurAmount, bool smear = false) => Blur2D(0, blurAmount, smear);

    // ------------------------------------------------------------------ palette use

    /// <summary>
    /// Whether a static palette lookup should wrap from the last entry back to the first.
    /// This is the C++ <c>PALETTE_SOLID_WRAP</c> macro, which effects pass to
    /// <see cref="ColorFromPalette"/> as the <c>moving</c> argument.
    /// </summary>
    public bool PaletteSolidWrap
        => (Strip?.PaletteBlend ?? PaletteBlendMode.WrapWhenMoving) is PaletteBlendMode.AlwaysWrap or PaletteBlendMode.None;

    /// <summary>
    /// Whether a scrolling palette lookup should wrap. This is the C++ <c>PALETTE_MOVING_WRAP</c>
    /// macro; a stationary effect (speed 0) does not wrap in the default mode.
    /// </summary>
    public bool PaletteMovingWrap
    {
        get
        {
            PaletteBlendMode blend = Strip?.PaletteBlend ?? PaletteBlendMode.WrapWhenMoving;
            return !(blend == PaletteBlendMode.NeverWrap || (blend == PaletteBlendMode.WrapWhenMoving && Speed == 0));
        }
    }

    /// <summary>
    /// A colour from the segment palette. When no palette is selected this returns the segment
    /// colour in slot <paramref name="colorSlot"/> instead, which is what makes palette-aware
    /// effects fall back to the user colours.
    /// </summary>
    /// <param name="index">Palette index; spread across the segment when <paramref name="mapping"/> is set.</param>
    /// <param name="mapping">Whether <paramref name="index"/> is a pixel position rather than a 0-255 palette index.</param>
    /// <param name="moving">Whether the effect scrolls the palette, in which case it should wrap end to start.</param>
    /// <param name="colorSlot">Which segment colour to fall back to when no palette is set.</param>
    /// <param name="brightness">Scales the result.</param>
    public Rgbw ColorFromPalette(int index, bool mapping, bool moving, byte colorSlot, byte brightness = 255)
    {
        Rgbw color = Color(colorSlot);
        if (Palette == 0 && colorSlot < ColorCount) return color.Fade(brightness, video: true);

        int paletteIndex = index;
        if (mapping) paletteIndex = System.Math.Min(index * 255 / System.Math.Max(Length, 1), 255);

        BlendType blend = (Strip?.PaletteBlend ?? PaletteBlendMode.WrapWhenMoving) switch
        {
            PaletteBlendMode.WrapWhenMoving => moving ? BlendType.LinearBlend : BlendType.LinearBlendNoWrap,
            PaletteBlendMode.AlwaysWrap => BlendType.LinearBlend,
            PaletteBlendMode.NeverWrap => BlendType.LinearBlendNoWrap,
            _ => BlendType.NoBlend,
        };

        return ColorUtil.ColorFromPalette(CurrentPalette, paletteIndex, brightness, blend).WithWhite(color.W);
    }

    /// <summary>
    /// A colour from the hue wheel, or from the palette when one is selected.
    /// <paramref name="pos"/> 0-255 covers the full wheel.
    /// </summary>
    public Rgbw ColorWheel(byte pos)
    {
        if (Palette != 0) return ColorFromPalette(pos, false, true, 0);
        return ColorUtil.HsvToRgbRainbow((ushort)(pos << 8), 255, 255).WithWhite(Color(0).W);
    }

    // ------------------------------------------------------------------ 2D movement

    /// <summary>Shifts the whole segment horizontally.</summary>
    public void MoveX(int delta, bool wrap = false)
    {
        if (!IsActive || delta == 0) return;
        int vW = Width, vH = Height;
        int absDelta = System.Math.Abs(delta);
        if (absDelta >= vW) return;

        var row = new Rgbw[vW];
        int start = 0, stop = vW, newDelta;
        if (wrap) newDelta = (delta + vW) % vW;
        else
        {
            if (delta < 0) start = absDelta;
            stop = vW - absDelta;
            newDelta = delta > 0 ? delta : 0;
        }

        for (int y = 0; y < vH; y++)
        {
            for (int x = 0; x < stop; x++)
            {
                int srcX = x + newDelta;
                if (wrap) srcX %= vW;
                row[x] = GetPixelColorXYRaw(srcX, y);
            }
            for (int x = 0; x < stop; x++) SetPixelColorXYRaw(x + start, y, row[x]);
        }
    }

    /// <summary>Shifts the whole segment vertically.</summary>
    public void MoveY(int delta, bool wrap = false)
    {
        if (!IsActive || delta == 0) return;
        int vW = Width, vH = Height;
        int absDelta = System.Math.Abs(delta);
        if (absDelta >= vH) return;

        var column = new Rgbw[vH];
        int start = 0, stop = vH, newDelta;
        if (wrap) newDelta = (delta + vH) % vH;
        else
        {
            if (delta < 0) start = absDelta;
            stop = vH - absDelta;
            newDelta = delta > 0 ? delta : 0;
        }

        for (int x = 0; x < vW; x++)
        {
            for (int y = 0; y < stop; y++)
            {
                int srcY = y + newDelta;
                if (wrap) srcY %= vH;
                column[y] = GetPixelColorXYRaw(x, srcY);
            }
            for (int y = 0; y < stop; y++) SetPixelColorXYRaw(x, y + start, column[y]);
        }
    }

    /// <summary>
    /// Shifts the segment in one of eight directions: 0 left, 2 up, 4 right, 6 down and the
    /// diagonals in between.
    /// </summary>
    public void Move(int direction, int delta, bool wrap = false)
    {
        if (delta == 0) return;
        switch (direction)
        {
            case 0: MoveX(delta, wrap); break;
            case 1: MoveX(delta, wrap); MoveY(delta, wrap); break;
            case 2: MoveY(delta, wrap); break;
            case 3: MoveX(-delta, wrap); MoveY(delta, wrap); break;
            case 4: MoveX(-delta, wrap); break;
            case 5: MoveX(-delta, wrap); MoveY(-delta, wrap); break;
            case 6: MoveY(-delta, wrap); break;
            case 7: MoveX(delta, wrap); MoveY(-delta, wrap); break;
        }
    }

    // -------------------------------------------------------------------- 2D shapes

    /// <summary>
    /// Draws a circle outline. <paramref name="soft"/> switches from Bresenham to Xiaolin Wu
    /// anti-aliasing.
    /// </summary>
    public void DrawCircle(int cx, int cy, int radius, Rgbw color, bool soft = false)
    {
        if (!IsActive || radius == 0) return;

        if (soft)
        {
            int rsq = radius * radius;
            int x = 0, y = radius;
            int oldFade = 0;
            while (x < y)
            {
                float yf = MathF.Sqrt(rsq - x * x);
                var fade = (byte)(0xFF * (MathF.Ceiling(yf) - yf)); // how much of the colour to keep
                if (oldFade > fade) y--;
                oldFade = fade;
                for (int i = 0; i < 16; i++)
                {
                    bool swaps = (i & 0x4) != 0;
                    int adj = i < 8 ? 0 : 1;
                    int dx = (i & 1) != 0 ? -1 : 1;
                    int dy = (i & 2) != 0 ? -1 : 1;
                    int px, py;
                    if (swaps) { px = cx + (y - adj) * dx; py = cy + x * dy; }
                    else { px = cx + x * dx; py = cy + (y - adj) * dy; }
                    Rgbw existing = GetPixelColorXY(px, py);
                    SetPixelColorXY(px, py, adj != 0
                        ? Rgbw.Blend(existing, color, fade)
                        : Rgbw.Blend(color, existing, fade));
                }
                x++;
            }
        }
        else
        {
            int d = 3 - 2 * radius;
            int y = radius, x = 0;
            while (y >= x)
            {
                for (int i = 0; i < 4; i++)
                {
                    int dx = (i & 1) != 0 ? -x : x;
                    int dy = (i & 2) != 0 ? -y : y;
                    SetPixelColorXY(cx + dx, cy + dy, color);
                    SetPixelColorXY(cx + dy, cy + dx, color);
                }
                x++;
                if (d > 0) { y--; d += 4 * (x - y) + 10; }
                else d += 4 * x + 6;
            }
        }
    }

    /// <summary>Draws a filled disc, optionally with an anti-aliased edge.</summary>
    public void FillCircle(int cx, int cy, int radius, Rgbw color, bool soft = false)
    {
        if (!IsActive || radius == 0) return;
        if (soft) DrawCircle(cx, cy, radius, color, soft);

        int vW = Width, vH = Height;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius
                    && cx + x >= 0 && cy + y >= 0 && cx + x < vW && cy + y < vH)
                    SetPixelColorXY(cx + x, cy + y, color);
            }
        }
    }

    /// <summary>
    /// Draws a line. <paramref name="soft"/> switches from Bresenham to Xiaolin Wu anti-aliasing.
    /// </summary>
    public void DrawLine(int x0, int y0, int x1, int y1, Rgbw color, bool soft = false)
    {
        if (!IsActive) return;
        int vW = Width, vH = Height;
        if ((uint)x0 >= (uint)vW || (uint)x1 >= (uint)vW || (uint)y0 >= (uint)vH || (uint)y1 >= (uint)vH) return;

        int dx = System.Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = System.Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;

        if (dx + dy == 0)
        {
            SetPixelColorXY(x0, y0, color);
            return;
        }

        if (soft)
        {
            bool steep = dy > dx;
            if (steep) // always walk along the longer axis
            {
                (x0, y0) = (y0, x0);
                (x1, y1) = (y1, x1);
            }
            if (x0 > x1) // always walk in increasing order
            {
                (x0, x1) = (x1, x0);
                (y0, y1) = (y1, y0);
            }
            float gradient = x1 - x0 == 0 ? 1.0f : (float)(y1 - y0) / (x1 - x0);
            float intersectY = y0;
            for (int x = x0; x <= x1; x++)
            {
                var keep = (byte)(0xFF * (intersectY - (int)intersectY)); // coverage from the fraction of y
                var seep = (byte)(0xFF - keep);
                var y = (int)intersectY;
                int px = steep ? y : x;
                int py = steep ? x : y;
                BlendPixelColorXY(px, py, color, seep);
                BlendPixelColorXY(px + (steep ? 1 : 0), py + (steep ? 0 : 1), color, keep);
                intersectY += gradient;
            }
        }
        else
        {
            int err = (dx > dy ? dx : -dy) / 2;
            while (true)
            {
                SetPixelColorXY(x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int e2 = err;
                if (e2 > -dx) { err -= dy; x0 += sx; }
                if (e2 < dy) { err += dx; y0 += sy; }
            }
        }
    }

    private static byte WuWeight(int a, int b) => (byte)((a * b + a + b) >> 8);

    /// <summary>
    /// Adds a colour at sub-pixel coordinates, spreading it over the four surrounding pixels.
    /// Coordinates are 8.8 fixed point, so 256 is one pixel.
    /// </summary>
    public void WuPixel(int x, int y, Crgb color)
    {
        if (!IsActive) return;
        int xx = x & 0xFF, yy = y & 0xFF, ix = 255 - xx, iy = 255 - yy;
        Span<byte> weights =
        [
            WuWeight(ix, iy), WuWeight(xx, iy),
            WuWeight(ix, yy), WuWeight(xx, yy),
        ];
        for (int i = 0; i < 4; i++)
        {
            int px = (x >> 8) + (i & 1);
            int py = (y >> 8) + ((i >> 1) & 1);
            var led = (Crgb)GetPixelColorXY(px, py);
            var blended = new Crgb(
                FastMath.QAdd8(led.R, (byte)(color.R * weights[i] >> 8)),
                FastMath.QAdd8(led.G, (byte)(color.G * weights[i] >> 8)),
                FastMath.QAdd8(led.B, (byte)(color.B * weights[i] >> 8)));
            if (blended != led) SetPixelColorXY(px, py, blended);
        }
    }
}
