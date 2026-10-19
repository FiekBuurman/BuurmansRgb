using BuurmansRgb.Engine.Power;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

public sealed class ResumeRestorerTests : IDisposable
{
    private static readonly TimeSpan First = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(10);

    private readonly FakePowerEvents _events = new();
    private readonly FakeTimeProvider _time = new();
    private readonly ResumeRestorer _restorer;
    private int _restores;

    public ResumeRestorerTests() => _restorer = new ResumeRestorer(_events, () => _restores++, _time, [First, Second]);

    public void Dispose() => _restorer.Dispose();

    [Fact]
    public void DefaultDelays_RestoreSoonAndOnceMoreLater() =>
        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10)], ResumeRestorer.DefaultDelays);

    [Fact]
    public void Resumed_RestoresAfterEachDelay()
    {
        _events.RaiseResumed();

        _time.Advance(First - TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, _restores);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, _restores);
        _time.Advance(Second - First - TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, _restores);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(2, _restores);
        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(2, _restores);
    }

    [Fact]
    public void WithoutResume_NeverRestores()
    {
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(0, _restores);
    }

    [Fact]
    public void ResumedAgain_StartsOver()
    {
        _events.RaiseResumed();
        _time.Advance(First);

        _events.RaiseResumed();
        _time.Advance(First);
        _time.Advance(Second - First);

        Assert.Equal(3, _restores);
    }

    [Fact]
    public void RestoreThrows_KeepsRestoringLater()
    {
        var restores = 0;
        using var restorer = new ResumeRestorer(_events, () =>
        {
            restores++;
            throw new InvalidOperationException("Restoring failed.");
        }, _time, [First, Second]);

        _events.RaiseResumed();
        _time.Advance(First);
        _time.Advance(Second - First);

        Assert.Equal(2, restores);
    }

    [Fact]
    public void Dispose_StopsListeningAndRestoring()
    {
        _events.RaiseResumed();

        _restorer.Dispose();
        _time.Advance(Second);
        _events.RaiseResumed();
        _time.Advance(Second);

        Assert.Equal(0, _restores);
        Assert.False(_events.HasListeners);
    }

    [Fact]
    public void Create_WithoutDelays_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResumeRestorer(_events, () => { }, _time, []));

    [Theory]
    [InlineData(-1)]
    [InlineData(10, 2)]
    [InlineData(2, 2)]
    public void Create_WithDelaysThatDoNotGoUp_Throws(params int[] seconds) =>
        Assert.Throws<ArgumentException>(() =>
            new ResumeRestorer(_events, () => { }, _time, [.. seconds.Select(second => TimeSpan.FromSeconds(second))]));

    private sealed class FakePowerEvents : IPowerEvents
    {
        public event EventHandler? Resumed;

        public bool HasListeners => Resumed is not null;

        public void RaiseResumed() => Resumed?.Invoke(this, EventArgs.Empty);
    }
}
