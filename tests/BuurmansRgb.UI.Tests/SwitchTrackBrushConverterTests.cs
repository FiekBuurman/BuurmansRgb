using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

public sealed class SwitchTrackBrushConverterTests
{
    [Theory]
    [InlineData(255, 136, 0)]
    [InlineData(0, 0, 128)]
    [InlineData(200, 200, 200)]
    public void Lift_ABrightEnoughColor_StaysAsItIs(byte r, byte g, byte b) =>
        Assert.Equal(new RgbColor(r, g, b), SwitchTrackBrushConverter.Lift(new RgbColor(r, g, b)));

    // The dark knob would vanish on such a track; the hue stays.
    [Fact]
    public void Lift_ADarkColor_KeepsItsHueUpToMinChannel() =>
        Assert.Equal(new RgbColor(128, 64, 0), SwitchTrackBrushConverter.Lift(new RgbColor(32, 16, 0)));

    [Fact]
    public void Lift_Black_BecomesGrey() =>
        Assert.Equal(new RgbColor(128, 128, 128), SwitchTrackBrushConverter.Lift(RgbColor.Black));
}
