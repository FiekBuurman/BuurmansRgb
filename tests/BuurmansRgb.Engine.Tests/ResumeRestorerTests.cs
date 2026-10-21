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
    public void SuspendingBeforeTheLastRestore_DropsTheRestoresThatAreLeft()
    {
        _events.RaiseResumed();
        _time.Advance(First);

        _events.RaiseSuspending();
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(1, _restores);
    }

    [Fact]
    public void SuspendingAndResumedAgain_RestoresAfterEachDelayOnceMore()
    {
        _events.RaiseResumed();
        _events.RaiseSuspending();

        _events.RaiseResumed();
        _time.Advance(First);
        _time.Advance(Second - First);

        Assert.Equal(2, _restores);
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
    public void Resumed_RaisesRestoredOnceAfterTheFirstRestore()
    {
        var restoresWhenRaised = new List<int>();
        _restorer.Restored += (_, _) => restoresWhenRaised.Add(_restores);

        _events.RaiseResumed();
        _time.Advance(First - TimeSpan.FromMilliseconds(1));
        Assert.Empty(restoresWhenRaised);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        _time.Advance(Second - First);

        Assert.Equal([1], restoresWhenRaised);
    }

    [Fact]
    public void ResumedTwice_RaisesRestoredForEachWakeUp()
    {
        var raised = 0;
        _restorer.Restored += (_, _) => raised++;

        _events.RaiseResumed();
        _time.Advance(Second);
        _events.RaiseSuspending();
        _events.RaiseResumed();
        _time.Advance(Second);

        Assert.Equal(2, raised);
    }

    [Fact]
    public void SuspendingBeforeTheFirstRestore_RaisesNoRestored()
    {
        var raised = 0;
        _restorer.Restored += (_, _) => raised++;

        _events.RaiseResumed();
        _events.RaiseSuspending();
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(0, raised);
    }

    // The devices may still be back with the next restore.
    [Fact]
    public void RestoreThrows_StillRaisesRestored()
    {
        var raised = 0;
        using var restorer = new ResumeRestorer(_events, () => throw new InvalidOperationException("Restoring failed."), _time, [First, Second]);
        restorer.Restored += (_, _) => raised++;

        _events.RaiseResumed();
        _time.Advance(First);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void RestoredListenerThrows_KeepsRestoringLater()
    {
        _restorer.Restored += (_, _) => throw new InvalidOperationException("Loading failed.");

        _events.RaiseResumed();
        _time.Advance(First);
        _time.Advance(Second - First);

        Assert.Equal(2, _restores);
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
}
