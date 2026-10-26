using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

// Rainbow, wave and the spread color loop run over the places of a direction as they run over the
// index: a place of i / n is LED i.
public sealed class DirectedEffectTests
{
    private const int LedCount = 7;

    private static readonly TimeSpan Time = TimeSpan.FromSeconds(1.3);

    public static TheoryData<string> Effects => [nameof(RainbowEffect), nameof(WaveEffect), nameof(ColorLoopEffect)];

    [Theory]
    [MemberData(nameof(Effects))]
    public void Render_PlacesOfTheIndex_IsTheImageByIndex(string name)
    {
        var effect = Create(name);
        var byIndex = new RgbColor[LedCount];
        var byPlace = new RgbColor[LedCount];

        effect.Render(byIndex, Time);
        effect.Render(byPlace, [.. Enumerable.Range(0, LedCount).Select(i => i / (double)LedCount)], Time);

        Assert.Equal(byIndex, byPlace);
    }

    // Places in the other order, as a strip at 180 degrees, turn the image round.
    [Theory]
    [MemberData(nameof(Effects))]
    public void Render_PlacesTheOtherWay_TurnsTheImageRound(string name)
    {
        var effect = Create(name);
        var byIndex = new RgbColor[LedCount];
        var turned = new RgbColor[LedCount];

        effect.Render(byIndex, Time);
        effect.Render(turned, [.. Enumerable.Range(0, LedCount).Select(i => (LedCount - 1 - i) / (double)LedCount)], Time);

        Assert.Equal(byIndex.Reverse(), turned);
    }

    // LEDs at the same place, such as two keys in one column at 0 degrees, show the same color.
    [Theory]
    [MemberData(nameof(Effects))]
    public void Render_LedsAtOnePlace_ShowOneColor(string name)
    {
        var leds = new RgbColor[3];

        Create(name).Render(leds, [0.25, 0.25, 0.75], Time);

        Assert.Equal(leds[0], leds[1]);
        Assert.NotEqual(leds[0], leds[2]);
    }

    [Theory]
    [MemberData(nameof(Effects))]
    public void Render_NotOnePlacePerLed_Throws(string name) =>
        Assert.Throws<ArgumentException>(() => Create(name).Render(new RgbColor[3], [0.0, 0.5], Time));

    [Fact]
    public void Render_ColorLoopNotSpread_ShowsOneColorWhereverTheLedsSit()
    {
        var effect = new ColorLoopEffect([Red, Green, Blue], spread: false);
        var byIndex = new RgbColor[3];
        var byPlace = new RgbColor[3];

        effect.Render(byIndex, Time);
        effect.Render(byPlace, [0.6, 0.1, 0.3], Time);

        Assert.Equal(byIndex, byPlace);
    }

    private static IDirectedEffect Create(string name) => name switch
    {
        nameof(RainbowEffect) => new RainbowEffect(),
        nameof(WaveEffect) => new WaveEffect(Orange),
        _ => new ColorLoopEffect([Red, Green, Blue]),
    };
}
