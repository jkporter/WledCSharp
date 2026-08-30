namespace Wled.Fx;

/// <summary>How a segment is combined with what is already on the strip beneath it.</summary>
/// <remarks>Values match the <c>bm</c> list in the WLED UI, so presets carry across unchanged.</remarks>
public enum BlendMode : byte
{
    Top = 0,
    Bottom = 1,
    Add = 2,
    Subtract = 3,
    Difference = 4,
    Average = 5,
    Multiply = 6,
    Divide = 7,
    Lighten = 8,
    Darken = 9,
    Screen = 10,
    Overlay = 11,
    HardLight = 12,
    SoftLight = 13,
    Dodge = 14,
    Burn = 15,
    /// <summary>Keep the top layer wherever it is lit, otherwise show the bottom layer.</summary>
    Stencil = 16,
}

/// <summary>How one look gives way to the next when a segment changes.</summary>
public enum TransitionStyle : byte
{
    Fade = 0x00,
    FairyDust = 0x01,
    SwipeRight = 0x02,
    SwipeLeft = 0x03,
    OutsideIn = 0x04,
    InsideOut = 0x05,
    SwipeUp = 0x06,
    SwipeDown = 0x07,
    OpenHorizontal = 0x08,
    OpenVertical = 0x09,
    SwipeTopLeft = 0x0A,
    SwipeTopRight = 0x0B,
    SwipeBottomRight = 0x0C,
    SwipeBottomLeft = 0x0D,
    CircularOut = 0x0E,
    CircularIn = 0x0F,
    PushRight = 0x10,
    PushLeft = 0x11,
    PushUp = 0x12,
    PushDown = 0x13,
    PushTopLeft = 0x14,
    PushTopRight = 0x15,
    PushBottomRight = 0x16,
    PushBottomLeft = 0x17,
}

/// <summary>Whether palette lookups wrap from the last entry back to the first.</summary>
public enum PaletteBlendMode : byte
{
    /// <summary>Wrap only for effects that scroll the palette.</summary>
    WrapWhenMoving = 0,
    /// <summary>Always wrap.</summary>
    AlwaysWrap = 1,
    /// <summary>Never wrap, so the last palette entry is reached exactly.</summary>
    NeverWrap = 2,
    /// <summary>Do not interpolate between palette entries at all.</summary>
    None = 3,
}

/// <summary>
/// The effect engine: owns the pixel buffer, the segments and the frame loop.
/// Port of the <c>WS2812FX</c> class from <c>FX.h</c> / <c>FX_fcn.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// Call <see cref="Service"/> as often as you like; it renders only when a frame is actually due at
/// the configured <see cref="TargetFps"/>, then raises <see cref="FrameReady"/> with the finished
/// buffer. Everything the firmware does with LED buses is left to the caller: this class stops at
/// producing pixels.
/// </para>
/// <para>
/// Segments are drawn in order, each into its own buffer, and then blended down onto the strip by
/// <see cref="BlendSegment"/> - that is where grouping, spacing, mirroring, reversing,
/// transposition, per-segment opacity and the blend modes are applied.
/// </para>
/// </remarks>
public sealed class LedStrip
{
    /// <summary>Frames per second the engine aims for by default.</summary>
    public const int DefaultFps = 42;

    /// <summary>Largest number of segments a strip may hold.</summary>
    public const int MaxSegments = 32;

    private Rgbw[] _pixels;
    private readonly List<Segment> _segments = [];
    private int _targetFps = DefaultFps;
    private int _frameTime = 1000 / DefaultFps;
    private uint _lastServiceShow;
    private uint _lastShow;
    private int _mainSegment;

    /// <summary>Creates a strip of <paramref name="length"/> pixels, optionally as a matrix.</summary>
    public LedStrip(int length = 30, int height = 1)
    {
        if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
        if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));

        MatrixWidth = length;
        MatrixHeight = height;
        IsMatrix = height > 1;
        _pixels = new Rgbw[length * height];
        AddSegment(0, length, 0, height);
    }

    // ------------------------------------------------------------------ geometry

    /// <summary>Width of the strip, or of the matrix.</summary>
    public int MatrixWidth { get; private set; }

    /// <summary>Height of the matrix; 1 for a plain strip.</summary>
    public int MatrixHeight { get; private set; }

    /// <summary>True when the strip is laid out as a matrix.</summary>
    public bool IsMatrix { get; private set; }

    /// <summary>Total number of pixels, including any a matrix has but the physical strip does not.</summary>
    public int Length => _pixels.Length;

    /// <summary>The finished frame. Valid to read after <see cref="FrameReady"/> fires.</summary>
    public ReadOnlySpan<Rgbw> Pixels => _pixels;

    /// <summary>
    /// Resizes the strip, dropping every segment and creating a single one that covers it.
    /// </summary>
    public void Resize(int width, int height = 1)
    {
        if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));

        MatrixWidth = width;
        MatrixHeight = height;
        IsMatrix = height > 1;
        _pixels = new Rgbw[width * height];
        foreach (Segment segment in _segments) segment.Strip = null;
        _segments.Clear();
        _mainSegment = 0;
        AddSegment(0, width, 0, height);
    }

    // ------------------------------------------------------------------- settings

    /// <summary>Global brightness applied when the frame is finished, 0-255.</summary>
    public byte Brightness { get; set; } = 127;

    /// <summary>How long a segment change takes to cross-fade, in milliseconds.</summary>
    public int TransitionDuration { get; set; } = 750;

    /// <summary>How a segment change gives way to the next look.</summary>
    public TransitionStyle BlendingStyle { get; set; } = TransitionStyle.Fade;

    /// <summary>Whether palette lookups wrap.</summary>
    public PaletteBlendMode PaletteBlend { get; set; } = PaletteBlendMode.WrapWhenMoving;

    /// <summary>Frames per second the engine aims for; 0 renders as fast as it is called.</summary>
    public int TargetFps
    {
        get => _targetFps;
        set
        {
            _targetFps = FastMath.Clamp(value, 0, 120);
            _frameTime = _targetFps > 0 ? 1000 / _targetFps : 1;
        }
    }

    /// <summary>Nominal duration of one frame, in milliseconds.</summary>
    public int FrameTime => _frameTime;

    /// <summary>Common time base handed to effects; advances with the clock plus <see cref="Timebase"/>.</summary>
    public uint Now { get; private set; }

    /// <summary>Offset added to the clock, so effects can be restarted from time zero.</summary>
    public uint Timebase { get; set; }

    /// <summary>Restarts the effect time base at zero.</summary>
    public void ResetTimebase() => Timebase = 0u - Clock.Millis;

    /// <summary>Measured frame rate over the last couple of seconds.</summary>
    public int Fps { get; private set; }

    /// <summary>Forces the next <see cref="Service"/> call to render, ignoring frame pacing.</summary>
    public void Trigger() => _triggered = true;

    private bool _triggered;
    private bool _suspended;

    /// <summary>Suspends rendering; <see cref="Service"/> returns without drawing.</summary>
    public void Suspend() => _suspended = true;

    /// <summary>Resumes rendering after <see cref="Suspend"/>.</summary>
    public void Resume() => _suspended = false;

    /// <summary>Raised once a frame has been rendered and is ready to be pushed to hardware.</summary>
    public event Action<LedStrip>? FrameReady;

    // ------------------------------------------------------------------- segments

    /// <summary>The segments on this strip, in draw order.</summary>
    public IReadOnlyList<Segment> Segments => _segments;

    /// <summary>The segment currently being rendered; only meaningful inside an effect.</summary>
    public Segment? CurrentSegment { get; private set; }

    /// <summary>Index of the segment currently being rendered; only meaningful inside an effect.</summary>
    public int CurrentSegmentId { get; private set; }

    /// <summary>How many segments currently have a usable pixel buffer.</summary>
    public int ActiveSegmentCount => _segments.Count(s => s.IsActive);

    /// <summary>Index of the first selected segment, or of the main one when nothing is selected.</summary>
    public int FirstSelectedSegmentId
    {
        get
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i].IsActive && _segments[i].Selected) return i;
            }
            return MainSegmentId;
        }
    }

    /// <summary>The main segment, which the UI treats as the one being edited.</summary>
    public Segment MainSegment => _segments[System.Math.Min(_mainSegment, _segments.Count - 1)];

    /// <summary>Index of the main segment.</summary>
    public int MainSegmentId
    {
        get => _mainSegment;
        set => _mainSegment = FastMath.Clamp(value, 0, System.Math.Max(_segments.Count - 1, 0));
    }

    /// <summary>Adds a segment covering <c>[start, stop)</c>.</summary>
    /// <exception cref="InvalidOperationException">The strip already holds <see cref="MaxSegments"/> segments.</exception>
    public Segment AddSegment(int start, int stop, int startY = 0, int stopY = 1)
    {
        if (_segments.Count >= MaxSegments)
            throw new InvalidOperationException($"A strip may hold at most {MaxSegments} segments.");

        var segment = new Segment(start, stop, startY, stopY) { Strip = this };
        segment.SetDrawDimensions();
        _segments.Add(segment);
        return segment;
    }

    /// <summary>Removes a segment.</summary>
    public bool RemoveSegment(Segment segment)
    {
        if (!_segments.Remove(segment)) return false;
        segment.Strip = null;
        if (_mainSegment >= _segments.Count) _mainSegment = System.Math.Max(_segments.Count - 1, 0);
        return true;
    }

    /// <summary>Returns the segment at <paramref name="id"/>, or the main one if the ID is out of range.</summary>
    public Segment GetSegment(int id) => (uint)id < _segments.Count ? _segments[id] : MainSegment;

    /// <summary>Marks every segment for reset, so each effect restarts from clean state.</summary>
    public void ResetSegments()
    {
        foreach (Segment segment in _segments) segment.MarkForReset();
    }

    // ---------------------------------------------------------------- frame loop

    /// <summary>
    /// Renders a frame if one is due. Returns true when a frame was produced, in which case
    /// <see cref="Pixels"/> holds it and <see cref="FrameReady"/> has been raised.
    /// </summary>
    public bool Service()
    {
        uint nowUp = Clock.Millis;
        uint elapsed = nowUp - _lastServiceShow;
        bool timeToShow = elapsed >= _frameTime || _triggered || _targetFps == 0;

        Now = nowUp + Timebase; // one time base for every effect this frame
        if (!timeToShow || _suspended) return false;

        bool drewSomething = _triggered;
        for (int i = 0; i < _segments.Count; i++)
        {
            Segment seg = _segments[i];
            if (_suspended) break;

            seg.HandleTransition();
            seg.ResetIfRequired();
            if (!seg.IsActive) continue;

            drewSomething = true;
            if (seg.Freeze) continue;

            ushort progress = seg.Progress;
            seg.BeginDraw(progress);
            CurrentSegment = seg;
            CurrentSegmentId = i;
            EffectRegistry.Get(seg.Mode).Render(seg);
            seg.Call++;

            // while a non-fade transition runs, the outgoing effect has to keep drawing too
            Segment? old = seg.OldSegment;
            if (old is { IsActive: true } && (seg.Mode != old.Mode || BlendingStyle != TransitionStyle.Fade))
            {
                old.BeginDraw(progress);
                CurrentSegment = old;
                EffectRegistry.Get(old.Mode).Render(old);
                old.Call++;
            }
        }
        CurrentSegment = null;

        if (!drewSomething || _suspended) return false;

        Segment.HandleRandomPalette(_frameTime, TransitionDuration);
        _lastServiceShow = nowUp;
        Show();
        _triggered = false;
        return true;
    }

    /// <summary>
    /// Composites every segment onto the strip and raises <see cref="FrameReady"/>.
    /// Called by <see cref="Service"/>; call it directly only when driving the engine by hand.
    /// </summary>
    public void Show()
    {
        Array.Clear(_pixels);
        foreach (Segment segment in _segments)
        {
            if (segment.IsActive) BlendSegment(segment);
        }

        uint now = Clock.Millis;
        uint sinceLast = now - _lastShow;
        if (sinceLast > 0) Fps = (int)(1000 / sinceLast);
        _lastShow = now;

        FrameReady?.Invoke(this);
    }

    /// <summary>
    /// The frame with global brightness and gamma applied, ready to hand to LED hardware.
    /// </summary>
    public void CopyOutput(Span<Rgbw> destination)
    {
        if (destination.Length < _pixels.Length)
            throw new ArgumentException("Destination is shorter than the strip.", nameof(destination));

        byte brightness = Brightness;
        for (int i = 0; i < _pixels.Length; i++)
        {
            Rgbw color = brightness == 255 ? _pixels[i] : _pixels[i].Fade(brightness);
            destination[i] = Gamma.Correct(color);
        }
    }

    /// <summary>Reads one pixel of the composited frame.</summary>
    public Rgbw GetPixelColor(int index) => (uint)index < _pixels.Length ? _pixels[index] : Rgbw.Black;

    /// <summary>Writes one pixel of the composited frame directly, bypassing the segments.</summary>
    public void SetPixelColor(int index, Rgbw color)
    {
        if ((uint)index < _pixels.Length) _pixels[index] = color;
    }

    /// <summary>Reads one pixel of the composited frame by matrix coordinates.</summary>
    public Rgbw GetPixelColorXY(int x, int y) => GetPixelColor(y * MatrixWidth + x);

    /// <summary>Writes one pixel of the composited frame by matrix coordinates.</summary>
    public void SetPixelColorXY(int x, int y, Rgbw color) => SetPixelColor(y * MatrixWidth + x, color);

    /// <summary>Fills the whole composited frame with one colour.</summary>
    public void Fill(Rgbw color) => Array.Fill(_pixels, color);

    // ------------------------------------------------------------------ blending

    /// <summary>Clipping window in force this frame, used by the wipe and push transitions.</summary>
    internal int ClipStart { get; private set; }

    internal int ClipStop { get; private set; }

    internal int ClipStartY { get; private set; }

    internal int ClipStopY { get; private set; } = 1;

    private void SetClippingRect(int startX, int stopX, int startY = 0, int stopY = 1)
    {
        ClipStart = startX;
        ClipStop = stopX;
        ClipStartY = startY;
        ClipStopY = stopY;
    }

    private static Rgbw Blend(BlendMode mode, Rgbw top, Rgbw bottom) => mode switch
    {
        BlendMode.Top => top,
        BlendMode.Bottom => bottom,
        BlendMode.Add => top.Add(bottom, preserveRatio: true),
        BlendMode.Stencil => top.IsBlack ? bottom : top,
        _ => PerChannel(mode, top, bottom),
    };

    private static Rgbw PerChannel(BlendMode mode, Rgbw t, Rgbw b) => new(
        BlendChannel(mode, t.R, b.R),
        BlendChannel(mode, t.G, b.G),
        BlendChannel(mode, t.B, b.B),
        BlendChannel(mode, t.W, b.W));

    // https://en.wikipedia.org/wiki/Blend_modes, with a for the top layer and b for the bottom one
    private static byte BlendChannel(BlendMode mode, byte a, byte b) => mode switch
    {
        BlendMode.Subtract => (byte)(b > a ? b - a : 0),
        BlendMode.Difference => (byte)(b > a ? b - a : a - b),
        BlendMode.Average => (byte)((a + b) >> 1),
        BlendMode.Multiply => Multiply(a, b),
        BlendMode.Divide => Divide(a, b),
        BlendMode.Lighten => System.Math.Max(a, b),
        BlendMode.Darken => System.Math.Min(a, b),
        BlendMode.Screen => (byte)(255 - Multiply((byte)~a, (byte)~b)),
        BlendMode.Overlay => b < 128 ? (byte)(2 * Multiply(a, b)) : (byte)(255 - 2 * Multiply((byte)~a, (byte)~b)),
        BlendMode.HardLight => a < 128 ? (byte)(2 * Multiply(a, b)) : (byte)(255 - 2 * Multiply((byte)~a, (byte)~b)),
        // Pegtop's formula: (1 - 2a)b^2 + 2ab
        BlendMode.SoftLight => (byte)((b * b * (255 - 2 * a) + 255 * 2 * a * b) / (255 * 255)),
        BlendMode.Dodge => Divide((byte)~a, b),
        BlendMode.Burn => (byte)~Divide(a, (byte)~b),
        _ => a,
    };

    private static byte Multiply(byte a, byte b) => (byte)(a * b / 255);

    private static byte Divide(byte a, byte b) => (byte)(a > b ? b * 255 / a : 255);

    /// <summary>
    /// Expands one segment buffer onto the strip, applying grouping, spacing, mirroring, reversal,
    /// transposition, opacity, the blend mode and any transition clipping.
    /// </summary>
    public void BlendSegment(Segment top)
    {
        ArgumentNullException.ThrowIfNull(top);
        if (!top.IsActive) return;

        int width = top.PhysicalWidth;
        int height = top.PhysicalHeight;
        byte opacity = top.CurrentBrightness();
        if (Gamma.Enabled) opacity = Gamma.RawInverse8(opacity); // so gamma is applied after scaling
        BlendMode mode = top.BlendMode;
        Segment? old = top.OldSegment;
        bool hasGrouping = top.GroupLength != 1;

        // fast path: no transition, no grouping, no mirroring
        if (old is null && BlendingStyle == TransitionStyle.Fade && !hasGrouping && !top.Mirror && !top.MirrorY)
        {
            if (IsMatrix && Index(top.Start, top.StartY) + width * height <= _pixels.Length)
            {
                BlendFastMatrix(top, width, height, mode, opacity);
                return;
            }
            if (!IsMatrix)
            {
                BlendFastStrip(top, width * height, mode, opacity);
                return;
            }
        }

        SetClippingRect(0, 0);
        int progress = top.Progress;
        int progInv = 0xFFFF - progress;
        int dw = (BlendingStyle == TransitionStyle.OutsideIn ? progInv : progress) * width / 0xFFFF + 1;
        int dh = (BlendingStyle == TransitionStyle.OutsideIn ? progInv : progress) * height / 0xFFFF + 1;
        TransitionStyle style = width * height == 1 ? TransitionStyle.Fade : BlendingStyle;

        switch (style)
        {
            // these three have to be handed the whole segment; the per-pixel test does the shaping
            case TransitionStyle.CircularIn:
            case TransitionStyle.CircularOut:
            case TransitionStyle.FairyDust:
                SetClippingRect(0, width, 0, height);
                break;
            case TransitionStyle.SwipeRight:
            case TransitionStyle.PushRight:
                SetClippingRect(0, dw, 0, height);
                break;
            case TransitionStyle.SwipeLeft:
            case TransitionStyle.PushLeft:
                SetClippingRect(width - dw, width, 0, height);
                break;
            case TransitionStyle.OutsideIn:
                SetClippingRect((width + dw) / 2, (width - dw) / 2, (height + dh) / 2, (height - dh) / 2); // inverted
                break;
            case TransitionStyle.InsideOut:
                SetClippingRect((width - dw) / 2, (width + dw) / 2, (height - dh) / 2, (height + dh) / 2);
                break;
            case TransitionStyle.SwipeDown:
            case TransitionStyle.PushDown:
                SetClippingRect(0, width, 0, dh);
                break;
            case TransitionStyle.SwipeUp:
            case TransitionStyle.PushUp:
                SetClippingRect(0, width, height - dh, height);
                break;
            case TransitionStyle.OpenHorizontal:
                SetClippingRect((width - dw) / 2, (width + dw) / 2, 0, height);
                break;
            case TransitionStyle.OpenVertical:
                SetClippingRect(0, width, (height - dh) / 2, (height + dh) / 2);
                break;
            case TransitionStyle.SwipeTopLeft:
            case TransitionStyle.PushTopLeft:
                SetClippingRect(0, dw, 0, dh);
                break;
            case TransitionStyle.SwipeTopRight:
            case TransitionStyle.PushTopRight:
                SetClippingRect(width - dw, width, 0, dh);
                break;
            case TransitionStyle.SwipeBottomRight:
            case TransitionStyle.PushBottomRight:
                SetClippingRect(width - dw, width, height - dh, height);
                break;
            case TransitionStyle.SwipeBottomLeft:
            case TransitionStyle.PushBottomLeft:
                SetClippingRect(0, dw, height - dh, height);
                break;
        }

        if (IsMatrix && Index(top.Start, top.StartY) + width * height <= _pixels.Length)
            BlendMatrix(top, old, width, height, mode, opacity, progInv, style);
        else
            BlendStrip(top, old, width * height, mode, opacity, progInv, style);

        SetClippingRect(0, 0);
    }

    private int Index(int x, int y) => x + y * MatrixWidth;

    private void BlendFastMatrix(Segment top, int width, int height, BlendMode mode, byte opacity)
    {
        if (!top.Transpose)
        {
            int xInc = 1;
            int yInc = MatrixWidth;
            int startOffset = Index(top.Start, top.StartY);
            if (top.Reverse) { startOffset += width - 1; xInc = -1; }
            if (top.ReverseY) { startOffset += (height - 1) * MatrixWidth; yInc = -MatrixWidth; }

            for (int y = 0; y < height; y++)
            {
                int rowStart = startOffset + y * yInc;
                int yWidth = y * width;
                for (int x = 0; x < width; x++)
                {
                    int p = rowStart + x * xInc;
                    Rgbw source = top.GetPixelColorRaw(x + yWidth);
                    _pixels[p] = Rgbw.Blend(_pixels[p], Blend(mode, source, _pixels[p]), opacity);
                }
            }
            return;
        }

        for (int y = 0; y < height; y++)
        {
            int px = top.Reverse ? height - y - 1 : y; // the source has X and Y swapped
            for (int x = 0; x < width; x++)
            {
                int py = top.ReverseY ? width - x - 1 : x;
                Rgbw source = top.GetPixelColorRaw(px + py * height); // height is the virtual width here
                int idx = Index(top.Start + x, top.StartY + y);
                _pixels[idx] = Rgbw.Blend(_pixels[idx], Blend(mode, source, _pixels[idx]), opacity);
            }
        }
    }

    private void BlendFastStrip(Segment top, int length, BlendMode mode, byte opacity)
    {
        for (int i = 0; i < length; i++)
        {
            Rgbw source = top.GetPixelColorRaw(i);
            int p = top.Reverse ? length - i - 1 : i;
            int idx = top.Start + p + top.Offset;
            if (idx >= top.Stop) idx -= length;
            if ((uint)idx >= _pixels.Length) continue;
            _pixels[idx] = Rgbw.Blend(_pixels[idx], Blend(mode, source, _pixels[idx]), opacity);
        }
    }

    private void BlendMatrix(Segment top, Segment? old, int width, int height, BlendMode mode, byte opacity,
                             int progInv, TransitionStyle style)
    {
        int nCols = top.VirtualWidth();
        int nRows = top.VirtualHeight();
        int oCols = old?.VirtualWidth() ?? nCols;
        int oRows = old?.VirtualHeight() ?? nRows;
        int groupLen = top.GroupLength;
        bool applyReverse = top.Reverse || top.ReverseY || top.Transpose;

        int offsetX = style is TransitionStyle.PushUp or TransitionStyle.PushDown ? 0 : progInv * nCols / 0xFFFF;
        int offsetY = style is TransitionStyle.PushLeft or TransitionStyle.PushRight ? 0 : progInv * nRows / 0xFFFF;
        int pushOffsetX = 0, pushOffsetY = 0;
        switch (style)
        {
            case TransitionStyle.PushRight: pushOffsetX = offsetX; break;
            case TransitionStyle.PushLeft: pushOffsetX = -offsetX + nCols; break;
            case TransitionStyle.PushDown: pushOffsetY = offsetY; break;
            case TransitionStyle.PushUp: pushOffsetY = -offsetY + nRows; break;
            case TransitionStyle.PushTopLeft: pushOffsetX = offsetX; pushOffsetY = offsetY; break;
            case TransitionStyle.PushTopRight: pushOffsetX = -offsetX + nCols; pushOffsetY = offsetY; break;
            case TransitionStyle.PushBottomRight: pushOffsetX = -offsetX + nCols; pushOffsetY = -offsetY + nRows; break;
            case TransitionStyle.PushBottomLeft: pushOffsetX = offsetX; pushOffsetY = -offsetY + nRows; break;
        }

        for (int r = 0; r < nRows; r++)
        {
            for (int c = 0; c < nCols; c++)
            {
                bool clipped = top.IsPixelXYClipped(c, r);
                // a clipped pixel shows the outgoing segment instead; pixels are never clipped for a fade
                Segment source = clipped && old is not null ? old : top;
                int vCols = ReferenceEquals(source, old) ? oCols : nCols;
                int vRows = ReferenceEquals(source, old) ? oRows : nRows;

                int x = c, y = r;
                if (pushOffsetX != 0) x = (x + pushOffsetX) % nCols;
                if (pushOffsetY != 0) y = (y + pushOffsetY) % nRows;

                Rgbw color = Rgbw.Black;
                if (x < vCols && y < vRows) color = source.GetPixelColorRaw(x + y * vCols);
                if (old is not null && style == TransitionStyle.Fade && top.Mode != old.Mode && x < oCols && y < oRows)
                    color = Rgbw.Blend16(color, old.GetPixelColorRaw(x + y * oCols), (ushort)progInv);

                x = c;
                y = r;
                if (applyReverse)
                {
                    if (top.Reverse) x = nCols - x - 1;
                    if (top.ReverseY) y = nRows - y - 1;
                    if (top.Transpose) (x, y) = (y, x);
                }

                if (groupLen == 1)
                {
                    SetMirroredPixel(top, x, y, width, height, color, mode, opacity);
                }
                else
                {
                    x *= groupLen;
                    y *= groupLen;
                    int maxX = System.Math.Min(x + top.Grouping, width);
                    int maxY = System.Math.Min(y + top.Grouping, height);
                    for (int gy = y; gy < maxY; gy++)
                        for (int gx = x; gx < maxX; gx++)
                            SetMirroredPixel(top, gx, gy, width, height, color, mode, opacity);
                }
            }
        }
    }

    private void SetMirroredPixel(Segment top, int x, int y, int width, int height, Rgbw color,
                                  BlendMode mode, byte opacity)
    {
        int baseX = top.Start + x;
        int baseY = top.StartY + y;
        Paint(Index(baseX, baseY), color, mode, opacity);
        if (!top.Mirror && !top.MirrorY) return;

        int mirrorX = top.Start + width - x - 1;
        int mirrorY = top.StartY + height - y - 1;
        if (top.Mirror)
            Paint(Index(top.Transpose ? baseX : mirrorX, top.Transpose ? mirrorY : baseY), color, mode, opacity);
        if (top.MirrorY)
            Paint(Index(top.Transpose ? mirrorX : baseX, top.Transpose ? baseY : mirrorY), color, mode, opacity);
        if (top.Mirror && top.MirrorY)
            Paint(Index(mirrorX, mirrorY), color, mode, opacity);
    }

    private void Paint(int index, Rgbw color, BlendMode mode, byte opacity)
    {
        if ((uint)index >= _pixels.Length) return;
        _pixels[index] = Rgbw.Blend(_pixels[index], Blend(mode, color, _pixels[index]), opacity);
    }

    private void BlendStrip(Segment top, Segment? old, int length, BlendMode mode, byte opacity,
                            int progInv, TransitionStyle style)
    {
        int nLen = top.VirtualLength();
        int oLen = old?.VirtualLength() ?? nLen;
        int offsetI = progInv * nLen / 0xFFFF;

        for (int k = 0; k < nLen; k++)
        {
            bool clipped = top.IsPixelClipped(k);
            Segment source = clipped && old is not null ? old : top;
            int vLen = ReferenceEquals(source, old) ? oLen : nLen;

            int i = k;
            switch (style)
            {
                case TransitionStyle.PushRight: i = (i + offsetI) % nLen; break;
                case TransitionStyle.PushLeft: i = (i - offsetI + nLen) % nLen; break;
            }

            Rgbw color = Rgbw.Black;
            if (i < vLen) color = source.GetPixelColorRaw(i);
            if (old is not null && style == TransitionStyle.Fade && top.Mode != old.Mode && i < oLen)
                color = Rgbw.Blend16(color, old.GetPixelColorRaw(i), (ushort)progInv);

            i = k;
            if (top.Reverse) i = nLen - i - 1;
            i *= top.GroupLength;

            int maxI = System.Math.Min(i + top.Grouping, length);
            while (i < maxI) PaintStrip(top, i++, length, color, mode, opacity);
        }
    }

    private void PaintStrip(Segment top, int i, int length, Rgbw color, BlendMode mode, byte opacity)
    {
        if (top.Mirror)
        {
            int mirrored = top.Stop - i - 1 + top.Offset;
            if (mirrored >= top.Stop) mirrored -= length;
            Paint(mirrored, color, mode, opacity);
        }
        int index = top.Start + i + top.Offset;
        if (index >= top.Stop) index -= length;
        Paint(index, color, mode, opacity);
    }
}
