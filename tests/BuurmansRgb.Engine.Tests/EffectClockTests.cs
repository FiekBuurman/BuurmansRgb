using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class EffectClockTests
{
    [Fact]
    public void Tick_NormalSpeed_GivesTheEngineTime()
    {
        var clock = new EffectClock();

        Assert.Equal(TimeSpan.FromSeconds(3), clock.Tick(TimeSpan.FromSeconds(3)));
        Assert.Equal(TimeSpan.FromSeconds(5), clock.Tick(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Tick_FirstTick_StartsAtTimeTimesSpeed()
    {
        var clock = new EffectClock { Speed = 2.0 };

        Assert.Equal(TimeSpan.FromSeconds(6), clock.Tick(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void Tick_NewSpeed_CountsFromTheNextTickOn()
    {
        var clock = new EffectClock();
        clock.Tick(TimeSpan.FromSeconds(10));

        clock.Speed = 0.5;
        var atChange = clock.Tick(TimeSpan.FromSeconds(12));
        var later = clock.Tick(TimeSpan.FromSeconds(16));

        // The two seconds before the change still ran at the normal speed, so nothing jumps.
        Assert.Equal(TimeSpan.FromSeconds(12), atChange);
        Assert.Equal(TimeSpan.FromSeconds(14), later);
    }

    [Fact]
    public void Tick_TwoClocksChangedAtTheSameMoment_StayInStep()
    {
        var first = new EffectClock();
        var second = new EffectClock();
        first.Tick(TimeSpan.FromSeconds(1));
        second.Tick(TimeSpan.FromSeconds(1));

        first.Speed = 3.0;
        second.Speed = 3.0;
        first.Tick(TimeSpan.FromSeconds(2));
        second.Tick(TimeSpan.FromSeconds(2));

        Assert.Equal(first.Tick(TimeSpan.FromSeconds(4)), second.Tick(TimeSpan.FromSeconds(4)));
    }
}
