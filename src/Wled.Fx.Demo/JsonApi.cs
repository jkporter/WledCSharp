using System.Globalization;
using System.Text.Json.Nodes;

namespace Wled.Fx.Demo;

/// <summary>
/// The value syntax of the WLED JSON API: how a single key is read out of a state object.
/// Port of <c>parseNumber</c>, <c>getVal</c>, <c>getBoolVal</c> and <c>colorFromHexString</c>.
/// </summary>
/// <remarks>
/// The API is not simply numbers. A slider value may arrive as a string that says what to do with
/// the value already there - <c>"~10"</c> adds ten, <c>"~-"</c> steps down by one, <c>"r"</c> picks
/// at random, <c>"w~10"</c> wraps round the ends, <c>"1~5~"</c> cycles inside a range - and a
/// boolean may arrive as <c>"t"</c>, meaning toggle. Presets written by the UI mostly hold plain
/// numbers, but presets written by hand or by a button macro do use these, so a loader that only
/// reads numbers quietly does the wrong thing.
/// </remarks>
internal static class JsonApi
{
    /// <summary>The last hue the random colour syntax landed on; the C++ <c>lastRandomIndex</c>.</summary>
    private static byte _lastRandomIndex;

    /// <summary>
    /// Reads a 0-255 value, applying the increment, decrement, wrap and random forms to
    /// <paramref name="val"/>. Returns false when the key is absent or unreadable, in which case
    /// <paramref name="val"/> is untouched. Port of <c>getVal()</c>.
    /// </summary>
    public static bool TryGetVal(JsonNode? elem, ref byte val, byte vmin = 0, byte vmax = 255)
    {
        if (elem is not JsonValue value) return false;

        if (value.TryGetValue(out int number))
        {
            if (number < 0) return false; // ignore e.g. {"ps":-1}
            val = (byte)number;
            return true;
        }

        if (value.TryGetValue(out string? text) && text is not null)
        {
            if (text.Length == 0 || text.Length > 12) return false;
            // an explicit range or a random request carries its own limits, so drop the caller's
            if (text.Length > 3 && (text.Contains('r') || text.IndexOf('~') != text.LastIndexOf('~'))) vmax = vmin = 0;
            ParseNumber(text, ref val, vmin, vmax);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Applies one value expression to <paramref name="val"/>. Port of <c>parseNumber()</c>.
    /// </summary>
    public static void ParseNumber(string? str, ref byte val, byte vmin = 0, byte vmax = 255)
    {
        if (string.IsNullOrEmpty(str)) return;

        if (str[0] == 'r') // "r": anywhere in the range
        {
            val = Rng.Next8(vmin, vmax != 0 ? vmax : (byte)255);
            return;
        }

        bool wrap = false;
        if (str[0] == 'w' && str.Length > 1) // "w~10": step, and run off one end onto the other
        {
            str = str[1..];
            wrap = true;
        }

        if (str[0] == '~') // "~10", "~-10", "~", "~-": relative to the value already there
        {
            char next = str.Length > 1 ? str[1] : '\0';
            int delta = Atoi(str[1..]);
            if (delta == 0)
            {
                if (next == '0') return; // "~0" is an explicit no-op
                if (next == '-') val = (byte)(val - 1 < vmin ? vmax : Math.Min(vmax, val - 1));
                else val = (byte)(val + 1 > vmax ? vmin : Math.Max(vmin, val + 1));
            }
            else
            {
                if (wrap && val == vmax && delta > 0) delta = vmin;
                else if (wrap && val == vmin && delta < 0) delta = vmax;
                else
                {
                    delta += val;
                    if (delta > vmax) delta = vmax;
                    if (delta < vmin) delta = vmin;
                }
                val = (byte)delta;
            }
            return;
        }

        if (vmin == vmax && vmin == 0) // no limits from the caller, so the string may carry a range
        {
            int tilde = str.IndexOf('~');
            if (tilde >= 0)
            {
                var low = (byte)Atoi(str);
                string rest = str[(tilde + 1)..]; // "5~" out of "1~5~"
                var high = (byte)Atoi(rest);
                if (high > 0)
                {
                    int i = 1; // the firmware steps past the first digit before it starts looking
                    while (i < rest.Length && char.IsAsciiDigit(rest[i])) i++;
                    ParseNumber(i < rest.Length ? rest[i..] : string.Empty, ref val, low, high);
                    return;
                }
            }
        }

        val = (byte)Atoi(str);
    }

    /// <summary>
    /// Reads a boolean, where the string <c>"t"</c> means "toggle whatever it is now".
    /// Port of <c>getBoolVal()</c>.
    /// </summary>
    public static bool GetBool(JsonNode? elem, bool dflt)
        => GetString(elem) is ['t', ..] ? !dflt : GetBoolOrDefault(elem, dflt);

    /// <summary>
    /// Reads a boolean without the toggle syntax, falling back to <paramref name="dflt"/> for
    /// anything that is not a boolean or a number. This is the bare <c>elem | dflt</c> the
    /// firmware uses for the state-level <c>on</c> key, which handles <c>"t"</c> separately and
    /// would otherwise toggle twice.
    /// </summary>
    public static bool GetBoolOrDefault(JsonNode? elem, bool dflt)
    {
        if (elem is not JsonValue value) return dflt;
        if (value.TryGetValue(out bool flag)) return flag;
        if (value.TryGetValue(out int number)) return number != 0;
        return dflt;
    }

    /// <summary>Reads a whole number, or null when the key is absent or is not a number.</summary>
    public static int? GetInt(JsonNode? elem)
        => elem is JsonValue value && value.TryGetValue(out int number) ? number : null;

    /// <summary>Reads a string, or null when the key is absent or is not a string.</summary>
    public static string? GetString(JsonNode? elem)
        => elem is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    /// <summary>
    /// Parses a colour written as six hex digits (RRGGBB) or eight (RRGGBBWW), returning null for
    /// anything else. Port of <c>colorFromHexString()</c>.
    /// </summary>
    public static Rgbw? ColorFromHexString(string text)
    {
        if (text.Length != 6 && text.Length != 8) return null;
        if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint c)) return null;

        return text.Length == 6
            ? new Rgbw((int)(c >> 16) & 0xFF, (int)(c >> 8) & 0xFF, (int)c & 0xFF)
            : new Rgbw((int)(c >> 24) & 0xFF, (int)(c >> 16) & 0xFF, (int)(c >> 8) & 0xFF, (int)c & 0xFF);
    }

    /// <summary>
    /// A saturated random colour, at least a sixth of the hue wheel away from the last one.
    /// Port of <c>setRandomColor()</c>.
    /// </summary>
    public static Rgbw RandomColor()
    {
        _lastRandomIndex = Rng.NextWheelIndex(_lastRandomIndex);
        return ColorUtil.HsvToRgbSpectrum(new Chsv32(_lastRandomIndex * 256, 255, 255));
    }

    /// <summary>
    /// C's <c>atoi</c>: the leading integer, or zero when the string does not start with one.
    /// </summary>
    private static int Atoi(string str)
    {
        int i = 0;
        while (i < str.Length && char.IsWhiteSpace(str[i])) i++;

        int sign = 1;
        if (i < str.Length && (str[i] == '+' || str[i] == '-'))
        {
            if (str[i] == '-') sign = -1;
            i++;
        }

        long value = 0;
        while (i < str.Length && char.IsAsciiDigit(str[i]))
        {
            value = value * 10 + (str[i++] - '0');
            if (value > int.MaxValue) return sign > 0 ? int.MaxValue : int.MinValue;
        }
        return (int)(sign * value);
    }
}
