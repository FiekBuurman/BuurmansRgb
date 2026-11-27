using BuurmansRgb.Engine.Profiles;

namespace BuurmansRgb.Engine.Tests;

public sealed class ActiveSceneTrackerTests
{
    private readonly ActiveSceneTracker _tracker = new();

    [Fact]
    public void OnLightingChanged_FromTheThreadOfShowing_KeepsTheScene()
    {
        _tracker.Showing("Avond", _tracker.OnLightingChanged);

        Assert.Equal("Avond", _tracker.Name);
    }

    [Fact]
    public void OnLightingChanged_FromAnotherThreadWhileShowing_EndsTheScene()
    {
        _tracker.Set("Avond");

        _tracker.Showing("Nacht", () =>
        {
            // A thread of its own always has another ManagedThreadId; Task.Run().Wait() may run inline.
            var other = new Thread(_tracker.OnLightingChanged);
            other.Start();
            other.Join();
        });

        Assert.Null(_tracker.Name);
    }

    [Fact]
    public void OnLightingChanged_OutsideShowing_EndsTheScene()
    {
        _tracker.Set("Avond");

        _tracker.OnLightingChanged();

        Assert.Null(_tracker.Name);
    }

    [Fact]
    public void Changed_GoesOffOnlyForAnotherName()
    {
        var count = 0;
        _tracker.Changed += (_, _) => count++;

        _tracker.Set("Avond");
        _tracker.Set("Avond");
        Assert.Equal(1, count);

        _tracker.Set("Nacht");
        _tracker.Set(null);
        Assert.Equal(3, count);
    }
}
