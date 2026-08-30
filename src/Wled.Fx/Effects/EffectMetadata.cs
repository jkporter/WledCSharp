namespace Wled.Fx;

/// <summary>Which dimensionalities an effect supports.</summary>
[Flags]
public enum EffectDimensions
{
    /// <summary>Not restricted.</summary>
    Any = 0,
    /// <summary>Runs on a strip.</summary>
    OneDimensional = 1,
    /// <summary>Runs on a matrix.</summary>
    TwoDimensional = 2,
    /// <summary>Uses a volume reading from an audio source.</summary>
    Volume = 4,
    /// <summary>Uses a frequency reading from an audio source.</summary>
    Frequency = 8,
}

/// <summary>
/// The slider, colour and option defaults an effect declares in its metadata string.
/// </summary>
/// <remarks>
/// Any value left null means "keep the engine default", which is what
/// <see cref="Segment.SetMode"/> falls back to.
/// </remarks>
public sealed record EffectDefaults
{
    public byte? Speed { get; init; }
    public byte? Intensity { get; init; }
    public byte? Custom1 { get; init; }
    public byte? Custom2 { get; init; }
    public byte? Custom3 { get; init; }
    public bool? Check1 { get; init; }
    public bool? Check2 { get; init; }
    public bool? Check3 { get; init; }
    public Mapping1D2D? Map1D2D { get; init; }
    public byte? SoundSim { get; init; }
    public bool? Reverse { get; init; }
    public bool? Mirror { get; init; }
    public bool? ReverseY { get; init; }
    public bool? MirrorY { get; init; }

    /// <summary>Palette to select, and the palette that stands in for "Default" (palette 0).</summary>
    public byte? Palette { get; init; }
}

/// <summary>
/// A parsed WLED effect metadata string - the UI description that ships alongside every effect.
/// </summary>
/// <remarks>
/// <para>The format is</para>
/// <code>Name@slider labels;colour labels;palette label;flags;defaults</code>
/// <para>
/// for example <c>Blink@!,Duty cycle;!,!;!;01</c>. An empty label hides the control, <c>!</c> asks
/// for the standard label, the flags carry the supported dimensions, and the defaults are
/// <c>key=value</c> pairs such as <c>sx=128,pal=11,m12=1</c>. See
/// https://kno.wled.ge/interfaces/json-api/#effect-metadata for the full specification.
/// </para>
/// </remarks>
public sealed class EffectMetadata
{
    /// <summary>The raw metadata string, exactly as the C++ effect declares it.</summary>
    public string Raw { get; }

    /// <summary>Display name.</summary>
    public string Name { get; }

    /// <summary>Labels for the speed, intensity and custom sliders, in order.</summary>
    public IReadOnlyList<string> SliderLabels { get; }

    /// <summary>Labels for the three colour slots, in order.</summary>
    public IReadOnlyList<string> ColorLabels { get; }

    /// <summary>Label for the palette selector, empty when the effect ignores palettes.</summary>
    public string PaletteLabel { get; }

    /// <summary>Which dimensionalities and data sources the effect supports.</summary>
    public EffectDimensions Dimensions { get; }

    /// <summary>Slider and option defaults.</summary>
    public EffectDefaults Defaults { get; }

    /// <summary>True when this ID is a placeholder rather than a real effect.</summary>
    public bool IsReserved => Raw.StartsWith("RSVD", StringComparison.Ordinal);

    private EffectMetadata(string raw, string name, string[] sliders, string[] colors, string palette,
                           EffectDimensions dimensions, EffectDefaults defaults)
    {
        Raw = raw;
        Name = name;
        SliderLabels = sliders;
        ColorLabels = colors;
        PaletteLabel = palette;
        Dimensions = dimensions;
        Defaults = defaults;
    }

    /// <summary>Parses a metadata string; a bare name with no <c>@</c> is valid and common.</summary>
    public static EffectMetadata Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        int at = raw.IndexOf('@');
        string name = at < 0 ? raw : raw[..at];
        string[] sections = at < 0 ? [] : raw[(at + 1)..].Split(';');

        string[] sliders = Section(sections, 0).Split(',', StringSplitOptions.None);
        string[] colors = Section(sections, 1).Split(',', StringSplitOptions.None);
        string palette = Section(sections, 2);
        string flags = Section(sections, 3);
        string defaults = Section(sections, 4);

        return new EffectMetadata(raw, name, Clean(sliders), Clean(colors), palette,
                                  ParseFlags(flags), ParseDefaults(defaults));

        static string Section(string[] parts, int index) => index < parts.Length ? parts[index] : string.Empty;

        static string[] Clean(string[] labels)
            => labels.Length == 1 && labels[0].Length == 0 ? [] : labels;
    }

    private static EffectDimensions ParseFlags(string flags)
    {
        EffectDimensions dimensions = EffectDimensions.Any;
        foreach (char c in flags)
        {
            dimensions |= c switch
            {
                '1' => EffectDimensions.OneDimensional,
                '2' => EffectDimensions.TwoDimensional,
                'v' => EffectDimensions.Volume,
                'f' => EffectDimensions.Frequency,
                _ => EffectDimensions.Any,
            };
        }
        return dimensions;
    }

    private static EffectDefaults ParseDefaults(string defaults)
    {
        if (defaults.Length == 0) return new EffectDefaults();

        byte? sx = null, ix = null, c1 = null, c2 = null, c3 = null, si = null, pal = null;
        bool? o1 = null, o2 = null, o3 = null, rev = null, mi = null, rY = null, mY = null;
        Mapping1D2D? m12 = null;

        foreach (string pair in defaults.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            string key = pair[..eq].Trim();
            if (!int.TryParse(pair[(eq + 1)..].Trim(), out int value)) continue;

            switch (key)
            {
                case "sx": sx = Byte(value); break;
                case "ix": ix = Byte(value); break;
                case "c1": c1 = Byte(value); break;
                case "c2": c2 = Byte(value); break;
                case "c3": c3 = Byte(value); break;
                case "o1": o1 = value != 0; break;
                case "o2": o2 = value != 0; break;
                case "o3": o3 = value != 0; break;
                case "m12": m12 = (Mapping1D2D)FastMath.Clamp(value, 0, 4); break;
                case "si": si = Byte(FastMath.Clamp(value, 0, 3)); break;
                case "rev": rev = value != 0; break;
                case "mi": mi = value != 0; break;
                case "rY": rY = value != 0; break;
                case "mY": mY = value != 0; break;
                case "pal": pal = Byte(value); break;
            }
        }

        return new EffectDefaults
        {
            Speed = sx, Intensity = ix, Custom1 = c1, Custom2 = c2, Custom3 = c3,
            Check1 = o1, Check2 = o2, Check3 = o3,
            Map1D2D = m12, SoundSim = si, Reverse = rev, Mirror = mi, ReverseY = rY, MirrorY = mY,
            // palette 0 means "unset"; the engine then falls back to Party colours
            Palette = pal is > 0 ? pal : null,
        };

        static byte Byte(int value) => (byte)FastMath.Clamp(value, 0, 255);
    }

    public override string ToString() => Name;
}
