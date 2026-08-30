namespace Wled.Fx;

/// <summary>
/// The built-in palette catalogue. Port of <c>palettes.cpp</c> plus the palette ID scheme
/// documented in <c>const.h</c>.
/// </summary>
/// <remarks>
/// <para>Palette IDs are laid out as:</para>
/// <list type="bullet">
///   <item><description>0-5 dynamic palettes, derived per segment from its colours (see <see cref="Segment.LoadPalette"/>)</description></item>
///   <item><description>6-12 the FastLED palettes</description></item>
///   <item><description>13-71 the cpt-city gradient palettes</description></item>
///   <item><description>72-200 user custom palettes, growing downward from 200</description></item>
///   <item><description>201-255 palettes registered at runtime, growing downward from 255</description></item>
/// </list>
/// <para>
/// The cpt-city palettes are gamma corrected with (1.182, 1.0, 1.136) and the FastLED ones carry an
/// inverse gamma of 2.2, exactly as shipped, so colours match the firmware once the global gamma is
/// applied.
/// </para>
/// </remarks>
public static partial class Palettes
{
    /// <summary>Number of palettes derived from the segment colours (IDs 0-5).</summary>
    public const int DynamicCount = 6;

    /// <summary>Number of FastLED palettes (IDs 6-12).</summary>
    public const int FastLedCount = 7;

    /// <summary>Number of cpt-city gradient palettes (IDs 13-71).</summary>
    public const int GradientCount = 59;

    /// <summary>Total number of built-in palettes.</summary>
    public const int FixedCount = DynamicCount + FastLedCount + GradientCount;

    /// <summary>Highest ID reserved for runtime-registered palettes.</summary>
    public const int UsermodIdBase = 255;

    /// <summary>Highest ID reserved for user custom palettes.</summary>
    public const int CustomIdBase = 200;

    // --------------------------------------------------------- FastLED palettes

    private static Crgb C(uint code) => new(code);

    /// <summary>Party colours, gamma corrected. This is also the fallback for palette 0.</summary>
    public static readonly Palette16 PartyColors = FromCodes(
        0x9B00D5, 0xBD00B8, 0xDA0092, 0xF3005C,
        0xF45500, 0xDC8F00, 0xD5B400, 0xD5D500,
        0xD59B00, 0xEF6600, 0xF90044, 0xE10086,
        0xC400B0, 0xA300CF, 0x7600E8, 0x0032FC);

    /// <summary>Rainbow colours, gamma corrected.</summary>
    public static readonly Palette16 RainbowColors = FromCodes(
        0xFF0000, 0xEB7000, 0xD59B00, 0xD5BA00,
        0xD5D500, 0x9CEB00, 0x00FF00, 0x00EB70,
        0x00D59B, 0x009CD4, 0x0000FF, 0x7000EB,
        0x9B00D5, 0xBA00BB, 0xD5009B, 0xEB0072);

    /// <summary>Rainbow colours separated by black stripes.</summary>
    public static readonly Palette16 RainbowStripeColors = FromCodes(
        0xFF0000, 0x000000, 0xD59B00, 0x000000,
        0xD5D500, 0x000000, 0x00FF00, 0x000000,
        0x00D59B, 0x000000, 0x0000FF, 0x000000,
        0x9B00D5, 0x000000, 0xD5009B, 0x000000);

    /// <summary>Blues and whites.</summary>
    public static readonly Palette16 CloudColors = FromCodes(
        0x0000FF, 0x00008B, 0x00008B, 0x00008B,
        0x00008B, 0x00008B, 0x00008B, 0x00008B,
        0x0000FF, 0x00008B, 0x87CEEB, 0x87CEEB,
        0xADD8E6, 0xFFFFFF, 0xADD8E6, 0x87CEEB);

    /// <summary>Reds and oranges over black.</summary>
    public static readonly Palette16 LavaColors = FromCodes(
        0x000000, 0x800000, 0x000000, 0x800000,
        0x8B0000, 0x8B0000, 0x800000, 0x8B0000,
        0x8B0000, 0x8B0000, 0xFF0000, 0xFFA500,
        0xFFFFFF, 0xFFA500, 0xFF0000, 0x8B0000);

    /// <summary>Blues, teals and whites.</summary>
    public static readonly Palette16 OceanColors = FromCodes(
        0x191970, 0x00008B, 0x191970, 0x000080,
        0x00008B, 0x0000CD, 0x2E8B57, 0x008080,
        0x5F9EA0, 0x0000FF, 0x008B8B, 0x6495ED,
        0x7FFFD4, 0x2E8B57, 0x00FFFF, 0x87CEFA);

    /// <summary>Greens.</summary>
    public static readonly Palette16 ForestColors = FromCodes(
        0x006400, 0x006400, 0x556B2F, 0x006400,
        0x008000, 0x228B22, 0x6B8E23, 0x008000,
        0x2E8B57, 0x66CDAA, 0x32CD32, 0x9ACD32,
        0x90EE90, 0x7CFC00, 0x66CDAA, 0x228B22);

    private static Palette16 FromCodes(params uint[] codes)
    {
        var entries = new Crgb[Palette16.Size];
        for (int i = 0; i < Palette16.Size; i++) entries[i] = C(codes[i]);
        return new Palette16(entries);
    }

    /// <summary>The FastLED palettes in ID order (6-12).</summary>
    public static readonly Palette16[] FastLed =
    [
        PartyColors, CloudColors, LavaColors, OceanColors, ForestColors, RainbowColors, RainbowStripeColors,
    ];

    // -------------------------------------------------------- gradient palettes

    /// <summary>
    /// Display names for palette IDs 0..71, in ID order.
    /// </summary>
    /// <remarks>
    /// Taken from <c>JSON_palette_names</c>, which is what the UI shows. Note that the comments in
    /// the C++ <c>gGradientPalettes</c> array disagree with it for IDs 22 and 26 ("Beach" and
    /// "Beech" are swapped there); the names below are the ones users actually see.
    /// </remarks>
    public static readonly string[] Names =
    [
        "Default", "* Random Cycle", "* Color 1", "* Colors 1&2", "* Color Gradient", "* Colors Only",
        "Party", "Cloud", "Lava", "Ocean", "Forest", "Rainbow", "Rainbow Bands",
        "Sunset", "Rivendell", "Breeze", "Red & Blue", "Yellowout", "Analogous", "Splash",
        "Pastel", "Sunset 2", "Beach", "Vintage", "Departure", "Landscape", "Beech", "Sherbet",
        "Hult", "Hult 64", "Drywet", "Jul", "Grintage", "Rewhi", "Tertiary", "Fire", "Icefire",
        "Cyane", "Light Pink", "Autumn", "Magenta", "Magred", "Yelmag", "Yelblu", "Orange & Teal",
        "Tiamat", "April Night", "Orangery", "C9", "Sakura", "Aurora", "Atlantica", "C9 2", "C9 New",
        "Temperature", "Aurora 2", "Retro Clown", "Candy", "Toxy Reaf", "Fairy Reaf", "Semi Blue",
        "Pink Candy", "Red Reaf", "Aqua Flash", "Yelblu Hot", "Lite Light", "Red Flash", "Blink Red",
        "Red Shift", "Red Tide", "Candy2", "Traffic Light",
    ];

    private static readonly Palette16?[] GradientCache = new Palette16?[GradientCount];

    /// <summary>
    /// Returns the built-in palette with the given ID (6..71). Gradient palettes are expanded on
    /// first use and cached, since expanding a gradient into 16 slots is not free.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The ID is not a fixed, non-dynamic palette.</exception>
    public static Palette16 Get(int id)
    {
        if (id < DynamicCount || id >= FixedCount)
            throw new ArgumentOutOfRangeException(nameof(id), id, $"Expected a fixed palette ID in {DynamicCount}..{FixedCount - 1}.");

        if (id < DynamicCount + FastLedCount) return FastLed[id - DynamicCount];

        int gradientIndex = id - (DynamicCount + FastLedCount);
        return GradientCache[gradientIndex] ??= Palette16.FromGradient(Gradients[gradientIndex]);
    }

    /// <summary>The display name for a built-in palette ID, or a generic label for custom IDs.</summary>
    public static string NameOf(int id) => id >= 0 && id < Names.Length ? Names[id] : $"Palette {id}";

    /// <summary>User-supplied palettes, addressed as IDs counting down from <see cref="CustomIdBase"/>.</summary>
    public static List<Palette16> Custom { get; } = [];

    /// <summary>Palettes registered by add-ons, addressed as IDs counting down from <see cref="UsermodIdBase"/>.</summary>
    public static List<Palette16> Registered { get; } = [];

    /// <summary>Total number of palettes currently addressable, including custom and registered ones.</summary>
    public static int Count => FixedCount + Custom.Count + Registered.Count;
}
