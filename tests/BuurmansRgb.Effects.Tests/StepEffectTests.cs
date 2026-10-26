using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

public sealed class StepEffectTests
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.99)]
    public void Render_DuringTheHold_ShowsTheStep(double seconds)
    {
        var effect = new StepEffect([Step(Red, 1, 1), Step(Blue, 1, 1)]);

        Assert.All(effect.RenderAt(3, seconds), led => Assert.Equal(Red, led));
    }

    // Halfway into the fade of the first step, halfway to the second.
    [Fact]
    public void Render_DuringTheFade_BlendsIntoTheNextStep()
    {
        var effect = new StepEffect([Step(Red, 1, 2), Step(Blue, 1, 1)]);

        Assert.All(effect.RenderAt(3, 2.0), led => Assert.Equal(RgbColor.Lerp(Red, Blue, 0.5), led));
    }

    // The fade of the last step runs into the first, and then the round starts again.
    [Theory]
    [InlineData(3.5, 0.5)]
    [InlineData(4.0, 0.0)]
    [InlineData(4.5, 0.0)]
    public void Render_AfterTheLastStep_GoesBackToTheFirst(double seconds, double towardsRed)
    {
        var effect = new StepEffect([Step(Red, 1, 1), Step(Blue, 1, 1)]);

        Assert.All(effect.RenderAt(2, seconds), led => Assert.Equal(towardsRed == 0.0 ? Red : RgbColor.Lerp(Blue, Red, towardsRed), led));
    }

    [Fact]
    public void Render_WithoutFade_JumpsToTheNextStep()
    {
        var effect = new StepEffect([Step(Red, 1, 0), Step(Green, 1, 0), Step(Blue, 1, 0)]);

        Assert.Equal([Red, Green, Blue, Red], [.. new[] { 0.5, 1.5, 2.5, 3.5 }.Select(seconds => effect.RenderAt(1, seconds)[0])]);
    }

    // A pattern as long as the LEDs is copied, one color per LED.
    [Fact]
    public void Render_PatternOfTheSameLength_ShowsItsColors()
    {
        var effect = new StepEffect([new EffectStep([Red, Green, Blue], OneSecond, OneSecond)]);

        Assert.Equal([Red, Green, Blue], effect.RenderAt(3, 0.0));
    }

    // A pattern is stretched over more LEDs, and over fewer each LED takes the color at its middle.
    [Theory]
    [InlineData(6, new[] { 0, 0, 1, 1, 2, 2 })]
    [InlineData(1, new[] { 1 })]
    [InlineData(2, new[] { 0, 2 })]
    public void Render_PatternOfAnotherLength_IsStretched(int ledCount, int[] expected)
    {
        RgbColor[] pattern = [Red, Green, Blue];
        var effect = new StepEffect([new EffectStep(pattern, OneSecond, OneSecond)]);

        Assert.Equal([.. expected.Select(index => pattern[index])], effect.RenderAt(ledCount, 0.0));
    }

    // Each LED blends from its own color in the one step to its own color in the next.
    [Fact]
    public void Render_FadeBetweenPatternAndColor_BlendsEveryLed()
    {
        var effect = new StepEffect([new EffectStep([Red, Blue], OneSecond, OneSecond), Step(RgbColor.Black, 1, 1)]);

        Assert.Equal([Red.Scale(0.5), Blue.Scale(0.5)], effect.RenderAt(2, 1.5));
    }

    // Speed comes from the clock of the device, which gives the effect a faster time.
    [Fact]
    public void Round_IsTheHoldsAndFadesOfAllSteps()
    {
        var effect = new StepEffect([Step(Red, 1, 0.5), Step(Blue, 2, 0.25)]);

        Assert.Equal(TimeSpan.FromSeconds(3.75), effect.Round);
        Assert.Equal(2, effect.StepCount);
    }

    [Fact]
    public void IsAnimated_OneStep_IsFalseAndShowsIt()
    {
        var effect = new StepEffect([Step(Orange, 1, 1)]);

        Assert.False(effect.IsAnimated);
        Assert.All(effect.RenderAt(4, 7.3), led => Assert.Equal(Orange, led));
    }

    // Steps that take no time show the first one and need no frames.
    [Fact]
    public void IsAnimated_StepsWithoutTime_IsFalseAndShowsTheFirst()
    {
        var effect = new StepEffect([Step(Red, 0, 0), Step(Blue, 0, 0)]);

        Assert.False(effect.IsAnimated);
        Assert.All(effect.RenderAt(2, 1.0), led => Assert.Equal(Red, led));
    }

    // A step of no time in between is passed over.
    [Fact]
    public void Render_StepWithoutTime_IsPassedOver()
    {
        var effect = new StepEffect([Step(Red, 1, 0), Step(Green, 0, 0), Step(Blue, 1, 0)]);

        Assert.Equal([Red, Blue], [.. new[] { 0.5, 1.5 }.Select(seconds => effect.RenderAt(1, seconds)[0])]);
    }

    [Fact]
    public void Render_BeforeTheStart_CountsBackFromTheEndOfTheRound()
    {
        var effect = new StepEffect([Step(Red, 1, 0), Step(Blue, 1, 0)]);

        Assert.Equal(Blue, effect.RenderAt(1, -0.5)[0]);
    }

    [Fact]
    public void Constructor_WithoutSteps_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StepEffect([]));
    }

    [Fact]
    public void Constructor_StepWithoutColors_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StepEffect([new EffectStep([], OneSecond, OneSecond)]));
    }

    [Theory]
    [InlineData(-1.0, 0.0)]
    [InlineData(0.0, -1.0)]
    public void Constructor_NegativeTime_Throws(double hold, double fade)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StepEffect([Step(Red, hold, fade)]));
    }

    // The steps are copied, so a list that changes later does not change the effect.
    [Fact]
    public void Constructor_CopiesTheColors()
    {
        var colors = new List<RgbColor> { Red };
        var effect = new StepEffect([new EffectStep(colors, OneSecond, OneSecond)]);

        colors[0] = Blue;

        Assert.Equal(Red, effect.RenderAt(1, 0.0)[0]);
    }

    [Fact]
    public void Render_AllocatesNothing()
    {
        var effect = new StepEffect([new EffectStep([.. Twelve], OneSecond, OneSecond), Step(Blue, 1, 1)]);
        var leds = new RgbColor[104];
        effect.Render(leds, TimeSpan.Zero);

        var allocated = Allocations.During(() =>
        {
            for (var frame = 0; frame < 300; frame++)
            {
                effect.Render(leds, TimeSpan.FromSeconds(frame / 30.0));
            }
        });

        Assert.Equal(0, allocated);
    }

    private static EffectStep Step(RgbColor color, double hold, double fade) =>
        new([color], TimeSpan.FromSeconds(hold), TimeSpan.FromSeconds(fade));
}
