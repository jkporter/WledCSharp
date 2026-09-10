namespace Wled.Fx.Demo;

/// <summary>
/// What the host publishes for <see cref="ProgressEffect"/> to draw: how far along a job is.
/// </summary>
/// <remarks>
/// A record rather than a mutable class on purpose. Publishing a fresh instance is the whole update
/// protocol, so the worker thread reporting progress and the render thread reading it never need to
/// agree on a lock - the effect always sees one consistent snapshot.
/// </remarks>
/// <param name="Fraction">How much of the job is done, 0 to 1.</param>
/// <param name="Indeterminate">True when the total is unknown, so the bar should sweep instead.</param>
internal sealed record ProgressData(float Fraction, bool Indeterminate = false) : IModuleData;

/// <summary>
/// A progress bar driven by data from outside the engine, showing how an effect reaches live host
/// state given that it is handed nothing but its <see cref="Segment"/>.
/// </summary>
/// <remarks>
/// The lookup mirrors the firmware's audio effects: pull the module data by ID every frame, and fall
/// back to a simulation when nothing is publishing so the effect still previews in a mode list.
/// </remarks>
internal static class ProgressEffect
{
    /// <summary>The module ID this effect and its host agree on.</summary>
    public const byte ProgressModule = ModuleId.UserBase;

    /// <summary>
    /// The effect ID claimed here. Every ID up to 219 is spoken for by the WLED protocol, so a
    /// custom effect necessarily takes over one that is not ported - 200 is Particle Ghost Rider.
    /// </summary>
    public const byte Id = 200;

    /// <summary>Adds the effect to the registry; call once at start-up.</summary>
    public static void Register()
        => EffectRegistry.Register(Id, "Progress@Ease,,,,Sweep width;!,!;!;1;sx=16,c3=6", Render);

    /// <summary>Draws one frame of the bar.</summary>
    public static void Render(Segment seg)
    {
        ProgressData progress = GetProgress(seg);
        if (progress.Indeterminate)
        {
            DrawSweep(seg);
            return;
        }

        int target = (int)MathF.Round(System.Math.Clamp(progress.Fraction, 0f, 1f) * seg.Length);
        int size = 1 + ((seg.Speed * seg.Length) >> 11); // how far the bar may travel in one frame

        for (int i = 0; i < seg.Length; i++)
        {
            seg.SetPixelColor(i, i < seg.Aux1
                ? seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0)
                : seg.Color(1));
        }

        // ease towards the reported level rather than snapping to it, as Percent does
        if (target > seg.Aux1) seg.Aux1 = (ushort)System.Math.Min(seg.Aux1 + size, target);
        else if (target < seg.Aux1)
            seg.Aux1 = (ushort)System.Math.Max(seg.Aux1 > size ? seg.Aux1 - size : 0, target);
    }

    /// <summary>
    /// The <c>getAudioData()</c> shape: read the published data, and never return null, so the
    /// effect draws something whether or not a host is attached.
    /// </summary>
    private static ProgressData GetProgress(Segment seg)
        => seg.GetModuleData<ProgressData>(ProgressModule) ?? Simulate(seg);

    /// <summary>Stands in for a host, the way <c>simulateSound()</c> does for the audio feed.</summary>
    private static ProgressData Simulate(Segment seg) => new(seg.Now % 4000 / 4000f);

    /// <summary>A bar bouncing back and forth, for a job whose total is not known.</summary>
    private static void DrawSweep(Segment seg)
    {
        int width = System.Math.Max(1, seg.Length * System.Math.Max(1, (int)seg.Custom3) / 32);
        int travel = System.Math.Max(0, seg.Length - width);
        int start = Beat.Sin8((uint)(8 + (seg.Speed >> 2))) * travel / 255;

        for (int i = 0; i < seg.Length; i++)
        {
            seg.SetPixelColor(i, i >= start && i < start + width
                ? seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0)
                : seg.Color(1));
        }
    }
}
