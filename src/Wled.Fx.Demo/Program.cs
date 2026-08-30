using System.Globalization;
using System.Net.Sockets;
using System.Text;
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

            Play options:
              --length <n>      strip length, default 60
              --height <n>      matrix height, default 1
              --palette <n>     palette id, default the effect own
              --speed <0-255>
              --intensity <0-255>
              --color <hex>     primary colour, e.g. FF8000
              --seconds <n>     how long to run, default 10
              --fps <n>         frame rate, default 30

            The effect may be given by id or by name:
              wledfx play Fireworks --length 80 --seconds 5
              wledfx play 66 --height 16 --length 16
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

        if (TryIntOption(args, "--palette", out int palette)) seg.SetPalette((byte)palette);
        if (TryIntOption(args, "--speed", out int speed)) seg.Speed = (byte)speed;
        if (TryIntOption(args, "--intensity", out int intensity)) seg.Intensity = (byte)intensity;
        if (StringOption(args, "--color") is { } hex && uint.TryParse(hex, NumberStyles.HexNumber, null, out uint color))
            seg.SetColor(0, color);
        seg.StopTransition();

        Console.WriteLine($"{effect.Name} on {length}x{height}, palette {Palettes.NameOf(seg.Palette)} - press Ctrl+C to stop");
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
