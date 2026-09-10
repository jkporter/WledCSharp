using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Wled.Fx;

namespace Wled.Fx.Demo;

/// <summary>
/// A small command line front end for the effect engine: lists what is available and previews
/// effects in the terminal using 24-bit ANSI colour.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        ProgressEffect.Register(); // a host-supplied effect, so 'list' and 'play' pick it up too

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 0;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "list" => ListEffects(args),
                "palettes" => ListPalettes(),
                "play" => Play(args),
                "progress" => ShowProgress(args),
                "preset" => ApplyPreset(args),
                _ => Unknown(args[0]),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"error: unknown command '{command}'");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            wledfx - preview the WLED effect engine

            Usage:
              wledfx list [filter]              list the implemented effects
              wledfx palettes                   list the built-in palettes
              wledfx play <effect> [options]    preview an effect in the terminal
              wledfx progress [options]         run the Progress effect off live external data
              wledfx preset <file> [key]        apply a preset out of a WLED presets.json

            Play options:
              --length <n>      strip length, default 60
              --height <n>      matrix height, default 1
              --palette <n>     palette id, default 11 (Rainbow)
              --speed <0-255>
              --intensity <0-255>
              --color <hex>     primary colour, e.g. FF8000; on its own it keeps palette 0,
                                which is built from the colour
              --seconds <n>     how long to run, default 10
              --fps <n>         frame rate, default 30

            The effect may be given by id or by name:
              wledfx play Fireworks --length 80 --seconds 5
              wledfx play 66 --height 16 --length 16

            Progress options:
              --length, --height, --seconds, --fps as above
              --indeterminate   report an unknown total, so the bar sweeps

            'progress' fakes a job that publishes its progress from a worker thread;
            'play Progress' runs the same effect with nothing published, on simulated data.

            Preset options:
              --length, --height, --seconds, --fps as above

            The key is the preset id it is filed under, or its name; with no key the presets in
            the file are listed. The strip is sized by --length and --height, so give it the
            shape the preset was saved on or its segment bounds will not line up:
              wledfx preset presets.json 3 --length 120
              wledfx preset presets.json "Sunset" --length 16 --height 16
            """);
    }

    private static int ListEffects(string[] args)
    {
        string? filter = args.Length > 1 ? args[1] : null;
        int shown = 0;
        foreach (EffectInfo effect in EffectRegistry.All)
        {
            if (filter is not null && !effect.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            string dimensions = Describe(effect.Metadata.Dimensions);
            Console.WriteLine($"{effect.Id,3}  {effect.Name,-22} {dimensions}");
            shown++;
        }
        Console.WriteLine();
        Console.WriteLine($"{shown} effect(s) of {EffectRegistry.ModeCount} protocol ids");
        return 0;

        static string Describe(EffectDimensions dimensions)
        {
            var parts = new List<string>();
            if (dimensions.HasFlag(EffectDimensions.OneDimensional)) parts.Add("1D");
            if (dimensions.HasFlag(EffectDimensions.TwoDimensional)) parts.Add("2D");
            if (dimensions.HasFlag(EffectDimensions.Volume)) parts.Add("volume");
            if (dimensions.HasFlag(EffectDimensions.Frequency)) parts.Add("frequency");
            return string.Join(" ", parts);
        }
    }

    private static int ListPalettes()
    {
        for (int id = 0; id < Palettes.FixedCount; id++)
        {
            Console.Write($"{id,3}  {Palettes.NameOf(id),-16} ");
            if (id >= Palettes.DynamicCount)
            {
                Palette16 palette = Palettes.Get(id);
                var swatch = new StringBuilder();
                for (int i = 0; i < 32; i++)
                {
                    Rgbw c = palette.ColorAt(i * 255 / 31);
                    swatch.Append(Background(c)).Append(' ');
                }
                swatch.Append("\u001b[0m");
                Console.Write(swatch);
            }
            Console.WriteLine();
        }
        return 0;
    }

    private static int Play(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("error: play needs an effect id or name");
            return 1;
        }

        EffectInfo? effect = ResolveEffect(args[1]);
        if (effect is null)
        {
            Console.Error.WriteLine($"error: no effect called '{args[1]}' - try 'wledfx list'");
            return 1;
        }

        int length = IntOption(args, "--length", 60);
        int height = IntOption(args, "--height", 1);
        int seconds = IntOption(args, "--seconds", 10);
        int fps = IntOption(args, "--fps", 30);
        string? host = StringOption(args, "--host");
        int port = IntOption(args, "--port", 21324);

        var strip = new LedStrip(length, height) { Brightness = 255, TargetFps = fps };
        Segment seg = strip.MainSegment;
        seg.SetMode(effect.Id, loadDefaults: true);
        seg.StopTransition(); // start on the effect itself rather than fading in from Solid

        ApplyLook(seg, args);
        if (TryIntOption(args, "--speed", out int speed)) seg.Speed = (byte)speed;
        if (TryIntOption(args, "--intensity", out int intensity)) seg.Intensity = (byte)intensity;
        seg.StopTransition();

        Console.WriteLine($"{effect.Name} on {length}×{height}, palette {Palettes.NameOf(seg.Palette)} - press Ctrl+C to stop");
        Console.WriteLine();

        UdpClient? client = null;
        if (host is not null)
        {
            client = new UdpClient();
            client.Connect(host, port);
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        Console.Write("\u001b[?25l"); // hide the cursor
        try
        {
            var frame = new StringBuilder();
            var frameBuffer = client is not null ? new byte[seg.Length * 4 + 2] : null;
            while (!cancellation.IsCancellationRequested)
            {
                if (strip.Service())
                {
                    if(client is not null)
                        Render(strip, client, frameBuffer!, height);
                    Render(strip, frame, height);
                }

                Thread.Sleep(1);
            }
        }
        finally
        {
            Console.Write("\u001b[?25h\u001b[0m"); // show it again
            Console.WriteLine();
        }
        return 0;
    }

    /// <summary>
    /// Runs the Progress effect against live external data: a worker thread reports how far along a
    /// fake job is, and the effect picks that up through the strip's module registry.
    /// </summary>
    private static int ShowProgress(string[] args)
    {
        int length = IntOption(args, "--length", 60);
        int height = IntOption(args, "--height", 1);
        int seconds = IntOption(args, "--seconds", 10);
        int fps = IntOption(args, "--fps", 30);
        bool indeterminate = Array.IndexOf(args, "--indeterminate") >= 0;

        var strip = new LedStrip(length, height) { Brightness = 255, TargetFps = fps };
        Segment seg = strip.MainSegment;
        seg.SetMode(ProgressEffect.Id, loadDefaults: true);
        ApplyLook(seg, args);
        if (TryIntOption(args, "--speed", out int speed)) seg.Speed = (byte)speed;
        seg.StopTransition();

        Console.WriteLine($"Fake job on {length}×{height}, published as module {ProgressEffect.ProgressModule} "
            + "from a worker thread - press Ctrl+C to stop");
        Console.WriteLine();

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        // the host side: whatever is doing the actual work republishes as it goes
        CancellationToken token = cancellation.Token;
        Task worker = Task.Run(async () =>
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            var total = TimeSpan.FromSeconds(seconds * 0.8);
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var fraction = (float)(elapsed.Elapsed / total);
                    strip.Modules.Publish(ProgressEffect.ProgressModule,
                        new ProgressData(fraction, indeterminate));
                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // the run is over; nothing to clean up
            }
        });

        Console.Write("\u001b[?25l"); // hide the cursor
        try
        {
            var frame = new StringBuilder();
            while (!cancellation.IsCancellationRequested)
            {
                if (strip.Service()) Render(strip, frame, height);
                Thread.Sleep(1);
            }
            worker.Wait();
        }
        finally
        {
            Console.Write("\u001b[?25h\u001b[0m"); // show it again
            Console.WriteLine();
        }
        return 0;
    }

    /// <summary>
    /// Applies a preset out of a WLED <c>presets.json</c> and previews the result, which is what
    /// the firmware does when a preset is recalled: read the object filed under that key, hand it
    /// to the state deserializer, carry on rendering.
    /// </summary>
    private static int ApplyPreset(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("error: preset needs the path to a presets.json");
            return 1;
        }

        string path = args[1];
        JsonObject file = PresetLoader.LoadFile(path);
        string? key = args.Length > 2 && !args[2].StartsWith("--", StringComparison.Ordinal) ? args[2] : null;

        if (key is null) return ListPresets(file, path);

        (string Key, JsonObject Preset)? found = PresetLoader.Find(file, key);
        if (found is null)
        {
            Console.Error.WriteLine($"error: {path} has no preset '{key}'");
            ListPresets(file, path);
            return 1;
        }

        (string id, JsonObject preset) = found.Value;
        int length = IntOption(args, "--length", 60);
        int height = IntOption(args, "--height", 1);
        int seconds = IntOption(args, "--seconds", 10);
        int fps = IntOption(args, "--fps", 30);

        var strip = new LedStrip(length, height) { Brightness = 255, TargetFps = fps };
        var loader = new PresetLoader(strip);
        loader.ApplyPreset(preset);

        // a preset is applied to a running strip, so its effects fade in; start on them instead
        foreach (Segment segment in strip.Segments) segment.StopTransition();

        Console.WriteLine($"Preset {id}: {PresetLoader.NameOf(preset) ?? "(unnamed)"} on {length}×{height}, "
            + $"brightness {strip.Brightness}");
        foreach (Segment segment in strip.Segments)
        {
            if (!segment.IsActive) continue;
            string name = segment.Name is { Length: > 0 } n ? $" \"{n}\"" : string.Empty;
            Console.WriteLine($"  seg{name} {segment.Start}-{segment.Stop}"
                + $"  {EffectRegistry.Get(segment.Mode).Name} (fx {segment.Mode})"
                + $"  palette {Palettes.NameOf(segment.Palette)}"
                + $"  sx {segment.Speed} ix {segment.Intensity}"
                + $"  col {segment.Colors[0]}"
                + (segment.On ? string.Empty : "  [off]"));
        }
        if (loader.Skipped.Count > 0)
        {
            Console.WriteLine($"  ignored: {string.Join(", ", loader.Skipped)} - state a rendering library does not own");
        }
        Console.WriteLine();

        Preview(strip, height, seconds);
        return 0;
    }

    /// <summary>Prints what a preset file holds, which is the answer to "what keys can I pass?".</summary>
    private static int ListPresets(JsonObject file, string path)
    {
        int shown = 0;
        foreach ((string id, JsonObject preset) in PresetLoader.All(file))
        {
            string kind = PresetLoader.IsPlaylist(preset) ? " (playlist)" : string.Empty;
            Console.WriteLine($"{id,4}  {PresetLoader.NameOf(preset) ?? "(unnamed)"}{kind}");
            shown++;
        }
        Console.WriteLine();
        Console.WriteLine($"{shown} preset(s) in {path}");
        return 0;
    }

    /// <summary>Runs the strip for a while, drawing each frame in the terminal.</summary>
    private static void Preview(LedStrip strip, int height, int seconds)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        Console.Write("\u001b[?25l"); // hide the cursor
        try
        {
            var frame = new StringBuilder();
            while (!cancellation.IsCancellationRequested)
            {
                if (strip.Service()) Render(strip, frame, height);
                Thread.Sleep(1);
            }
        }
        finally
        {
            Console.Write("\u001b[?25h\u001b[0m"); // show it again
            Console.WriteLine();
        }
    }

    /// <summary>Draws the current frame, using one line per matrix row.</summary>
    private static void Render(LedStrip strip, StringBuilder frame, int height)
    {
        frame.Clear();
        if (height > 1) frame.Append($"\u001b[{height}A"); // rewind over the previous frame
        else frame.Append('\r');

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < strip.MatrixWidth; x++)
            {
                Rgbw c = strip.GetPixelColorXY(x, y);
                frame.Append(Background(c)).Append(' ');
            }
            frame.Append("\u001b[0m");
            if (height > 1) frame.Append('\n');
        }
        Console.Write(frame);
    }

    private static void Render(LedStrip strip, UdpClient updClient, byte[] frame, int height)
    {
        Array.Clear(frame);
        int i = 0;
        frame[i++] = 3;
        frame[i++] = 3;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < strip.MatrixWidth; x++)
            {
                Rgbw c = strip.GetPixelColorXY(x, y);
                frame[i++]  = c.R;
                frame[i++]  = c.G;
                frame[i++]  = c.B;
                frame[i++]  = c.W;
            }
        }
        updClient.Send(frame, frame.Length);
    }

    private static string Background(Rgbw c) => $"\u001b[48;2;{c.R};{c.G};{c.B}m";

    /// <summary>Warm white, so an effect that draws with the primary colour shows up.</summary>
    private static readonly Rgbw DefaultColor = new(255, 190, 130);

    /// <summary>Palette to preview with when the caller names neither a palette nor a colour.</summary>
    private const byte DefaultPalette = 11; // Rainbow

    /// <summary>
    /// Applies the <c>--palette</c> and <c>--color</c> options, defaulting to a look that is
    /// actually visible.
    /// </summary>
    /// <remarks>
    /// A segment starts with all three colour slots black, matching the firmware, and palette 0 is
    /// built out of those slots - so an effect previewed at bare defaults draws black on black. The
    /// preview is not the place to reproduce that, so it fills in a colour and a palette. Naming
    /// either one is taken as knowing what you want: a colour on its own keeps palette 0, which is
    /// how you preview an effect in a single colour.
    /// </remarks>
    private static void ApplyLook(Segment seg, string[] args)
    {
        bool named = false;

        if (StringOption(args, "--color") is { } hex
            && uint.TryParse(hex, NumberStyles.HexNumber, null, out uint color))
        {
            seg.SetColor(0, color);
            named = true;
        }
        else seg.SetColor(0, DefaultColor);

        if (TryIntOption(args, "--palette", out int palette)) seg.SetPalette((byte)palette);
        else if (!named) seg.SetPalette(DefaultPalette);
    }

    private static EffectInfo? ResolveEffect(string token)
    {
        if (int.TryParse(token, out int id))
        {
            EffectInfo byId = EffectRegistry.Get(id);
            return byId.Metadata.IsReserved ? null : byId;
        }
        return EffectRegistry.FindByName(token);
    }

    private static string? StringOption(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static bool TryIntOption(string[] args, string name, out int value)
    {
        value = 0;
        return StringOption(args, name) is { } text && int.TryParse(text, out value);
    }

    private static int IntOption(string[] args, string name, int fallback)
        => TryIntOption(args, name, out int value) ? value : fallback;
}
