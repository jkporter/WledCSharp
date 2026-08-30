namespace Wled.Fx;

/// <summary>How a 1D effect is projected onto a 2D segment.</summary>
public enum Mapping1D2D : byte
{
    /// <summary>Treat the matrix as one long strip.</summary>
    Pixels = 0,
    /// <summary>Expand vertically, or run on virtual vertical strips.</summary>
    Bar = 1,
    /// <summary>Expand in arcs from the top-left corner.</summary>
    Arc = 2,
    /// <summary>Expand in nested squares from the top-left corner.</summary>
    Corner = 3,
    /// <summary>Sweep rays out from the centre.</summary>
    Pinwheel = 4,
}

/// <summary>
/// A contiguous run of LEDs (or a rectangle of them on a matrix) that renders one effect.
/// Port of the <c>Segment</c> class from <c>FX.h</c> / <c>FX_fcn.cpp</c> / <c>FX_2Dfcn.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// A segment owns its own pixel buffer. Effects draw into that buffer in <em>virtual</em>
/// coordinates - the coordinate space left after grouping, spacing, mirroring and transposition are
/// accounted for - and <see cref="LedStrip.BlendSegment"/> later expands the buffer onto the
/// physical strip. That is why <see cref="Length"/>, <see cref="Width"/> and <see cref="Height"/>
/// are the virtual dimensions (the C++ <c>SEGLEN</c>, <c>SEG_W</c> and <c>SEG_H</c> macros) while
/// the raw geometry is exposed separately as <see cref="PhysicalLength"/> and friends.
/// </para>
/// <para>
/// Runtime state that an effect needs to carry between frames lives in <see cref="Step"/>,
/// <see cref="Aux0"/>, <see cref="Aux1"/>, <see cref="Call"/> and the typed buffer handed out by
/// <see cref="GetData{T}"/>. All of it is cleared when the segment is reset.
/// </para>
/// </remarks>
public sealed partial class Segment
{
    /// <summary>Number of colour slots every segment carries.</summary>
    public const int ColorCount = 3;

    private Rgbw[] _pixels;
    private object? _data;
    private byte _defaultPalette = 6;

    /// <summary>Creates a segment covering <c>[start, stop)</c> on a single row.</summary>
    public Segment(int start = 0, int stop = 30, int startY = 0, int stopY = 1)
    {
        Start = start;
        Stop = stop > start ? stop : start + 1; // a segment is at least one pixel long
        StartY = startY;
        StopY = stopY > startY ? stopY : startY + 1;
        _pixels = new Rgbw[PhysicalLength];
        CurrentPalette = new Palette16();
        Colors[0] = Rgbw.Black;
        Colors[1] = Rgbw.Black;
        Colors[2] = Rgbw.Black;
    }

    /// <summary>The strip this segment belongs to; <see langword="null"/> until it is added to one.</summary>
    public LedStrip? Strip { get; internal set; }

    // ------------------------------------------------------------------ geometry

    /// <summary>First pixel index, or the left edge on a matrix.</summary>
    public int Start { get; private set; }

    /// <summary>One past the last pixel index, or the right edge on a matrix.</summary>
    public int Stop { get; private set; }

    /// <summary>Top edge on a matrix.</summary>
    public int StartY { get; private set; }

    /// <summary>Bottom edge on a matrix.</summary>
    public int StopY { get; private set; }

    /// <summary>Phase offset applied when the segment is written to the strip; 1D effects wrap around it.</summary>
    public int Offset { get; set; }

    /// <summary>How many physical pixels each virtual pixel drives.</summary>
    public byte Grouping { get; private set; } = 1;

    /// <summary>How many physical pixels are skipped after each group.</summary>
    public byte Spacing { get; private set; }

    /// <summary>Pixels covered by one virtual pixel including the gap after it.</summary>
    public int GroupLength => Grouping + Spacing;

    /// <summary>Physical width in pixels (the whole length when 1D).</summary>
    public int PhysicalWidth => Stop > Start ? Stop - Start : 0;

    /// <summary>Physical height in pixels; always at least 1.</summary>
    public int PhysicalHeight => StopY - StartY;

    /// <summary>Physical pixel count.</summary>
    public int PhysicalLength => PhysicalWidth * PhysicalHeight;

    /// <summary>Width of the matrix the segment lives on.</summary>
    public int MatrixWidth => Strip?.MatrixWidth ?? PhysicalWidth;

    /// <summary>Height of the matrix the segment lives on.</summary>
    public int MatrixHeight => Strip?.MatrixHeight ?? 1;

    /// <summary>True when the segment spans more than one pixel in both directions.</summary>
    public bool Is2D => PhysicalWidth > 1 && PhysicalHeight > 1;

    /// <summary>True when the segment has a usable pixel buffer.</summary>
    public bool IsActive => Stop > Start && _pixels.Length > 0;

    // -------------------------------------------------------------------- options

    /// <summary>Whether the UI has this segment selected.</summary>
    public bool Selected { get; set; } = true;

    /// <summary>Mirrors the effect back on itself along X.</summary>
    public bool Mirror { get; set; }

    /// <summary>Mirrors the effect back on itself along Y.</summary>
    public bool MirrorY { get; set; }

    /// <summary>Draws the segment right to left.</summary>
    public bool Reverse { get; set; }

    /// <summary>Draws the segment bottom to top.</summary>
    public bool ReverseY { get; set; }

    /// <summary>Swaps the X and Y axes.</summary>
    public bool Transpose { get; set; }

    /// <summary>Whether the segment is lit at all.</summary>
    public bool On { get; set; } = true;

    /// <summary>Freezes the effect on its current frame without blanking it.</summary>
    public bool Freeze { get; set; }

    /// <summary>How a 1D effect is expanded across a 2D segment.</summary>
    public Mapping1D2D Map1D2D { get; set; } = Mapping1D2D.Pixels;

    /// <summary>Which of the sound simulation flavours audio-reactive effects should use.</summary>
    public byte SoundSim { get; set; }

    /// <summary>UI grouping slot, 0-3.</summary>
    public byte Set { get; set; }

    /// <summary>How this segment is combined with what is already on the strip.</summary>
    public BlendMode BlendMode { get; set; }

    /// <summary>Optional segment name.</summary>
    public string? Name { get; set; }

    // ------------------------------------------------------------- effect settings

    /// <summary>The three colour slots the effect draws with.</summary>
    public Rgbw[] Colors { get; } = new Rgbw[ColorCount];

    /// <summary>Effect ID; see <see cref="EffectRegistry"/>.</summary>
    public byte Mode { get; private set; }

    /// <summary>Palette ID; see <see cref="Palettes"/>.</summary>
    public byte Palette { get; private set; }

    /// <summary>The speed slider, 0-255.</summary>
    public byte Speed { get; set; } = 128;

    /// <summary>The intensity slider, 0-255.</summary>
    public byte Intensity { get; set; } = 128;

    /// <summary>Custom slider 1, 0-255.</summary>
    public byte Custom1 { get; set; } = 128;

    /// <summary>Custom slider 2, 0-255.</summary>
    public byte Custom2 { get; set; } = 128;

    private byte _custom3 = 16;

    /// <summary>Custom slider 3; reduced range, 0-31.</summary>
    public byte Custom3
    {
        get => _custom3;
        set => _custom3 = (byte)(value & 0x1F);
    }

    /// <summary>Custom checkbox 1.</summary>
    public bool Check1 { get; set; }

    /// <summary>Custom checkbox 2.</summary>
    public bool Check2 { get; set; }

    /// <summary>Custom checkbox 3.</summary>
    public bool Check3 { get; set; }

    /// <summary>Segment brightness, 0-255.</summary>
    public byte Opacity { get; set; } = 255;

    /// <summary>Correlated colour temperature, 0 == 1900K and 255 == 10091K.</summary>
    public byte Cct { get; set; } = 127;

    // --------------------------------------------------------------- runtime state

    /// <summary>Free-form per-effect counter that survives between frames.</summary>
    public uint Step { get; set; }

    /// <summary>How many frames this effect has rendered since the last reset.</summary>
    public uint Call { get; set; }

    /// <summary>Free-form per-effect variable that survives between frames.</summary>
    public ushort Aux0 { get; set; }

    /// <summary>Free-form per-effect variable that survives between frames.</summary>
    public ushort Aux1 { get; set; }

    /// <summary>Set to have the runtime state cleared before the next frame.</summary>
    public bool NeedsReset { get; set; }

    /// <summary>Common time base for effects, in milliseconds.</summary>
    public uint Now => Strip?.Now ?? Clock.Millis;

    /// <summary>Nominal duration of one frame, in milliseconds.</summary>
    public int FrameTime => Strip?.FrameTime ?? 23;

    /// <summary>
    /// Returns a per-effect state buffer of <paramref name="count"/> elements, allocating it on the
    /// first call and handing back the same array on every later frame.
    /// </summary>
    /// <remarks>
    /// This replaces the untyped <c>SEGENV.allocateData()</c> byte blob of the firmware: effects
    /// declare the struct they want and the engine keeps it alive for them. Changing the element
    /// type or the count re-allocates, so a resized segment starts from a cleared buffer.
    /// </remarks>
    public T[] GetData<T>(int count) where T : struct
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (_data is T[] existing && existing.Length == count) return existing;
        var fresh = new T[count];
        _data = fresh;
        return fresh;
    }

    /// <summary>
    /// Returns a per-effect state buffer of reference-typed objects, creating them with
    /// <paramref name="factory"/> on the first call. The companion to <see cref="GetData{T}"/> for
    /// the few effects whose state is an object rather than a value - a palette, say.
    /// </summary>
    public T[] GetObjects<T>(int count, Func<T> factory) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (_data is T[] existing && existing.Length == count)
        {
            // a reset clears the buffer, which for a reference type means nulling it out
            for (int i = 0; i < count; i++) existing[i] ??= factory();
            return existing;
        }
        var fresh = new T[count];
        for (int i = 0; i < count; i++) fresh[i] = factory();
        _data = fresh;
        return fresh;
    }

    /// <summary>True when a state buffer of exactly this shape is already allocated.</summary>
    public bool HasData<T>(int count) where T : struct => _data is T[] existing && existing.Length == count;

    /// <summary>Drops the per-effect state buffer.</summary>
    public void ClearData() => _data = null;

    /// <summary>
    /// Clears runtime state if a reset was requested. Called by the engine before the effect runs,
    /// never from inside an effect.
    /// </summary>
    public void ResetIfRequired()
    {
        if (!NeedsReset || !IsActive) return;
        switch (_data)
        {
            case Array array: Array.Clear(array); break;
        }
        Array.Clear(_pixels);
        Step = 0;
        Call = 0;
        Aux0 = 0;
        Aux1 = 0;
        NeedsReset = false;
    }

    /// <summary>Requests a reset before the next frame. Safe to call from any thread.</summary>
    public Segment MarkForReset()
    {
        NeedsReset = true;
        return this;
    }

    // ------------------------------------------------------- current draw context

    /// <summary>Virtual width, i.e. what the effect sees after grouping, spacing and mirroring.</summary>
    public int Width { get; private set; } = 1;

    /// <summary>Virtual height, i.e. what the effect sees after grouping, spacing and mirroring.</summary>
    public int Height { get; private set; } = 1;

    /// <summary>
    /// Virtual length - the pixel count an effect should iterate over. This is the C++ <c>SEGLEN</c>.
    /// </summary>
    public int Length { get; private set; } = 1;

    /// <summary>The palette the effect should sample, already blended if a transition is running.</summary>
    public Palette16 CurrentPalette { get; private set; }

    private readonly Rgbw[] _currentColors = new Rgbw[ColorCount];

    /// <summary>
    /// The colour in slot <paramref name="slot"/> as the effect should use it - blended with the
    /// previous colour while a transition runs. This is the C++ <c>SEGCOLOR()</c> macro.
    /// </summary>
    public Rgbw Color(int slot) => _currentColors[(uint)slot < ColorCount ? slot : 0];

    /// <summary>Recomputes the virtual dimensions. Call before touching pixels outside a frame.</summary>
    public void SetDrawDimensions()
    {
        Width = VirtualWidth();
        Height = VirtualHeight();
        Length = VirtualLength();
    }

    /// <summary>
    /// Prepares the draw context for one frame: virtual dimensions, colours and palette, blending
    /// each of them with the pre-transition values when a transition is in flight.
    /// </summary>
    public void BeginDraw(ushort progress = 0xFFFF)
    {
        SetDrawDimensions();
        for (int i = 0; i < ColorCount; i++) _currentColors[i] = Colors[i];
        CurrentPalette = LoadPalette(Palette);

        if (_transition is not { } t || progress >= 0xFFFF || Strip?.BlendingStyle != TransitionStyle.Fade) return;

        for (int i = 0; i < ColorCount; i++) _currentColors[i] = Rgbw.Blend16(t.Colors[i], Colors[i], progress);

        // roughly 255 passes of 48 channel steps morph one palette fully into another
        int blends = (255 * progress / 0xFFFF) - t.PreviousPaletteBlends;
        if (blends > 255) blends = 255;
        for (int i = 0; i < blends; i++, t.PreviousPaletteBlends++) t.Palette.BlendToward(CurrentPalette, 48);
        CurrentPalette = t.Palette;
    }

    // -------------------------------------------------------------------- palette

    /// <summary>
    /// Resolves a palette ID into an actual palette. IDs 0-5 are derived from the segment colours,
    /// everything else comes from <see cref="Palettes"/>.
    /// </summary>
    public Palette16 LoadPalette(byte id)
    {
        if (id == 0) id = _defaultPalette; // effects can nominate a better default in SetMode()

        if (id >= Palettes.FixedCount)
        {
            if (id > Palettes.CustomIdBase)
            {
                if (Palettes.UsermodIdBase - id >= Palettes.Registered.Count) id = 0;
            }
            else if (Palettes.CustomIdBase - id >= Palettes.Custom.Count) id = 0;
        }

        switch (id)
        {
            case 0: return Palettes.PartyColors;
            case 1: return RandomPalette;
            case 2: return new Palette16((Crgb)Colors[0]);
            case 3:
            {
                var prim = (Crgb)Colors[0];
                var sec = (Crgb)Colors[1];
                return new Palette16(prim, prim, sec, sec);
            }
            case 4: return new Palette16((Crgb)Colors[2], (Crgb)Colors[1], (Crgb)Colors[0]);
            case 5:
            {
                var prim = (Crgb)Colors[0];
                var sec = (Crgb)Colors[1];
                if (!Colors[2].IsBlack)
                {
                    var ter = (Crgb)Colors[2];
                    return new Palette16([prim, prim, prim, prim, prim, sec, sec, sec, sec, sec, ter, ter, ter, ter, ter, prim]);
                }
                return new Palette16([prim, prim, prim, prim, prim, prim, prim, prim, sec, sec, sec, sec, sec, sec, sec, sec]);
            }
            default:
                if (id > Palettes.CustomIdBase) return Palettes.Registered[Palettes.UsermodIdBase - id];
                if (id >= Palettes.FixedCount) return Palettes.Custom[Palettes.CustomIdBase - id];
                return Palettes.Get(id);
        }
    }

    /// <summary>The palette that "Random Cycle" (palette 1) is currently showing.</summary>
    public static Palette16 RandomPalette { get; private set; } = Palette16.Random();

    private static Palette16 _targetRandomPalette = Palette16.Random();
    private static uint _lastRandomPaletteChange;
    private static uint _nextRandomPaletteBlend;

    /// <summary>How long "Random Cycle" waits before picking a new palette, in seconds.</summary>
    public static int RandomPaletteChangeTime { get; set; } = 5;

    /// <summary>Whether "Random Cycle" picks harmonically related palettes rather than fully random ones.</summary>
    public static bool UseHarmonicRandomPalette { get; set; } = true;

    /// <summary>
    /// Advances the "Random Cycle" palette. The engine calls this once per shown frame; the palette
    /// morphs towards its successor over the transition time rather than snapping.
    /// </summary>
    public static void HandleRandomPalette(int frameTime, int transitionTime)
    {
        uint now = Clock.Millis;
        uint nowSeconds = now / 1000;
        if (nowSeconds < _lastRandomPaletteChange) _lastRandomPaletteChange = 0; // clock wrapped

        if (nowSeconds > _lastRandomPaletteChange + RandomPaletteChangeTime)
        {
            _targetRandomPalette = UseHarmonicRandomPalette
                ? Palette16.RandomHarmonic(RandomPalette)
                : Palette16.Random();
            _lastRandomPaletteChange = nowSeconds;
            _nextRandomPaletteBlend = now; // start blending straight away
        }

        if (now < _nextRandomPaletteBlend || now > _lastRandomPaletteChange * 1000 + transitionTime + 2 * frameTime) return;

        int transitionFrames = frameTime > transitionTime ? 1 : transitionTime / frameTime;
        int blends = transitionFrames > 255 ? 1 : (255 + (transitionFrames >> 1)) / transitionFrames;
        for (int i = 0; i < blends; i++) RandomPalette.BlendToward(_targetRandomPalette, 48);
        _nextRandomPaletteBlend = now + (uint)((transitionFrames >> 8) * frameTime);
    }

    // ----------------------------------------------------------------- transitions

    private sealed class TransitionState(int durationMs)
    {
        public uint Start { get; } = Clock.Millis;
        public Rgbw[] Colors { get; } = new Rgbw[ColorCount];
        public Palette16 Palette { get; set; } = new();
        public int Duration { get; set; } = durationMs;
        public ushort Progress { get; set; }
        public int PreviousPaletteBlends { get; set; }
        public byte StartPalette { get; init; }
        public byte StartBrightness { get; init; }
        public byte StartCct { get; init; }
        public Segment? OldSegment { get; set; }
    }

    private TransitionState? _transition;

    /// <summary>True while this segment is cross-fading from a previous look.</summary>
    public bool IsInTransition => _transition is not null;

    /// <summary>Transition progress, 0..65535; 65535 when no transition is running.</summary>
    public ushort Progress => _transition?.Progress ?? 0xFFFF;

    /// <summary>
    /// The pre-transition copy of this segment, kept so the outgoing effect can keep rendering
    /// during a wipe or push. Null when the transition is a plain cross-fade.
    /// </summary>
    public Segment? OldSegment => _transition?.OldSegment;

    /// <summary>
    /// Captures the current look so the next change fades out of it. Must be called
    /// <em>before</em> the segment values change.
    /// </summary>
    /// <param name="durationMs">Fade duration; 0 makes the change immediate.</param>
    /// <param name="copySegment">
    /// Whether to snapshot the whole segment. Needed when the effect itself changes or when the
    /// blending style is not a plain fade, because the outgoing effect has to keep drawing.
    /// </param>
    public void StartTransition(int durationMs, bool copySegment = true)
    {
        if (durationMs == 0 || !IsActive)
        {
            if (_transition is { } running) running.Duration = 0;
            return;
        }
        if (_transition is not null) return; // already fading; let the running one finish

        var t = new TransitionState(durationMs)
        {
            StartPalette = Palette,
            StartBrightness = On ? Opacity : (byte)0,
            StartCct = Cct,
        };
        for (int i = 0; i < ColorCount; i++) t.Colors[i] = Colors[i];
        t.Palette = LoadPalette(Palette).Clone();
        if (copySegment) t.OldSegment = CloneForTransition();
        _transition = t;
    }

    /// <summary>Ends any running transition immediately.</summary>
    public void StopTransition() => _transition = null;

    /// <summary>Recomputes transition progress and retires the transition once it completes.</summary>
    public void HandleTransition()
    {
        if (_transition is not { } t) return;
        uint elapsed = Clock.Millis - t.Start;
        t.Progress = elapsed >= t.Duration || t.Duration == 0
            ? (ushort)0xFFFF
            : (ushort)(elapsed * 0xFFFF / (uint)t.Duration);
        if (t.Progress == 0xFFFF) StopTransition();
    }

    /// <summary>Opacity as it should be applied this frame, faded while a transition runs.</summary>
    public byte CurrentBrightness()
    {
        byte target = On ? Opacity : (byte)0;
        if (_transition is not { } t || Strip?.BlendingStyle != TransitionStyle.Fade) return target;
        return FastMath.Lerp8By8(t.StartBrightness, target, (byte)(t.Progress >> 8));
    }

    /// <summary>CCT as it should be applied this frame, faded while a transition runs.</summary>
    public byte CurrentCct()
    {
        if (_transition is not { } t) return Cct;
        return FastMath.Lerp8By8(t.StartCct, Cct, (byte)(t.Progress >> 8));
    }

    private Segment CloneForTransition()
    {
        var copy = new Segment(Start, Stop, StartY, StopY)
        {
            Strip = Strip,
            Offset = Offset,
            Grouping = Grouping,
            Spacing = Spacing,
            Opacity = Opacity,
            Cct = Cct,
            Mode = Mode,
            Palette = Palette,
            Speed = Speed,
            Intensity = Intensity,
            Custom1 = Custom1,
            Custom2 = Custom2,
            Custom3 = Custom3,
            Check1 = Check1,
            Check2 = Check2,
            Check3 = Check3,
            Mirror = Mirror,
            MirrorY = MirrorY,
            Reverse = Reverse,
            ReverseY = ReverseY,
            Transpose = Transpose,
            On = On,
            Map1D2D = Map1D2D,
            BlendMode = BlendMode,
            Name = Name,
            Step = Step,
            Call = Call,
            Aux0 = Aux0,
            Aux1 = Aux1,
            _defaultPalette = _defaultPalette,
        };
        Colors.CopyTo(copy.Colors, 0);
        _pixels.CopyTo(copy._pixels, 0);
        return copy;
    }

    // -------------------------------------------------------------------- mutators

    /// <summary>Sets one of the three colour slots, starting a transition if the strip wants one.</summary>
    public Segment SetColor(int slot, Rgbw color)
    {
        if ((uint)slot >= ColorCount || Colors[slot] == color) return this;
        StartTransition(Strip?.TransitionDuration ?? 0, false);
        Colors[slot] = color;
        return this;
    }

    /// <summary>Sets the segment brightness, starting a transition if the strip wants one.</summary>
    public Segment SetOpacity(byte opacity)
    {
        if (Opacity == opacity) return this;
        StartTransition(Strip?.TransitionDuration ?? 0, false);
        Opacity = opacity;
        return this;
    }

    /// <summary>Sets the colour temperature, either as 0-255 or as a value in Kelvin.</summary>
    public Segment SetCct(int kelvin)
    {
        if (kelvin > 255) kelvin = FastMath.Clamp((kelvin - 1900) >> 5, 0, 255); // 1900K..10060K
        if (Cct == kelvin) return this;
        StartTransition(Strip?.TransitionDuration ?? 0, false);
        Cct = (byte)kelvin;
        return this;
    }

    /// <summary>
    /// Switches to another effect, optionally resetting the sliders to that effect defaults.
    /// The segment is reset so the incoming effect starts from clean state.
    /// </summary>
    public Segment SetMode(byte mode, bool loadDefaults = false)
    {
        if (mode >= EffectRegistry.Count) mode = 0;
        if (mode == Mode) return this;

        StartTransition(Strip?.TransitionDuration ?? 0);
        Mode = mode;

        EffectInfo info = EffectRegistry.Get(mode);
        if (loadDefaults)
        {
            EffectDefaults defaults = info.Metadata.Defaults;
            Speed = defaults.Speed ?? 128;
            Intensity = defaults.Intensity ?? 128;
            Custom1 = defaults.Custom1 ?? 128;
            Custom2 = defaults.Custom2 ?? 128;
            Custom3 = defaults.Custom3 ?? 16;
            Check1 = defaults.Check1 ?? false;
            Check2 = defaults.Check2 ?? false;
            Check3 = defaults.Check3 ?? false;
            if (defaults.Map1D2D is { } m) Map1D2D = m;
            if (defaults.Palette is { } p) Palette = p;
        }
        _defaultPalette = info.Metadata.Defaults.Palette ?? 6;
        MarkForReset();
        return this;
    }

    /// <summary>Switches to another palette.</summary>
    public Segment SetPalette(byte palette)
    {
        if (palette == Palette) return this;
        StartTransition(Strip?.TransitionDuration ?? 0, false);
        Palette = palette;
        return this;
    }

    /// <summary>
    /// Moves or resizes the segment. The pixel buffer is re-allocated when the size changes, so any
    /// in-flight effect state is dropped.
    /// </summary>
    public void SetGeometry(int start, int stop, byte grouping = 1, byte spacing = 0, int? offset = null,
                            int startY = 0, int stopY = 1, Mapping1D2D? map1D2D = null)
    {
        bool boundsUnchanged = Start == start && Stop == stop && StartY == startY && StopY == stopY;
        if (boundsUnchanged && Grouping == grouping && Spacing == spacing && map1D2D is null && offset is null) return;

        if (stop == 0) // deactivating the segment
        {
            Stop = 0;
            _pixels = [];
            return;
        }

        Start = start;
        Stop = System.Math.Max(stop, start + 1);
        StartY = startY;
        StopY = System.Math.Max(stopY, startY + 1);
        Grouping = System.Math.Max(grouping, (byte)1);
        Spacing = spacing;
        if (offset is { } o) Offset = o;
        if (map1D2D is { } m) Map1D2D = m;

        if (_pixels.Length != PhysicalLength) _pixels = new Rgbw[PhysicalLength];
        else Array.Clear(_pixels);
        SetDrawDimensions();
        MarkForReset();
    }

    /// <summary>Turns the segment off and releases its pixel buffer.</summary>
    public void Deactivate() => SetGeometry(0, 0);

    // ---------------------------------------------------------- virtual dimensions

    /// <summary>Computes the virtual width from the current geometry.</summary>
    public int VirtualWidth()
    {
        int groupLen = GroupLength;
        int width = ((Transpose ? PhysicalHeight : PhysicalWidth) + groupLen - 1) / groupLen;
        if (Mirror) width = (width + 1) / 2; // keep at least one pixel
        return width;
    }

    /// <summary>Computes the virtual height from the current geometry.</summary>
    public int VirtualHeight()
    {
        int groupLen = GroupLength;
        int height = ((Transpose ? PhysicalWidth : PhysicalHeight) + groupLen - 1) / groupLen;
        if (MirrorY) height = (height + 1) / 2;
        return height;
    }

    /// <summary>
    /// Computes the virtual length: on a matrix this depends on how a 1D effect is mapped onto it.
    /// </summary>
    public int VirtualLength()
    {
        if (Is2D)
        {
            int vW = VirtualWidth();
            int vH = VirtualHeight();
            return Map1D2D switch
            {
                Mapping1D2D.Bar => vH,
                Mapping1D2D.Corner => System.Math.Max(vW, vH),
                Mapping1D2D.Arc => (int)FastMath.Sqrt32((uint)(vH * vH + vW * vW)), // the diagonal
                Mapping1D2D.Pinwheel => PinwheelLength(vW, vH),
                _ => vW * vH,
            };
        }

        int groupLen = GroupLength;
        int length = (PhysicalLength + groupLen - 1) / groupLen;
        if (Mirror) length = (length + 1) / 2;
        return length;
    }

    /// <summary>
    /// Number of virtual vertical strips a 1D effect can spread across. Greater than one only in
    /// <see cref="Mapping1D2D.Bar"/> mode, where each column runs its own copy of the effect.
    /// </summary>
    public int VerticalStripCount => Is2D && Map1D2D == Mapping1D2D.Bar ? VirtualWidth() : 1;

    /// <summary>Size of the raw pixel buffer actually in use this frame.</summary>
    public int RawLength => Is2D ? Width * Height : Length;

    /// <summary>The longest a 1D mapping can get on this segment; used to size effect buffers.</summary>
    public int MaxMappingLength()
    {
        int vW = VirtualWidth(), vH = VirtualHeight();
        return System.Math.Max((int)FastMath.Sqrt32((uint)(vH * vH + vW * vW)), PinwheelLength(vW, vH));
    }
}
