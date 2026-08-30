namespace Wled.Fx;

/// <summary>
/// The random number source used by effects. Replaces the hardware RNG wrappers
/// (<c>hw_random8/16/32</c>) declared in <c>fcn_declare.h</c>.
/// </summary>
/// <remarks>
/// The scaling of the limited overloads mirrors the C++ original (a multiply-and-shift rather than
/// a modulo), so the very slight bias of the firmware is preserved. Call <see cref="Seed"/> to make
/// a render deterministic, which is what the tests and the frame-dumping demo rely on.
/// </remarks>
public static class Rng
{
    [ThreadStatic] private static Random? _random;

    private static Random Current => _random ??= new Random();

    /// <summary>Reseeds this thread generator so subsequent renders are reproducible.</summary>
    public static void Seed(int seed) => _random = new Random(seed);

    /// <summary>A full 32-bit random value.</summary>
    public static uint Next() => (uint)Current.NextInt64(uint.MinValue, (long)uint.MaxValue + 1);

    /// <summary>A random value in <c>[0, upperLimit)</c>.</summary>
    public static uint Next(uint upperLimit) => (uint)(((ulong)Next() * upperLimit) >> 32);

    /// <summary>A random value in <c>[lowerLimit, upperLimit)</c>.</summary>
    public static int Next(int lowerLimit, int upperLimit)
    {
        if (lowerLimit >= upperLimit) return lowerLimit;
        return lowerLimit + (int)Next((uint)(upperLimit - lowerLimit));
    }

    /// <summary>A random 16-bit value.</summary>
    public static ushort Next16() => (ushort)Next();

    /// <summary>A random 16-bit value in <c>[0, upperLimit)</c>; <paramref name="upperLimit"/> is 0..65535.</summary>
    public static ushort Next16(uint upperLimit) => (ushort)((Next16() * upperLimit) >> 16);

    /// <summary>A random 16-bit value in <c>[lowerLimit, upperLimit)</c>.</summary>
    public static short Next16(int lowerLimit, int upperLimit) => (short)(lowerLimit + Next16((uint)(upperLimit - lowerLimit)));

    /// <summary>A random byte.</summary>
    public static byte Next8() => (byte)Next();

    /// <summary>A random byte in <c>[0, upperLimit)</c>.</summary>
    public static byte Next8(uint upperLimit) => (byte)((Next8() * upperLimit) >> 8);

    /// <summary>A random byte in <c>[lowerLimit, upperLimit)</c>.</summary>
    public static byte Next8(uint lowerLimit, uint upperLimit) => (byte)(lowerLimit + Next8(upperLimit - lowerLimit));

    /// <summary>Returns a colour-wheel index at least 42 steps away from <paramref name="pos"/>.</summary>
    public static byte NextWheelIndex(byte pos)
    {
        byte r = 0;
        int d = 0;
        while (d < 42)
        {
            r = Next8();
            int x = System.Math.Abs(pos - r);
            int y = 255 - x;
            d = System.Math.Min(x, y);
        }
        return r;
    }
}

/// <summary>
/// A tiny, seedable pseudo random generator. Port of <c>prng.h</c>.
/// </summary>
/// <remarks>
/// Effects use it when they need the <em>same</em> random sequence on every frame - for example to
/// re-derive per-pixel jitter without storing it. Resetting <see cref="Seed"/> replays the sequence.
/// </remarks>
public sealed class Prng(ushort initialSeed = 0x1234)
{
    /// <summary>The current generator state; assigning it restarts the sequence.</summary>
    public ushort Seed { get; set; } = initialSeed;

    /// <summary>Advances the generator and returns a 16-bit value.</summary>
    public ushort Next16()
    {
        var s = (ushort)(Seed * 3001 + 31683);
        s ^= (ushort)(s >> 7);
        Seed = s;
        return s;
    }

    /// <summary>A value in <c>[0, limit)</c>.</summary>
    public ushort Next16(ushort limit) => (ushort)(((uint)Next16() * limit) >> 16);

    /// <summary>A value in <c>[min, limit)</c>.</summary>
    public ushort Next16(ushort min, ushort limit) => (ushort)(Next16((ushort)(limit - min)) + min);

    /// <summary>Advances the generator and returns a byte.</summary>
    public byte Next8() => (byte)Next16();

    /// <summary>A byte in <c>[0, limit)</c>.</summary>
    public byte Next8(byte limit) => (byte)((Next8() * limit) >> 8);

    /// <summary>A byte in <c>[min, limit)</c>.</summary>
    public byte Next8(byte min, byte limit) => (byte)(Next8((byte)(limit - min)) + min);
}
