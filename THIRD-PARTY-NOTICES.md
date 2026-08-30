# Third-party notices

This project is a C# port of [WLED](https://github.com/wled/WLED), which is licensed under the
EUPL v1.2 or later. See [LICENSE](LICENSE).

Parts of WLED derive from other projects and carry their original terms. Those terms follow the
code into this port.

## FastLED

Portions derive from [FastLED](https://github.com/FastLED/FastLED) 3.6.0, by way of WLED's trimmed
copy in `src/dependencies/fastled_slim` and the FastLED-derived routines marked in `colors.cpp` and
`util.cpp`. In this port that covers:

| File | What derives from FastLED |
| --- | --- |
| `src/Wled.Fx/Math/FastMath.cs` | the scaling, saturating-arithmetic, easing and waveform helpers |
| `src/Wled.Fx/Math/Clock.cs` | the `Beat` BPM waveform generators |
| `src/Wled.Fx/Colors/Crgb.cs` | `Crgb` and `Chsv` |
| `src/Wled.Fx/Colors/ColorUtil.cs` | `HsvToRgbRainbow`, `HeatColor`, `ColorFromPalette`, the gradient fills |
| `src/Wled.Fx/Colors/Palette16.cs` | `Palette16` and `BlendToward` |
| `src/Wled.Fx/Colors/Palettes.cs` | the seven FastLED palettes (Party, Cloud, Lava, Ocean, Forest, Rainbow, Rainbow Bands) |

Several effects also originate with FastLED authors and are credited in their own doc comments -
among them Pride 2015 and Colorwaves (Mark Kriegsman), TwinkleFOX (Mark Kriegsman), Pacifica
(Mark Kriegsman and Mary Corey March), Fire 2012 (Mark Kriegsman), Squared Swirl (Mark Kriegsman)
and Metaballs (Stefan Petrick).

```
The MIT License (MIT)

Copyright (c) 2013 FastLED

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

## cpt-city gradient palettes

The 59 gradient palettes in `src/Wled.Fx/Colors/Palettes.Data.cs` were imported by WLED from
[cpt-city](http://soliton.vm.bytemark.co.uk/pub/cpt-city/), where the individual palettes carry the
terms set by their respective authors. They are reproduced here in the same form and with the same
gamma correction WLED applies.
