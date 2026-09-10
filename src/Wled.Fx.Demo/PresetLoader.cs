using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wled.Fx.Demo;

/// <summary>
/// Applies a preset out of a WLED <c>presets.json</c> to a <see cref="LedStrip"/>.
/// </summary>
/// <remarks>
/// <para>
/// A preset file is a JSON object keyed by preset ID - <c>{"0":{},"1":{"n":"Sunset","bri":128,
/// "seg":[...]},...}</c> - and each value is a saved state object, the same shape the JSON API
/// takes over HTTP. So applying one is the firmware's <c>handlePresets()</c>: pull the object out
/// by key and hand it to <c>deserializeState()</c>, which walks the state keys and then hands each
/// element of <c>seg</c> to <c>deserializeSegment()</c>.
/// </para>
/// <para>
/// This is a port of those two functions from <c>json.cpp</c>, down to the value syntax in
/// <c>util.cpp</c>: <c>"~10"</c> increments, <c>"~-"</c> decrements, <c>"r"</c> randomises and
/// <c>"w~10"</c> wraps, the same as a device would read them. What it leaves out is everything
/// outside a rendering library - nightlight, playlists, UDP sync, the HTTP API bridge and preset
/// chaining. Those keys are collected in <see cref="Skipped"/> rather than silently dropped.
/// </para>
/// </remarks>
public sealed class PresetLoader(LedStrip strip)
{
    private readonly LedStrip _strip = strip;

    /// <summary>Brightness to restore when a state turns the strip back on; the C++ <c>briLast</c>.</summary>
    private byte _brightnessWhenOn = strip.Brightness != 0 ? strip.Brightness : (byte)128;

    /// <summary>State keys the last apply understood but could not honour, in the order seen.</summary>
    public List<string> Skipped { get; } = [];

    /// <summary>State keys a rendering library has nothing to do with.</summary>
    private static readonly string[] OutOfScope =
        ["nl", "udpn", "playlist", "ps", "pd", "win", "psave", "pdel", "ledmap", "live", "lor", "rb", "np", "time"];

    // ------------------------------------------------------------------- the file

    /// <summary>Reads a <c>presets.json</c> file.</summary>
    public static JsonObject LoadFile(string path)
    {
        using FileStream file = File.OpenRead(path);
        var options = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        return JsonNode.Parse(file, nodeOptions: null, options) as JsonObject
            ?? throw new InvalidDataException($"{path} is not a preset file: the root is not a JSON object.");
    }

    /// <summary>
    /// Finds a preset by key - the ID it is filed under, or its <c>n</c> name. Preset 0 is the
    /// firmware's "no preset" slot and is never a match.
    /// </summary>
    public static (string Key, JsonObject Preset)? Find(JsonObject presets, string key)
    {
        foreach ((string id, JsonNode? node) in presets)
        {
            if (node is not JsonObject || id == "0") continue;
            if (id.Equals(key, StringComparison.OrdinalIgnoreCase)) return (id, (JsonObject)node);
        }

        foreach ((string id, JsonNode? node) in presets)
        {
            if (node is not JsonObject preset || id == "0") continue;
            if (NameOf(preset) is { } name && name.Equals(key, StringComparison.OrdinalIgnoreCase)) return (id, preset);
        }
        return null;
    }

    /// <summary>Every populated preset in the file, in file order.</summary>
    public static IEnumerable<(string Key, JsonObject Preset)> All(JsonObject presets)
    {
        foreach ((string id, JsonNode? node) in presets)
        {
            if (node is JsonObject preset && id != "0" && preset.Count > 0) yield return (id, preset);
        }
    }

    /// <summary>The preset's name, or null when it was saved without one.</summary>
    public static string? NameOf(JsonObject preset) => JsonApi.GetString(preset["n"]);

    /// <summary>Whether the preset is a playlist rather than a saved state.</summary>
    public static bool IsPlaylist(JsonObject preset) => preset["playlist"] is JsonObject;

    // ------------------------------------------------------------------- applying

    /// <summary>
    /// Applies a preset. This is <c>handlePresets()</c>: a preset is just a state object, with the
    /// chaining keys stripped so that a preset cannot recursively load another one.
    /// </summary>
    public void ApplyPreset(JsonObject preset)
    {
        if (IsPlaylist(preset))
            throw new NotSupportedException(
                "That preset is a playlist. A playlist sequences other presets over time, which is scheduling rather than rendering - apply the presets it names instead.");

        Apply(preset);
    }

    /// <summary>
    /// Applies a WLED state object - the body of a preset, or of a <c>POST /json/state</c> call.
    /// Port of <c>deserializeState()</c>.
    /// </summary>
    public void Apply(JsonObject root)
    {
        Skipped.Clear();
        foreach (string key in OutOfScope)
        {
            if (root[key] is not null) Skipped.Add(key);
        }

        // Brightness, and the on/off flag that rides on it: WLED holds "off" as brightness 0 and
        // remembers what it was, so turning back on restores the old level.
        bool onBefore = _strip.Brightness != 0;
        byte brightness = _strip.Brightness;
        JsonApi.TryGetVal(root["bri"], ref brightness);
        _strip.Brightness = brightness;

        bool on = JsonApi.GetBoolOrDefault(root["on"], brightness > 0);
        if (on != _strip.Brightness > 0) ToggleOnOff();
        if (JsonApi.GetString(root["on"]) is ['t', ..] && (onBefore || _strip.Brightness == 0)) ToggleOnOff();

        // Turning on unfreezes every segment, so a state frozen by a live feed draws again.
        if (_strip.Brightness != 0 && !onBefore)
        {
            foreach (Segment segment in _strip.Segments) segment.Freeze = false;
        }

        // "transition" is in tenths of a second; "tt" is the same but applies to this call only.
        if (JsonApi.GetInt(root["transition"]) is >= 0 and { } transition) _strip.TransitionDuration = transition * 100;
        int restoreTransition = _strip.TransitionDuration;
        if (JsonApi.GetInt(root["tt"]) is >= 0 and { } once) _strip.TransitionDuration = once * 100;

        if (JsonApi.GetInt(root["bs"]) is { } blendingStyle) _strip.BlendingStyle = (TransitionStyle)(blendingStyle & 0x1F);
        if (JsonApi.GetInt(root["tb"]) is >= 0 and { } timebase) _strip.Timebase = (uint)timebase - Clock.Millis;
        if (JsonApi.GetInt(root["mainseg"]) is { } mainSegment) _strip.MainSegmentId = mainSegment;

        // The segments themselves. The strip is suspended so no frame renders mid-edit.
        if (root["seg"] is { } segVar)
        {
            _strip.Suspend();
            try
            {
                if (segVar is JsonObject single)
                {
                    int id = JsonApi.GetInt(single["id"]) ?? -1;
                    if (id < 0)
                    {
                        // a bare object with no id applies to every selected segment
                        for (int s = 0; s < _strip.Segments.Count; s++)
                        {
                            Segment segment = _strip.Segments[s];
                            if (segment.IsActive && segment.Selected) ApplySegment(single, s);
                        }
                    }
                    else ApplySegment(single, id);
                }
                else if (segVar is JsonArray many)
                {
                    int index = 0;
                    foreach (JsonNode? elem in many)
                    {
                        if (elem is JsonObject segment) ApplySegment(segment, index);
                        index++;
                    }
                }
            }
            finally
            {
                _strip.Resume();
            }
        }

        if (JsonApi.GetBool(root["rSeg"], false)) MakeSingleSegment();

        _strip.TransitionDuration = restoreTransition;
    }

    /// <summary>Port of <c>toggleOnOff()</c>.</summary>
    private void ToggleOnOff()
    {
        if (_strip.Brightness == 0)
        {
            _strip.Brightness = _brightnessWhenOn;
            _strip.ResetSegments(); // the C++ restartRuntime()
        }
        else
        {
            _brightnessWhenOn = _strip.Brightness;
            _strip.Brightness = 0;
        }
    }

    /// <summary>Collapses the strip back to one segment covering all of it; the "rSeg" request.</summary>
    private void MakeSingleSegment()
    {
        for (int i = _strip.Segments.Count - 1; i > 0; i--) _strip.RemoveSegment(_strip.Segments[i]);
        _strip.MainSegmentId = 0;
        _strip.Segments[0].SetGeometry(0, _strip.MatrixWidth, stopY: _strip.MatrixHeight);
    }

    // ------------------------------------------------------------------- segments

    /// <summary>
    /// Applies one element of the <c>seg</c> array. Port of <c>deserializeSegment()</c>; the
    /// firmware's <c>presetId</c> is always non-zero here, so a segment created by a preset does
    /// not get the orange "new segment" indicator colour.
    /// </summary>
    private bool ApplySegment(JsonObject elem, int index)
    {
        int id = JsonApi.GetInt(elem["id"]) ?? index;
        if (id >= LedStrip.MaxSegments) return false;

        int stop = JsonApi.GetInt(elem["stop"]) ?? -1;

        // A segment past the end of the list is appended, provided the preset gives it a length.
        if (id >= _strip.Segments.Count)
        {
            if (stop <= 0) return false;
            _strip.AddSegment(0, _strip.Length);
            id = _strip.Segments.Count - 1;
        }

        Segment seg = _strip.GetSegment(id);

        int start = JsonApi.GetInt(elem["start"]) ?? seg.Start;
        if (stop < 0)
        {
            int length = JsonApi.GetInt(elem["len"]) ?? 0;
            stop = length > 0 ? start + length : seg.Stop;
        }
        int startY = JsonApi.GetInt(elem["startY"]) ?? seg.StartY;
        int stopY = JsonApi.GetInt(elem["stopY"]) ?? seg.StopY;

        // "rpt" tiles this segment across the rest of the strip, alternating direction.
        if (JsonApi.GetBool(elem["rpt"], false) && stop > 0)
        {
            var repeated = (JsonObject)elem.DeepClone();
            repeated.Remove("id");
            repeated.Remove("rpt");
            repeated.Remove("n");
            int length = stop - start;
            for (int i = id + 1; i < LedStrip.MaxSegments; i++)
            {
                start += length;
                if (start >= _strip.Length) break;
                repeated["start"] = start;
                repeated["stop"] = start + length;
                repeated["rev"] = !JsonApi.GetBool(repeated["rev"], false);
                ApplySegment(repeated, i);
            }
            return true;
        }

        if (JsonApi.GetString(elem["n"]) is { Length: > 0 } name) seg.Name = name;
        else if (start != seg.Start || stop != seg.Stop) seg.Name = null;

        byte grouping = (byte)(JsonApi.GetInt(elem["grp"]) ?? seg.Grouping);
        byte spacing = (byte)(JsonApi.GetInt(elem["spc"]) ?? seg.Spacing);
        int offset = seg.Offset;
        byte soundSim = (byte)(JsonApi.GetInt(elem["si"]) ?? seg.SoundSim);
        var map1D2D = (Mapping1D2D)Math.Clamp(JsonApi.GetInt(elem["m12"]) ?? (int)seg.Map1D2D, 0, 7);
        byte set = (byte)(JsonApi.GetInt(elem["set"]) ?? seg.Set);
        bool selected = JsonApi.GetBool(elem["sel"], seg.Selected);
        bool reverse = JsonApi.GetBool(elem["rev"], seg.Reverse);
        bool mirror = JsonApi.GetBool(elem["mi"], seg.Mirror);
        bool reverseY = JsonApi.GetBool(elem["rY"], seg.ReverseY);
        bool mirrorY = JsonApi.GetBool(elem["mY"], seg.MirrorY);
        bool transpose = JsonApi.GetBool(elem["tp"], seg.Transpose);

        // These change the virtual dimensions an effect draws into, so it has to start over.
        if (seg.Mirror != mirror || seg.MirrorY != mirrorY || seg.Transpose != transpose || seg.Map1D2D != map1D2D)
            seg.MarkForReset();

        int len = stop > start ? stop - start : 1;
        if (JsonApi.GetInt(elem["of"]) is { } requested)
        {
            int absolute = Math.Abs(requested);
            if (absolute > len - 1) absolute %= len;
            offset = requested < 0 ? len - absolute : absolute;
        }
        if (stop > start && offset > len - 1) offset = len - 1;

        // Offset and mapping are assigned first so SetGeometry can bow out when nothing moved,
        // which is what stops a preset that only changes the effect from dropping the pixel buffer.
        seg.Offset = offset;
        seg.Map1D2D = map1D2D;
        seg.SetGeometry(start, stop, grouping, spacing, startY: startY, stopY: stopY);

        if (seg.NeedsReset && seg.Stop == 0)
        {
            if (id == _strip.MainSegmentId) _strip.MainSegmentId = 0;
            return true; // the segment was deleted; nothing else in the element applies
        }

        byte opacity = seg.Opacity;
        if (JsonApi.TryGetVal(elem["bri"], ref opacity))
        {
            if (opacity > 0) seg.SetOpacity(opacity);
            seg.On = opacity != 0;
        }

        seg.On = JsonApi.GetBool(elem["on"], seg.On);
        seg.Freeze = JsonApi.GetBool(elem["frz"], seg.Freeze);
        seg.SetCct(JsonApi.GetInt(elem["cct"]) ?? seg.Cct);

        if (elem["col"] is JsonArray colors) ApplyColors(seg, colors);

        seg.Set = Math.Clamp(set, (byte)0, (byte)3);
        seg.SoundSim = Math.Clamp(soundSim, (byte)0, (byte)3);
        seg.Selected = selected;
        seg.Reverse = reverse;
        seg.Mirror = mirror;
        seg.ReverseY = reverseY;
        seg.MirrorY = mirrorY;
        seg.Transpose = transpose;

        byte fx = seg.Mode;
        if (JsonApi.TryGetVal(elem["fx"], ref fx, 0, (byte)EffectRegistry.ModeCount) && fx != seg.Mode)
            seg.SetMode(fx, JsonApi.GetBool(elem["fxdef"], false));

        byte speed = seg.Speed;
        if (JsonApi.TryGetVal(elem["sx"], ref speed)) seg.Speed = speed;

        byte intensity = seg.Intensity;
        if (JsonApi.TryGetVal(elem["ix"], ref intensity)) seg.Intensity = intensity;

        // The firmware ignores the palette on white-only and on/off busses; every strip here is RGB.
        byte palette = seg.Palette;
        if (JsonApi.TryGetVal(elem["pal"], ref palette, 0, (byte)Palettes.FixedCount)) seg.SetPalette(palette);

        byte custom1 = seg.Custom1;
        if (JsonApi.TryGetVal(elem["c1"], ref custom1)) seg.Custom1 = custom1;

        byte custom2 = seg.Custom2;
        if (JsonApi.TryGetVal(elem["c2"], ref custom2)) seg.Custom2 = custom2;

        byte custom3 = seg.Custom3;
        if (JsonApi.TryGetVal(elem["c3"], ref custom3, 0, 31)) seg.Custom3 = custom3;

        seg.Check1 = JsonApi.GetBool(elem["o1"], seg.Check1);
        seg.Check2 = JsonApi.GetBool(elem["o2"], seg.Check2);
        seg.Check3 = JsonApi.GetBool(elem["o3"], seg.Check3);

        if (JsonApi.GetInt(elem["bm"]) is { } blendMode) seg.BlendMode = (BlendMode)(byte)blendMode;

        if (elem["i"] is JsonArray individual) ApplyIndividual(seg, individual);

        return true;
    }

    /// <summary>
    /// Fills the three colour slots. An entry may be an array of channel values, a hex string,
    /// <c>"r"</c> for a random colour, an object of named channels, or a colour temperature in
    /// Kelvin. Anything else leaves the slot alone.
    /// </summary>
    /// <remarks>
    /// The firmware skips this for segments on a white-only or on/off bus. Every strip here is
    /// RGB, so there is no capability check to make.
    /// </remarks>
    private void ApplyColors(Segment seg, JsonArray colors)
    {
        for (int i = 0; i < Segment.ColorCount && i < colors.Count; i++)
        {
            JsonNode? entry = colors[i];
            int[] rgbw = [0, 0, 0, 0];
            bool valid = false;

            if (entry is JsonArray channels) // [r,g,b] or [r,g,b,w]
            {
                if (channels.Count == 0) continue;
                for (int c = 0; c < 4 && c < channels.Count; c++) rgbw[c] = JsonApi.GetInt(channels[c]) ?? 0;
                valid = true;
            }
            else if (entry is JsonObject named) // {"r":0,"g":127,"b":255,"w":255}, each optional
            {
                Rgbw current = seg.Colors[i];
                rgbw[0] = JsonApi.GetInt(named["r"]) ?? current.R;
                rgbw[1] = JsonApi.GetInt(named["g"]) ?? current.G;
                rgbw[2] = JsonApi.GetInt(named["b"]) ?? current.B;
                rgbw[3] = JsonApi.GetInt(named["w"]) ?? current.W;
                valid = true;
            }
            else if (JsonApi.GetString(entry) is { } text)
            {
                if (text == "r") // a random colour, kept clear of the last one on the hue wheel
                {
                    Rgbw random = JsonApi.RandomColor();
                    rgbw = [random.R, random.G, random.B, 0];
                    valid = true;
                }
                else if (JsonApi.ColorFromHexString(text) is { } hex)
                {
                    rgbw = [hex.R, hex.G, hex.B, hex.W];
                    valid = true;
                }
            }
            else if (JsonApi.GetInt(entry) is { } kelvin && kelvin >= 0) // colour temperature, 0 for black
            {
                if (kelvin > 0)
                {
                    Rgbw white = ColorUtil.KelvinToRgb(kelvin);
                    rgbw = [white.R, white.G, white.B, 0];
                }
                valid = true;
            }

            if (!valid) continue;

            seg.SetColor(i, new Rgbw(rgbw[0], rgbw[1], rgbw[2], rgbw[3]));
            if (seg.Mode == 0) _strip.Trigger(); // Solid: refresh now rather than at the next frame
        }
    }

    /// <summary>
    /// Paints individual LEDs from the <c>i</c> array: <c>[index, colour]</c>,
    /// <c>[start, stop, colour]</c>, or a run of colours from the last index onwards. The segment
    /// is frozen, because these pixels are the frame - no effect draws over them.
    /// </summary>
    private void ApplyIndividual(Segment seg, JsonArray individual)
    {
        _strip.TransitionDuration = 0; // painting pixels one by one is not something to fade into
        seg.StopTransition();

        if (!seg.Freeze)
        {
            seg.Freeze = true;
            for (int i = 0; i < seg.PhysicalLength; i++) seg.SetRawPixelColor(i, Rgbw.Black);
        }

        int from = 0, to = 0, bounds = 0; // 0 nothing set, 1 start set, 2 range set
        foreach (JsonNode? entry in individual)
        {
            if (entry is JsonValue && JsonApi.GetInt(entry) is { } position)
            {
                if (bounds == 0) { from = Math.Abs(position); bounds = 1; }
                else { to = Math.Abs(position); bounds = 2; }
                continue;
            }

            Rgbw color = Rgbw.Black;
            if (entry is JsonArray channels)
            {
                if (channels.Count is > 0 and < 5)
                {
                    int[] rgbw = [0, 0, 0, 0];
                    for (int c = 0; c < channels.Count; c++) rgbw[c] = JsonApi.GetInt(channels[c]) ?? 0;
                    color = new Rgbw(rgbw[0], rgbw[1], rgbw[2], rgbw[3]);
                }
            }
            else if (JsonApi.GetString(entry) is { } hex && JsonApi.ColorFromHexString(hex) is { } parsed) color = parsed;

            if (bounds < 2 || to <= from) to = from + 1;
            while (from < to) seg.SetRawPixelColor(from++, color);
            bounds = 0;
        }

        _strip.Trigger();
    }
}
