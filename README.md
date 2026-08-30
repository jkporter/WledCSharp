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
  `Volume`/`Frequency` for them, so the slots are ready if you want to add an audio source.
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

196 tests. Beyond the math and colour parity checks, `EffectSmokeTests` runs every registered effect
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

## Licence

[EUPL v1.2 or later](LICENSE), the same terms as the WLED code this was translated from. Portions
derive from FastLED 3.6.0 and remain under the MIT licence; those, and the provenance of the
cpt-city palettes, are set out in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
