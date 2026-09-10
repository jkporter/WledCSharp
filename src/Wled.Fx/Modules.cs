using System.Diagnostics.CodeAnalysis;

namespace Wled.Fx;

/// <summary>
/// Marker for a bag of live values published by the host for effects to read while they draw.
/// Port of <c>um_data_t</c>.
/// </summary>
/// <remarks>
/// Prefer an immutable implementation. The registry stores a reference and effects read it in the
/// middle of a frame, so publishing a fresh instance on every update hands the effect a coherent
/// snapshot without any locking; mutating a published instance in place does not.
/// </remarks>
public interface IModuleData
{
}

/// <summary>
/// Well-known module IDs, matching the <c>USERMOD_ID_*</c> constants in the firmware's
/// <c>const.h</c> so a port of a usermod keeps its identity.
/// </summary>
public static class ModuleId
{
    /// <summary>Not a module; the value a slot holds when nothing has been published.</summary>
    public const byte Reserved = 0;

    /// <summary>A module that does not claim a specific ID.</summary>
    public const byte Unspecified = 1;

    /// <summary>The AudioReactive usermod: the volume and FFT feed the audio effects want.</summary>
    public const byte AudioReactive = 32;

    /// <summary>First ID reserved for host-defined modules; the firmware assigns below this.</summary>
    public const byte UserBase = 128;
}

/// <summary>
/// The external data published for a strip, keyed by module ID. Port of the side channel that
/// <c>UsermodManager::getUmData()</c> reads.
/// </summary>
/// <remarks>
/// <para>
/// An effect is handed nothing but its <see cref="Segment"/>, so live host data - a download
/// percentage, a sensor reading, an audio spectrum - reaches it the way the firmware does it: the
/// host publishes under a module ID, and the effect looks that ID up with
/// <see cref="Segment.GetModuleData{T}"/> each frame.
/// </para>
/// <para>
/// Publishing is a single reference store, so a host thread may publish while the render thread is
/// mid-frame: the effect sees either the old instance or the new one, never a half-written value.
/// </para>
/// </remarks>
public sealed class ModuleRegistry
{
    private readonly IModuleData?[] _modules = new IModuleData?[256];

    /// <summary>Number of module IDs currently publishing.</summary>
    public int Count => _modules.Count(m => m is not null);

    /// <summary>Publishes <paramref name="data"/> under <paramref name="moduleId"/>, replacing whatever was there.</summary>
    public void Publish(byte moduleId, IModuleData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Volatile.Write(ref _modules[moduleId], data);
    }

    /// <summary>Stops publishing <paramref name="moduleId"/>; returns whether anything was there.</summary>
    public bool Remove(byte moduleId)
    {
        bool published = Volatile.Read(ref _modules[moduleId]) is not null;
        Volatile.Write(ref _modules[moduleId], null);
        return published;
    }

    /// <summary>Returns what <paramref name="moduleId"/> is publishing, or null.</summary>
    public IModuleData? Get(byte moduleId) => Volatile.Read(ref _modules[moduleId]);

    /// <summary>
    /// Returns what <paramref name="moduleId"/> is publishing, if it is publishing at all and the
    /// data is of type <typeparamref name="T"/>.
    /// </summary>
    public bool TryGet<T>(byte moduleId, [NotNullWhen(true)] out T? data) where T : class, IModuleData
    {
        data = Volatile.Read(ref _modules[moduleId]) as T;
        return data is not null;
    }

    /// <summary>Stops publishing every module.</summary>
    public void Clear() => Array.Clear(_modules);
}
