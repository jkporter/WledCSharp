using Wled.Fx;
using Xunit;

namespace Wled.Fx.Tests;

/// <summary>
/// Checks the fixed-point primitives against values taken from the C++ implementation, so a
/// regression in the math layer shows up here rather than as a subtly wrong-looking effect.
/// </summary>
public class FastMathTests
{
    [Theory]
    [InlineData(0, 128)]
    [InlineData(64, 255)]
    [InlineData(128, 126)]
    [InlineData(192, 0)]
    public void Sin8_HitsItsKeyPoints(byte theta, byte expected)
        => Assert.Equal(expected, FastMath.Sin8(theta));

    [Fact]
    public void Sin8_IsCentredOn128()
    {
        // the wave should spend as much time above the midpoint as below it
        int above = 0, below = 0;
        for (int i = 0; i < 256; i++)
        {
            byte v = FastMath.Sin8((byte)i);
            if (v > 128) above++;
            else if (v < 128) below++;
        }
        Assert.Equal(above, below);
    }

    [Fact]
    public void Sin16_SpansTheFullSignedRange()
    {
        Assert.Equal(0, FastMath.Sin16(0));
        Assert.InRange(FastMath.Sin16(0x4000), (short)32700, (short)32767);
        Assert.InRange(FastMath.Sin16(0xC000), (short)-32767, (short)-32700);
    }

    [Fact]
    public void Cos8_IsSin8ShiftedByAQuarterTurn()
    {
        for (int i = 0; i < 256; i++)
            Assert.Equal(FastMath.Sin8((byte)(i + 64)), FastMath.Cos8((byte)i));
    }

    [Theory]
    [InlineData(255, 255, 255)]
    [InlineData(255, 0, 0)]
    [InlineData(128, 128, 64)]
    [InlineData(0, 255, 0)]
    public void Scale8_MatchesTheIntegerFormula(byte value, byte scale, byte expected)
        => Assert.Equal(expected, FastMath.Scale8(value, scale));

    [Fact]
    public void QAdd8_And_QSub8_Saturate()
    {
        Assert.Equal(255, FastMath.QAdd8(200, 100));
        Assert.Equal(0, FastMath.QSub8(100, 200));
    }

    [Fact]
    public void Sqrt32_IsExact()
    {
        foreach (uint n in new uint[] { 0, 1, 2, 3, 4, 15, 16, 17, 1000, 65535, 65536, 1000000 })
            Assert.Equal((uint)System.Math.Sqrt(n), FastMath.Sqrt32(n));
    }

    [Fact]
    public void TriWave8_PeaksInTheMiddle()
    {
        Assert.Equal(0, FastMath.TriWave8(0));
        Assert.Equal(254, FastMath.TriWave8(127));
        Assert.Equal(0, FastMath.TriWave8(255));
    }

    [Fact]
    public void Perlin_StaysWithinItsDocumentedRange()
    {
        for (uint x = 0; x < 4000; x += 37)
        {
            int raw = Perlin.Raw1D(x << 8);
            Assert.InRange(raw, -24691, 24689);
        }
    }

    [Fact]
    public void Perlin_IsContinuous()
    {
        // neighbouring samples of a smooth field must not jump
        byte previous = Perlin.Noise8(0, 0);
        for (ushort x = 1; x < 500; x++)
        {
            byte current = Perlin.Noise8(x, 0);
            Assert.InRange(System.Math.Abs(current - previous), 0, 24);
            previous = current;
        }
    }
}

/// <summary>Checks the colour primitives, which every effect depends on.</summary>
public class ColorTests
{
    [Fact]
    public void Rgbw_PacksAndUnpacksChannels()
    {
        var c = new Rgbw(0x11, 0x22, 0x33, 0x44);
        Assert.Equal(0x44112233u, c.Value);
        Assert.Equal(0x11, c.R);
        Assert.Equal(0x22, c.G);
        Assert.Equal(0x33, c.B);
        Assert.Equal(0x44, c.W);
    }

    [Fact]
    public void Blend_ReachesBothEnds()
    {
        Rgbw a = new(255, 0, 0);
        Rgbw b = new(0, 0, 255);
        Assert.Equal(a, Rgbw.Blend(a, b, 0));
        Assert.Equal(b, Rgbw.Blend(a, b, 255));
    }

    [Fact]
    public void Blend_IsMonotonic()
    {
        Rgbw a = new(0, 0, 0);
        Rgbw b = new(255, 255, 255);
        int previous = -1;
        for (int i = 0; i <= 255; i++)
        {
            int r = Rgbw.Blend(a, b, (byte)i).R;
            Assert.True(r >= previous);
            previous = r;
        }
    }

    [Fact]
    public void Add_SaturatesWithoutPreservingRatio()
    {
        Rgbw sum = new Rgbw(200, 200, 200).Add(new Rgbw(100, 100, 100));
        Assert.Equal(255, sum.R);
        Assert.Equal(255, sum.G);
        Assert.Equal(255, sum.B);
        // The branchless saturation leaves the carry bit of red sitting in the white channel, so an
        // overflowing add bumps white by one. The firmware does the same; it is invisible on RGB
        // strips and reproducing it keeps the port bit-identical.
        Assert.Equal(1, sum.W);
    }

    [Fact]
    public void Add_KeepsTheHueWhenPreservingRatio()
    {
        Rgbw sum = new Rgbw(200, 100, 0).Add(new Rgbw(200, 100, 0), preserveRatio: true);
        Assert.Equal(254, sum.R); // the fixed-point rescale lands one short of full
        // red and green started 2:1 apart and must stay that way
        Assert.InRange(sum.G, 120, 133);
        Assert.Equal(0, sum.B);
    }

    [Fact]
    public void Fade_ToZeroIsBlack() => Assert.True(new Rgbw(255, 255, 255).Fade(0).IsBlack);

    [Fact]
    public void Fade_Video_KeepsADimColourAlive()
    {
        Rgbw faded = new Rgbw(8, 0, 0).Fade(1, video: true);
        Assert.True(faded.R > 0, "video fading must not extinguish a lit channel");
    }

    [Fact]
    public void HsvToRgb_AndBack_RoundTrips()
    {
        for (int h = 0; h < 65536; h += 1024)
        {
            var hsv = new Chsv32(h, 255, 255);
            Chsv32 back = ColorUtil.RgbToHsv(ColorUtil.HsvToRgbSpectrum(hsv));
            int delta = System.Math.Abs(back.H - hsv.H);
            if (delta > 32768) delta = 65536 - delta; // hue is circular
            Assert.InRange(delta, 0, 600);
        }
    }

    [Fact]
    public void Palette_SamplingInterpolatesBetweenEntries()
    {
        var palette = new Palette16(new Crgb(0, 0, 0), new Crgb(255, 255, 255));
        Assert.Equal(0, palette.ColorAt(0).R);
        Assert.Equal(255, palette.ColorAt(240).R);
        Assert.InRange(palette.ColorAt(120).R, 100, 155);
    }

    [Fact]
    public void GradientPalettes_AllExpandToSixteenEntries()
    {
        for (int id = Palettes.DynamicCount + Palettes.FastLedCount; id < Palettes.FixedCount; id++)
        {
            Palette16 palette = Palettes.Get(id);
            Assert.Equal(Palette16.Size, palette.Entries.Length);
            bool anyLit = false;
            foreach (Crgb entry in palette.Entries) anyLit |= !entry.IsBlack;
            Assert.True(anyLit, $"palette {id} ({Palettes.NameOf(id)}) expanded to all black");
        }
    }

    [Fact]
    public void PaletteBlend_ConvergesOnItsTarget()
    {
        var current = new Palette16(new Crgb(0, 0, 0));
        var target = new Palette16(new Crgb(255, 128, 64));
        for (int i = 0; i < 512; i++) current.BlendToward(target, 48);
        Assert.Equal(target[0], current[0]);
        Assert.Equal(target[15], current[15]);
    }
}

/// <summary>Checks segment geometry, the mappings and the runtime state buffers.</summary>
public class SegmentTests
{
    [Fact]
    public void VirtualLength_AccountsForGroupingAndMirroring()
    {
        var strip = new LedStrip(100);
        Segment seg = strip.MainSegment;

        seg.SetGeometry(0, 100);
        Assert.Equal(100, seg.VirtualLength());

        seg.SetGeometry(0, 100, grouping: 2);
        Assert.Equal(50, seg.VirtualLength());

        seg.SetGeometry(0, 100, grouping: 2, spacing: 2);
        Assert.Equal(25, seg.VirtualLength());

        seg.Mirror = true;
        Assert.Equal(13, seg.VirtualLength()); // rounded up, so a single pixel always survives
    }

    [Fact]
    public void Fill_ReachesEveryPixelOfTheStrip()
    {
        var strip = new LedStrip(30);
        strip.Brightness = 255;
        Segment seg = strip.MainSegment;
        seg.BeginDraw();
        seg.Fill(new Rgbw(10, 20, 30));
        strip.Show();

        for (int i = 0; i < strip.Length; i++) Assert.Equal(new Rgbw(10, 20, 30), strip.GetPixelColor(i));
    }

    [Fact]
    public void Reverse_FlipsTheSegmentOntoTheStrip()
    {
        var strip = new LedStrip(4);
        Segment seg = strip.MainSegment;
        seg.Reverse = true;
        seg.BeginDraw();
        seg.SetPixelColor(0, new Rgbw(255, 0, 0));
        strip.Show();

        Assert.Equal(new Rgbw(255, 0, 0), strip.GetPixelColor(3));
        Assert.True(strip.GetPixelColor(0).IsBlack);
    }

    [Fact]
    public void Grouping_ExpandsEachVirtualPixel()
    {
        var strip = new LedStrip(8);
        Segment seg = strip.MainSegment;
        seg.SetGeometry(0, 8, grouping: 2);
        seg.BeginDraw();
        seg.SetPixelColor(0, new Rgbw(255, 0, 0));
        strip.Show();

        Assert.Equal(new Rgbw(255, 0, 0), strip.GetPixelColor(0));
        Assert.Equal(new Rgbw(255, 0, 0), strip.GetPixelColor(1));
        Assert.True(strip.GetPixelColor(2).IsBlack);
    }

    [Fact]
    public void Opacity_ScalesWhatReachesTheStrip()
    {
        var strip = new LedStrip(4);
        Segment seg = strip.MainSegment;
        seg.Opacity = 128;
        seg.BeginDraw();
        seg.Fill(new Rgbw(255, 255, 255));
        strip.Show();

        Assert.InRange(strip.GetPixelColor(0).R, 120, 136);
    }

    [Fact]
    public void TwoDMapping_LaysAOneDimensionalEffectAcrossTheMatrix()
    {
        var strip = new LedStrip(8, 8);
        Segment seg = strip.MainSegment;
        seg.Map1D2D = Mapping1D2D.Bar;
        seg.BeginDraw();

        Assert.Equal(8, seg.Length); // in bar mode the length is the height
        seg.SetPixelColor(0, new Rgbw(255, 0, 0));
        // bar mode lights the bottom row of the matrix
        for (int x = 0; x < 8; x++) Assert.Equal(new Rgbw(255, 0, 0), seg.GetPixelColorXY(x, 7));
    }

    [Fact]
    public void GetData_KeepsStateBetweenFramesAndClearsOnReset()
    {
        var strip = new LedStrip(10);
        Segment seg = strip.MainSegment;

        byte[] first = seg.GetData<byte>(10);
        first[3] = 42;
        Assert.Same(first, seg.GetData<byte>(10));

        seg.MarkForReset();
        seg.ResetIfRequired();
        Assert.Equal(0, seg.GetData<byte>(10)[3]);
    }

    [Fact]
    public void GetData_ReallocatesWhenTheShapeChanges()
    {
        var strip = new LedStrip(10);
        Segment seg = strip.MainSegment;
        byte[] bytes = seg.GetData<byte>(10);
        bytes[0] = 7;
        Assert.NotEqual(7, seg.GetData<byte>(20)[0]);
    }

    [Fact]
    public void Blur_SpreadsLightToNeighbours()
    {
        var strip = new LedStrip(11);
        Segment seg = strip.MainSegment;
        seg.BeginDraw();
        seg.SetPixelColor(5, new Rgbw(255, 255, 255));
        seg.Blur(128);

        Assert.True(seg.GetPixelColor(4).R > 0);
        Assert.True(seg.GetPixelColor(6).R > 0);
        Assert.True(seg.GetPixelColor(5).R < 255);
    }
}

/// <summary>Checks the metadata parser against real effect strings from the firmware.</summary>
public class EffectMetadataTests
{
    [Fact]
    public void Parse_HandlesABareName()
    {
        EffectMetadata meta = EffectMetadata.Parse("Solid");
        Assert.Equal("Solid", meta.Name);
        Assert.Empty(meta.SliderLabels);
        Assert.Null(meta.Defaults.Speed);
    }

    [Fact]
    public void Parse_ReadsLabelsAndFlags()
    {
        EffectMetadata meta = EffectMetadata.Parse("Blink@!,Duty cycle;!,!;!;01");
        Assert.Equal("Blink", meta.Name);
        Assert.Equal(["!", "Duty cycle"], meta.SliderLabels);
        Assert.Equal(["!", "!"], meta.ColorLabels);
        Assert.Equal("!", meta.PaletteLabel);
        Assert.True(meta.Dimensions.HasFlag(EffectDimensions.OneDimensional));
    }

    [Fact]
    public void Parse_ReadsDefaults()
    {
        EffectMetadata meta = EffectMetadata.Parse("Fire 2012@Cooling,Spark rate,,2D Blur,Boost;;!;1;pal=35,sx=64,ix=160,m12=1,c2=128");
        Assert.Equal((byte)64, meta.Defaults.Speed);
        Assert.Equal((byte)160, meta.Defaults.Intensity);
        Assert.Equal((byte)128, meta.Defaults.Custom2);
        Assert.Equal((byte)35, meta.Defaults.Palette);
        Assert.Equal(Mapping1D2D.Bar, meta.Defaults.Map1D2D);
    }

    [Fact]
    public void EveryRegisteredEffect_HasParseableMetadata()
    {
        foreach (EffectInfo effect in EffectRegistry.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(effect.Name), $"effect {effect.Id} has no name");
            Assert.DoesNotContain("@", effect.Name);
        }
    }

    [Fact]
    public void SetMode_AppliesTheDeclaredDefaults()
    {
        var strip = new LedStrip(30);
        Segment seg = strip.MainSegment;
        seg.SetMode(EffectId.Fire2012, loadDefaults: true);

        Assert.Equal(EffectId.Fire2012, seg.Mode);
        Assert.Equal(64, seg.Speed);
        Assert.Equal(160, seg.Intensity);
        Assert.Equal(35, seg.Palette);
    }
}

/// <summary>
/// Runs every registered effect for a stretch of frames on several strip shapes.
/// </summary>
/// <remarks>
/// This is the port safety net: it catches the out-of-range indexing and division by zero that a
/// hand translation of tight integer code invites, on the awkward geometries (one pixel, two
/// pixels, a wide matrix) where such bugs actually live.
/// </remarks>
public class EffectSmokeTests
{
    public static TheoryData<byte> AllEffects()
    {
        var data = new TheoryData<byte>();
        foreach (EffectInfo effect in EffectRegistry.All) data.Add(effect.Id);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllEffects))]
    public void EveryEffect_RendersWithoutThrowing(byte id)
    {
        foreach ((int width, int height) in new[] { (1, 1), (2, 1), (60, 1), (16, 16), (32, 8) })
        {
            Rng.Seed(12345);
            Clock.Freeze(0);

            var strip = new LedStrip(width, height);
            Segment seg = strip.MainSegment;
            seg.SetMode(id, loadDefaults: true);
            seg.StopTransition(); // the mode change queued one; the smoke test wants the effect alone

            for (int frame = 0; frame < 12; frame++)
            {
                Clock.Advance(24);
                strip.Trigger();
                strip.Service();
            }

            // sliders at their extremes are where the integer math is most likely to trip
            foreach (byte value in new byte[] { 0, 255 })
            {
                seg.Speed = value;
                seg.Intensity = value;
                seg.Custom1 = value;
                seg.Custom2 = value;
                seg.Custom3 = (byte)(value & 0x1F);
                seg.MarkForReset();
                for (int frame = 0; frame < 6; frame++)
                {
                    Clock.Advance(24);
                    strip.Trigger();
                    strip.Service();
                }
            }
        }
        Clock.Freeze(null);
    }

    [Fact]
    public void EveryEffect_ActuallyLightsSomething()
    {
        // Solid with a black colour is the only effect allowed to stay dark, so give every effect a
        // colour and a palette and check that at least one frame in a second is not blank.
        var skipped = new List<string>();
        foreach (EffectInfo effect in EffectRegistry.All)
        {
            // Copy Segment mirrors another segment, and there is only one here, so it correctly
            // fades to black.
            if (effect.Id == EffectId.Copy) continue;

            Rng.Seed(999);
            Clock.Freeze(0);
            var strip = new LedStrip(60);
            strip.Brightness = 255;
            Segment seg = strip.MainSegment;
            seg.SetMode(effect.Id, loadDefaults: true);
            seg.StopTransition();
            seg.SetColor(0, new Rgbw(255, 160, 0));
            seg.SetColor(1, new Rgbw(0, 0, 64));
            seg.SetPalette(11); // Rainbow
            seg.StopTransition();

            bool litSomething = false;
            for (int frame = 0; frame < 42 && !litSomething; frame++)
            {
                Clock.Advance(25);
                strip.Trigger();
                strip.Service();
                foreach (Rgbw pixel in strip.Pixels) litSomething |= !pixel.IsBlack;
            }
            if (!litSomething) skipped.Add(effect.Name);
        }
        Clock.Freeze(null);
        Assert.True(skipped.Count == 0, $"effects that never lit a pixel: {string.Join(", ", skipped)}");
    }
}

/// <summary>Checks the frame loop, blending and transitions.</summary>
public class StripTests
{
    [Fact]
    public void Service_RespectsTheTargetFrameRate()
    {
        Clock.Freeze(0);
        var strip = new LedStrip(30) { TargetFps = 40 }; // 25ms per frame
        int frames = 0;
        strip.FrameReady += _ => frames++;

        for (int i = 0; i < 10; i++)
        {
            Clock.Advance(10);
            strip.Service();
        }
        Clock.Freeze(null);

        Assert.InRange(frames, 3, 5); // 100ms of clock at 25ms per frame
    }

    [Fact]
    public void SegmentsBlendOntoEachOther()
    {
        var strip = new LedStrip(10) { Brightness = 255 };
        strip.MainSegment.SetGeometry(0, 10);
        strip.MainSegment.BeginDraw();
        strip.MainSegment.Fill(new Rgbw(255, 0, 0));

        Segment top = strip.AddSegment(0, 5);
        top.BlendMode = BlendMode.Add;
        top.BeginDraw();
        top.Fill(new Rgbw(0, 255, 0));

        strip.Show();

        Assert.Equal(new Rgbw(255, 255, 0), strip.GetPixelColor(0)); // both segments
        Assert.Equal(new Rgbw(255, 0, 0), strip.GetPixelColor(9));   // only the bottom one
    }

    [Fact]
    public void Transition_FadesTheOldColourIntoTheNew()
    {
        Clock.Freeze(1000);
        var strip = new LedStrip(4) { Brightness = 255, TransitionDuration = 1000 };
        Segment seg = strip.MainSegment;
        seg.SetColor(0, new Rgbw(255, 0, 0));
        seg.StopTransition();

        seg.SetColor(0, new Rgbw(0, 0, 255)); // starts a transition
        Assert.True(seg.IsInTransition);

        Clock.Freeze(1500); // halfway
        seg.HandleTransition();
        seg.BeginDraw(seg.Progress);
        Assert.InRange(seg.Color(0).R, 100, 155);
        Assert.InRange(seg.Color(0).B, 100, 155);

        Clock.Freeze(2100); // past the end
        seg.HandleTransition();
        Assert.False(seg.IsInTransition);
        Clock.Freeze(null);
    }

    [Fact]
    public void CopyOutput_AppliesGlobalBrightness()
    {
        var strip = new LedStrip(4) { Brightness = 128 };
        strip.MainSegment.BeginDraw();
        strip.MainSegment.Fill(new Rgbw(255, 255, 255));
        strip.Show();

        var output = new Rgbw[strip.Length];
        strip.CopyOutput(output);
        Assert.InRange(output[0].R, 120, 136);
    }
}
