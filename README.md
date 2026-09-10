# Wled.Fx

A C# port of the [WLED](https://github.com/wled/WLED) effect engine: the effects from `FX.cpp`,
the segment and strip machinery from `FX_fcn.cpp` and `FX_2Dfcn.cpp`, the colour and palette code
from `colors.cpp` and `palettes.cpp`, and the fixed-point math the whole thing rests on.

It is a library, not a firmware. It produces pixels; what you do with them - drive real LEDs, render
a preview, feed a simulator - is up to you.

```csharp
var strip = new LedStrip(length: 60);
strip.MainSegment.SetMode(EffectId.Fireworks, loadDefaults: true);
strip.FrameReady += s => PushToHardware(s.Pixels);

while (running) strip.Service();   // renders only when a frame is actually due
```

## Layout

| Path | What lives there |
| --- | --- |
| `src/Wled.Fx/Math` | `FastMath`, `Perlin`, `Rng`/`Prng`, `Clock`/`Beat` - the fixed-point layer |
| `src/Wled.Fx/Colors` | `Rgbw`, `Crgb`, `Chsv`/`Chsv32`, `ColorUtil`, `Gamma`, `Palette16`, `Palettes` |
| `src/Wled.Fx/Segment.cs` | segment state, geometry, palettes, transitions |
| `src/Wled.Fx/SegmentDraw.cs` | pixel access, the 1D-onto-2D mappings, fades, blurs, shapes |
| `src/Wled.Fx/LedStrip.cs` | the frame loop, segment compositing, blend modes |
| `src/Wled.Fx/Effects` | the effects themselves, plus the registry and metadata parser |
| `src/Wled.Fx.Demo` | `wledfx`, a terminal preview |
| `tests/Wled.Fx.Tests` | math and colour parity checks, engine tests, an all-effects smoke test |

## What is ported

**150 of the 217 effect IDs**, which is every effect that does not need a subsystem outside the
scope of a rendering library. Effect IDs match the WLED protocol, so a preset or JSON payload
selects the same effect here as on a device.

Not ported:

- **Audio reactive (30 effects)** - Gravcenter, Freqwave, Matripix, GEQ and the rest need a live FFT
  and volume feed from the AudioReactive usermod. `EffectMetadata.Dimensions` still reports
  `Volume`/`Frequency` for them, and [External data](#external-data) is the seam an audio source
  would publish through, so the slots are ready if you want to add one.
- **Particle system (31 effects)** - the PS effects are a thin layer over `FXparticleSystem.cpp`, a
  separate ~3000-line physics engine that would be its own port.
- **Six others** with external dependencies or unusual size: Image (GIF decoding from a filesystem),
  Scrolling Text (the font manager), Pac-Man, Shimmer, TV Simulator and Slow Transition.

All 72 built-in palettes are ported, including the 59 cpt-city gradients, with their gamma
correction intact.

## How the port is put together

**The math is bit-exact.** `Sin8`, `Scale8`, the Perlin gradient hashing, the blend and fade
routines - all of it reproduces the integer behaviour of the C++, including its quirks. `Rgbw.Add`
without ratio preservation leaves the red carry bit sitting in the white channel, for instance; the
firmware does that too, and the test suite asserts it. An effect that looks right on a device looks
right here.

**Effect state is typed.** The firmware hands each effect an untyped byte blob and lets it cast.
Here an effect asks for what it wants:

```csharp
Ball[] balls = seg.GetData<Ball>(MaxNumBalls);
```

The engine keeps the array alive between frames, clears it on reset, and re-allocates when the shape
changes. Same lifetime, no pointer casting.

**Coordinates are virtual.** Effects draw in the space left after grouping, spacing, mirroring and
transposition; `LedStrip.BlendSegment` expands that onto the physical strip. So `Segment.Length`,
`Width` and `Height` are the virtual dimensions (the C++ `SEGLEN`, `SEG_W`, `SEG_H` macros) and the
raw geometry is exposed separately as `PhysicalLength` and friends. The inversion is deliberate:
effect code reads better for it.

**Globals became objects**, with two exceptions. `Clock` and `Rng` are static, because effects call
them constantly and threading them through every signature would be noise. Both are controllable:
`Clock.Freeze` pins time and `Rng.Seed` fixes the sequence, which is what makes rendering
reproducible in tests. Everything else - the strip, the segments, the palettes - is an object.

### Naming

| C++ | C# |
| --- | --- |
| `WS2812FX` | `LedStrip` |
| `SEGMENT` / `SEGENV` | the `Segment` passed to the effect |
| `SEGLEN`, `SEG_W`, `SEG_H` | `seg.Length`, `seg.Width`, `seg.Height` |
| `SEGCOLOR(x)` | `seg.Color(x)` |
| `SEGPALETTE` | `seg.CurrentPalette` |
| `strip.now` | `seg.Now` |
| `SEGENV.allocateData(n)` | `seg.GetData<T>(n)` |
| `um_data_t` | `IModuleData` |
| `UsermodManager::getUmData(&d, id)` | `seg.GetModuleData<T>(id)` |
| `USERMOD_ID_*` | `ModuleId` |
| `color_blend`, `color_add`, `color_fade` | `Rgbw.Blend`, `.Add`, `.Fade` |
| `beatsin8_t`, `perlin8` | `Beat.Sin8`, `Perlin.Noise8` |

## Trying it

```bash
dotnet run --project src/Wled.Fx.Demo -- list
```

```bash
dotnet run --project src/Wled.Fx.Demo -- play Fireworks --length 80 --seconds 5
```

```bash
dotnet run --project src/Wled.Fx.Demo -- play "Black Hole" --length 24 --height 12
```

The preview needs a terminal with 24-bit colour. `list` takes a filter, and `palettes` prints every
built-in palette as a swatch.

## Tests

```bash
dotnet test
```

202 tests. Beyond the math and colour parity checks, `EffectSmokeTests` runs every registered effect
for a stretch of frames on five strip shapes - one pixel, two pixels, a long strip, a square matrix,
a wide matrix - with the sliders at both extremes, and asserts that each one lights something. Hand
translating tight integer code invites off-by-one indexing, and that test is where it surfaces.

Test parallelisation is disabled at the assembly level: the tests drive the shared clock.

## Writing a new effect

An effect is a method that draws one frame:

```csharp
public static void Sweep(Segment seg)
{
    seg.FadeOut(200);
    int pos = (int)(seg.Now / 20 % (uint)seg.Length);
    seg.SetPixelColor(pos, seg.ColorFromPalette(pos, mapping: true, moving: false, colorSlot: 0));
}

EffectRegistry.Register(id: 200, "My Sweep@!,Trail;!,!;!;1", Sweep);
```

The metadata string is the WLED format - name, slider labels, colour labels, palette label, flags,
defaults - and `EffectMetadata` parses it, so the sliders and defaults behave as they would on a
device. The format is documented at
<https://kno.wled.ge/interfaces/json-api/#effect-metadata>.

Every ID from 0 to 219 is claimed by the WLED protocol, so a custom effect takes over one that is
not ported - 200, above, is Particle Ghost Rider.

## External data

An effect is handed nothing but its `Segment`, so live host state - a download percentage, a sensor
reading, an audio spectrum - has to reach it some other way. The firmware's answer is a side channel:
a usermod publishes a `um_data_t` bag and the effect pulls it back out by module ID inside its own
body. Same seam here. The host publishes whenever the value changes:

```csharp
public sealed record ProgressData(float Fraction, bool Indeterminate = false) : IModuleData;

strip.Modules.Publish(ModuleId.UserBase, new ProgressData(0.42f));
```

and the effect looks it up as it draws:

```csharp
// the getAudioData() shape: read the module, fall back to a simulation, never return null
private static ProgressData GetProgress(Segment seg)
    => seg.GetModuleData<ProgressData>(ModuleId.UserBase) ?? Simulate(seg);

public static void Progress(Segment seg)
{
    ProgressData progress = GetProgress(seg);
    int target = (int)MathF.Round(Math.Clamp(progress.Fraction, 0f, 1f) * seg.Length);

    for (int i = 0; i < seg.Length; i++)
    {
        seg.SetPixelColor(i, i < seg.Aux1
            ? seg.ColorFromPalette(i, true, seg.PaletteSolidWrap, 0)
            : seg.Color(1));
    }

    // ease towards the reported level rather than snapping to it
    int size = 1 + ((seg.Speed * seg.Length) >> 11);
    if (target > seg.Aux1) seg.Aux1 = (ushort)Math.Min(seg.Aux1 + size, target);
    else if (target < seg.Aux1) seg.Aux1 = (ushort)Math.Max(seg.Aux1 > size ? seg.Aux1 - size : 0, target);
}
```

Three details carry the pattern, and all three are the firmware's. The lookup is **keyed by module
ID**, so several producers coexist and an effect takes only what it understands. The data is **read
fresh every frame** rather than captured when the effect is registered, so the effect sees the
current value and nothing has to be threaded through the signature. And the lookup **may come back
empty** - `GetModuleData` returns null when nobody is publishing - so an effect supplies its own
fallback, the way the audio effects fall back to `simulateSound()`. That is what keeps it drawing
something in a mode list with no host attached.

Publishing is a single reference store, so a worker thread can publish while the render thread is
mid-frame: make the published type immutable and republish on each update, and there is nothing to
lock. `ModuleId` carries the firmware's `USERMOD_ID_*` values, and IDs from `ModuleId.UserBase` up
are yours. The registry lives on the strip rather than in a global, so two strips do not see each
other's data.

The complete effect, and a demo that drives it from a worker thread, are in
`src/Wled.Fx.Demo/ProgressEffect.cs`:

```bash
dotnet run --project src/Wled.Fx.Demo -- progress --seconds 8
```

```bash
dotnet run --project src/Wled.Fx.Demo -- play Progress --palette 11 --seconds 5
```

The second runs the same effect with nothing published, on the simulated fallback.

## Presets

A device's `presets.json` is an object keyed by preset ID, and each value is a saved state - the
same shape the JSON API takes over HTTP:

```json
{"0":{},"1":{"n":"Sunset","on":true,"bri":200,"seg":[{"id":0,"start":0,"stop":60,"fx":2,"sx":60,"pal":35,"col":[[255,160,0],[64,0,96],[0,0,0]]}]}}
```

So recalling one is what the firmware does in `handlePresets()`: pull the object out by key, hand it
to `deserializeState()`, carry on rendering. `src/Wled.Fx.Demo/PresetLoader.cs` is a port of that
path - `deserializeState` and `deserializeSegment` from `json.cpp`, on top of the value syntax in
`util.cpp` - and it lives in the demo rather than the library, because reading files and parsing
JSON is a host's job and the engine deliberately does neither.

```csharp
JsonObject file = PresetLoader.LoadFile("presets.json");
(string id, JsonObject preset) = PresetLoader.Find(file, "Sunset")!.Value;

var strip = new LedStrip(length: 60);
new PresetLoader(strip).ApplyPreset(preset);

while (running) strip.Service();
```

```bash
dotnet run --project src/Wled.Fx.Demo -- preset src/Wled.Fx.Demo/presets.sample.json
```

```bash
dotnet run --project src/Wled.Fx.Demo -- preset src/Wled.Fx.Demo/presets.sample.json Sunset --length 60
```

The key is the ID the preset is filed under or its `n` name; with no key the file is listed. Size
the strip the way the device was, since segment bounds are absolute pixel positions.

What the loader reproduces, beyond the obvious mapping of `fx`, `pal`, `sx`, `ix`, `c1`-`c3` and
`col` onto the segment, is the parts of the API that are easy to get wrong:

- **Values are expressions.** `"sx":"~20"` means twenty faster than it is now, `"~-"` one slower,
  `"w~10"` wraps off one end onto the other, `"r"` is random and `"1~5~"` cycles inside a range.
  `"on":"t"` toggles. A loader that reads only numbers silently does the wrong thing on any preset
  written by hand or by a button macro.
- **Colours arrive five ways**: `[255,160,0]` channel arrays, `"FF8000"` hex (or `"FFA00040"` with
  white), `{"g":200}` for one channel on its own, a bare `2700` read as a colour temperature, and
  `"r"` for a random hue kept clear of the last one.
- **Segments are addressed, not replaced.** An element with an `id` past the end appends, `"stop":0`
  deletes, a bare object with no `id` applies to every selected segment, and one left out of the
  array keeps whatever it was doing. `"rpt"` tiles a segment across the rest of the strip,
  alternating direction, and `"i"` paints individual pixels and freezes the segment.
- **Geometry is applied before the effect**, and only when it actually changed, so a preset that
  switches effect alone does not throw away the pixel buffer.

Left out is everything that is not rendering: nightlight, playlists, UDP sync, the `win` HTTP API
bridge, `ps` preset chaining, ledmaps and reboot requests. Those keys are collected in
`PresetLoader.Skipped` and printed by the demo rather than dropped in silence, and a preset that
holds a playlist is refused with an explanation rather than half applied.

## Licence

[EUPL v1.2 or later](LICENSE), the same terms as the WLED code this was translated from. Portions
derive from FastLED 3.6.0 and remain under the MIT licence; those, and the provenance of the
cpt-city palettes, are set out in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
