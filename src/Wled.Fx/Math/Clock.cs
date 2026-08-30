using System.Diagnostics;

namespace Wled.Fx;

/// <summary>
/// The millisecond time base the effect engine runs on - the port of Arduino <c>millis()</c>.
/// </summary>
/// <remarks>
/// By default the clock follows a <see cref="Stopwatch"/> started when the process does. Call
/// <see cref="Freeze"/> to drive it by hand, which is how the tests and the offline frame renderer
/// produce reproducible output. Values wrap at 2^32 ms exactly as they do on the firmware, so the
/// unsigned arithmetic in the effects behaves the same way across a rollover.
/// </remarks>
public static class Clock
{
    private static readonly Stopwatch Watch = Stopwatch.StartNew();
    private static uint? _frozen;

    /// <summary>Milliseconds since start-up, wrapping at 2^32.</summary>
    public static uint Millis => _frozen ?? unchecked((uint)Watch.ElapsedMilliseconds);

    /// <summary>Pins the clock to a fixed value; pass <see langword="null"/> to resume real time.</summary>
    public static void Freeze(uint? millis) => _frozen = millis;

    /// <summary>Advances a frozen clock. Freezes it at <paramref name="delta"/> if it was running.</summary>
    public static void Advance(uint delta) => _frozen = (_frozen ?? 0) + delta;
}

/// <summary>
/// BPM-driven waveform generators. Port of the <c>beat*</c> family in <c>util.cpp</c>,
/// which in turn derives from FastLED 3.6.0 (MIT licence).
/// </summary>
public static class Beat
{
    /// <summary>16-bit sawtooth at a BPM given in Q8.8 fixed point (120 BPM == 30720).</summary>
    public static ushort Beat88(ushort beatsPerMinute88, uint timebase = 0)
        => (ushort)(((Clock.Millis - timebase) * beatsPerMinute88 * 280) >> 16);

    /// <summary>16-bit sawtooth at the given BPM.</summary>
    public static ushort Beat16(uint beatsPerMinute, uint timebase = 0)
    {
        if (beatsPerMinute < 256) beatsPerMinute <<= 8;
        return Beat88((ushort)beatsPerMinute, timebase);
    }

    /// <summary>8-bit sawtooth at the given BPM.</summary>
    public static byte Beat8(uint beatsPerMinute, uint timebase = 0) => (byte)(Beat16(beatsPerMinute, timebase) >> 8);

    /// <summary>16-bit sine oscillating between the given bounds, at a BPM in Q8.8 fixed point.</summary>
    public static ushort Sin88(ushort beatsPerMinute88, ushort lowest = 0, ushort highest = 65535,
                               uint timebase = 0, ushort phaseOffset = 0)
    {
        ushort beat = Beat88(beatsPerMinute88, timebase);
        var beatSin = (ushort)(FastMath.Sin16((ushort)(beat + phaseOffset)) + 32768);
        var rangeWidth = (ushort)(highest - lowest);
        return (ushort)(lowest + FastMath.Scale16(beatSin, rangeWidth));
    }

    /// <summary>16-bit sine oscillating between the given bounds.</summary>
    public static ushort Sin16(uint beatsPerMinute, ushort lowest = 0, ushort highest = 65535,
                               uint timebase = 0, ushort phaseOffset = 0)
    {
        ushort beat = Beat16(beatsPerMinute, timebase);
        var beatSin = (ushort)(FastMath.Sin16((ushort)(beat + phaseOffset)) + 32768);
        var rangeWidth = (ushort)(highest - lowest);
        return (ushort)(lowest + FastMath.Scale16(beatSin, rangeWidth));
    }

    /// <summary>
    /// 8-bit sine oscillating between the given bounds.
    /// </summary>
    /// <remarks>
    /// The bounds are truncated to bytes, so a negative bound wraps exactly as it does in C++ -
    /// effects routinely call this as <c>beatsin8_t(bpm, -64, 64)</c> and feed the result straight
    /// back into 8-bit phase math, where the wrap is what makes it come out right.
    /// </remarks>
    public static byte Sin8(uint beatsPerMinute, int lowest = 0, int highest = 255,
                            uint timebase = 0, byte phaseOffset = 0)
    {
        byte beat = Beat8(beatsPerMinute, timebase);
        byte beatSin = FastMath.Sin8((byte)(beat + phaseOffset));
        var rangeWidth = (byte)((byte)highest - (byte)lowest);
        return (byte)((byte)lowest + FastMath.Scale8(beatSin, rangeWidth));
    }
}
