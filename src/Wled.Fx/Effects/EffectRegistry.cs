namespace Wled.Fx;

/// <summary>Renders one frame of an effect into <paramref name="seg"/>.</summary>
public delegate void EffectRenderer(Segment seg);

/// <summary>One registered effect: its ID, its UI metadata and the function that draws it.</summary>
/// <param name="Id">Effect ID, as used by <see cref="Segment.Mode"/>.</param>
/// <param name="Metadata">Parsed UI description.</param>
/// <param name="Render">The draw function.</param>
public sealed record EffectInfo(byte Id, EffectMetadata Metadata, EffectRenderer Render)
{
    /// <summary>Display name.</summary>
    public string Name => Metadata.Name;

    public override string ToString() => $"{Id}: {Name}";
}

/// <summary>
/// The table of available effects, indexed by effect ID.
/// Port of the mode table that <c>WS2812FX::setupEffectData()</c> fills in.
/// </summary>
/// <remarks>
/// IDs are fixed by the WLED protocol, so the table is pre-filled with reserved placeholders that
/// fall back to Solid and each effect claims its own slot. That keeps presets and JSON payloads
/// interchangeable with the firmware even when an effect is not ported.
/// </remarks>
public static class EffectRegistry
{
    /// <summary>Number of effect IDs the protocol defines.</summary>
    public const int ModeCount = 220;

    private static readonly EffectInfo[] Effects = new EffectInfo[ModeCount];
    private static readonly EffectMetadata ReservedMetadata = EffectMetadata.Parse("RSVD");

    static EffectRegistry()
    {
        EffectMetadata solid = EffectMetadata.Parse("Solid");
        Effects[0] = new EffectInfo(0, solid, BasicEffects.Static);
        for (byte i = 1; i < ModeCount; i++) Effects[i] = new EffectInfo(i, ReservedMetadata, BasicEffects.Static);
        RegisterBuiltIn();
    }

    /// <summary>Number of registered effect IDs, including reserved placeholders.</summary>
    public static int Count => ModeCount;

    /// <summary>Returns the effect registered for an ID; unknown IDs resolve to Solid.</summary>
    public static EffectInfo Get(int id) => (uint)id < ModeCount ? Effects[id] : Effects[0];

    /// <summary>Every effect that is actually implemented, in ID order.</summary>
    public static IEnumerable<EffectInfo> All => Effects.Where(e => !e.Metadata.IsReserved);

    /// <summary>Looks an effect up by display name, case-insensitively.</summary>
    public static EffectInfo? FindByName(string name)
        => All.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Registers (or replaces) the effect at <paramref name="id"/>.
    /// </summary>
    /// <param name="id">Effect ID; see <see cref="EffectId"/> for the well-known ones.</param>
    /// <param name="metadata">Metadata string, e.g. <c>"Blink@!,Duty cycle;!,!;!;01"</c>.</param>
    /// <param name="render">The draw function.</param>
    public static void Register(byte id, string metadata, EffectRenderer render)
    {
        ArgumentNullException.ThrowIfNull(render);
        if (id >= ModeCount) throw new ArgumentOutOfRangeException(nameof(id), id, $"Effect IDs run 0..{ModeCount - 1}.");
        Effects[id] = new EffectInfo(id, EffectMetadata.Parse(metadata), render);
    }

    private static void RegisterBuiltIn()
    {
        BasicEffects.Register();
        ChaseEffects.Register();
        SparkleEffects.Register();
        NoiseEffects.Register();
        MotionEffects.Register();
        NatureEffects.Register();
        Matrix2DEffects.Register();
        Pattern2DEffects.Register();
        SimulationEffects.Register();
    }
}
