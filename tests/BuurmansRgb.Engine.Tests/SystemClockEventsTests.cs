using BuurmansRgb.Engine.Power;

namespace BuurmansRgb.Engine.Tests;

public sealed class SystemClockEventsTests
{
    private readonly FakePowerEvents _power = new();

    [Fact]
    public void Resumed_RaisesChanged()
    {
        using var clock = new SystemClockEvents(_power);
        var changes = 0;
        clock.Changed += (_, _) => changes++;

        _power.RaiseResumed();

        Assert.Equal(1, changes);
    }

    [Fact]
    public void Dispose_StopsListening()
    {
        var clock = new SystemClockEvents(_power);

        clock.Dispose();

        Assert.False(_power.HasListeners);
    }
}
