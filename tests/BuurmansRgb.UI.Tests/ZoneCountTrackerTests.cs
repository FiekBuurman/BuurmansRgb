using BuurmansRgb.Contracts;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The count of LEDs as this settings app follows it, without a preview: which state of the engine
// is taken, and when the count is the one this app started.
public sealed class ZoneCountTrackerTests
{
    private readonly ZoneCountTracker _tracker = new();

    [Fact]
    public void Count_AtFirst_IsNone()
    {
        Assert.Same(ZoneCountInfo.None, _tracker.Count);
    }

    [Fact]
    public void Take_NewerState_IsTheCount()
    {
        var count = Counting("board", 0, sequence: 3);

        Assert.True(_tracker.Take(count));

        Assert.Same(count, _tracker.Count);
    }

    // A step can arrive after the pause that followed it.
    [Fact]
    public void Take_OlderState_IsDropped()
    {
        _tracker.Take(ZoneCountInfo.None with { Sequence = 5 });

        Assert.False(_tracker.Take(Counting("board", 0, sequence: 4)));

        Assert.False(_tracker.Count.IsCounting);
    }

    [Fact]
    public void Take_SameSequence_IsTaken()
    {
        _tracker.Take(Counting("board", 0, sequence: 5));

        Assert.True(_tracker.Take(Counting("board", 0, sequence: 5, led: 2)));

        Assert.Equal(2, _tracker.Count.Led);
    }

    [Fact]
    public void Stop_CountedZone_EndsTheCountAtOnce()
    {
        _tracker.Take(Counting("board", 0, sequence: 3));

        Assert.True(_tracker.Stop("board", 0));

        Assert.Same(ZoneCountInfo.None, _tracker.Count);
    }

    [Fact]
    public void Stop_ZoneThatDoesNotCount_LeavesTheCount()
    {
        var count = Counting("board", 0, sequence: 3);
        _tracker.Take(count);

        Assert.False(_tracker.Stop("board", 1));

        Assert.Same(count, _tracker.Count);
    }

    // A step the engine took just before the stop reached it is ignored until the engine ends the
    // count; its sequence is not taken, so the None that follows still comes in.
    [Fact]
    public void Take_AfterAStop_IgnoresTheStoppedZoneUntilItEnds()
    {
        _tracker.Take(Counting("board", 0, sequence: 3));
        _tracker.Stop("board", 0);

        Assert.False(_tracker.Take(Counting("board", 0, sequence: 5)));
        Assert.True(_tracker.Take(ZoneCountInfo.None with { Sequence = 4 }));
        Assert.True(_tracker.Take(Counting("board", 0, sequence: 6)));

        Assert.True(_tracker.Count.Counts("board", 0));
    }

    [Fact]
    public void Take_AfterAStop_TakesAnotherZone()
    {
        _tracker.Take(Counting("board", 0, sequence: 3));
        _tracker.Stop("board", 0);

        Assert.True(_tracker.Take(Counting("board", 1, sequence: 4)));

        Assert.True(_tracker.Count.Counts("board", 1));
    }

    [Fact]
    public void Start_MakesTheCountOwn()
    {
        _tracker.Start("board", 0);

        Assert.True(_tracker.IsOwn("board", 0));
        Assert.False(_tracker.IsOwn("board", 1));
    }

    // The engine confirmed the count of this app, and then ended it by itself, such as at a pause.
    [Fact]
    public void Take_EndOfTheConfirmedCount_ForgetsTheOwnCount()
    {
        _tracker.Start("board", 0);
        _tracker.Take(Counting("board", 0, sequence: 1));

        _tracker.Take(ZoneCountInfo.None with { Sequence = 2 });

        Assert.False(_tracker.IsOwn("board", 0));
    }

    // A None from before the engine confirmed the start does not end the count of this app.
    [Fact]
    public void Take_NoneBeforeTheConfirmation_KeepsTheOwnCount()
    {
        _tracker.Start("board", 0);

        _tracker.Take(ZoneCountInfo.None with { Sequence = 1 });

        Assert.True(_tracker.IsOwn("board", 0));
    }

    // A step of the same zone that another settings app counted before this app started does not
    // confirm the count, so the None that follows it does not end it either.
    [Fact]
    public void Take_StateFromBeforeTheStart_DoesNotConfirm()
    {
        _tracker.Take(Counting("board", 0, sequence: 4));
        _tracker.Start("board", 0);

        _tracker.Take(Counting("board", 0, sequence: 4, led: 1));
        _tracker.Take(ZoneCountInfo.None with { Sequence = 5 });

        Assert.True(_tracker.IsOwn("board", 0));
    }

    [Fact]
    public void ForgetStart_BeforeTheConfirmation_ForgetsTheOwnCount()
    {
        _tracker.Start("board", 0);

        _tracker.ForgetStart("board", 0);

        Assert.False(_tracker.IsOwn("board", 0));
    }

    // The engine confirmed a later start meanwhile.
    [Fact]
    public void ForgetStart_AfterTheConfirmation_KeepsTheOwnCount()
    {
        _tracker.Start("board", 0);
        _tracker.Take(Counting("board", 0, sequence: 1));

        _tracker.ForgetStart("board", 0);

        Assert.True(_tracker.IsOwn("board", 0));
    }

    [Fact]
    public void ForgetStart_OtherZone_KeepsTheOwnCount()
    {
        _tracker.Start("board", 0);

        _tracker.ForgetStart("board", 1);

        Assert.True(_tracker.IsOwn("board", 0));
    }

    [Fact]
    public void Resize_OwnZone_ForgetsTheOwnCount()
    {
        _tracker.Start("board", 0);

        _tracker.Resize("board", 1);
        Assert.True(_tracker.IsOwn("board", 0));

        _tracker.Resize("board", 0);
        Assert.False(_tracker.IsOwn("board", 0));
    }

    [Fact]
    public void Stop_OwnZone_ForgetsTheOwnCount()
    {
        _tracker.Start("board", 0);

        Assert.False(_tracker.Stop("board", 0));

        Assert.False(_tracker.IsOwn("board", 0));
    }

    [Fact]
    public void StopOwn_CountOfThisApp_StopsIt()
    {
        _tracker.Start("board", 0);
        _tracker.Take(Counting("board", 0, sequence: 1));

        Assert.True(_tracker.StopOwn("board", 0));

        Assert.Same(ZoneCountInfo.None, _tracker.Count);
        Assert.False(_tracker.IsOwn("board", 0));
        Assert.False(_tracker.Take(Counting("board", 0, sequence: 2)));
    }

    // A count another settings app started is left alone.
    [Fact]
    public void StopOwn_CountOfAnotherApp_LeavesIt()
    {
        var count = Counting("board", 0, sequence: 1);
        _tracker.Take(count);

        Assert.False(_tracker.StopOwn("board", 0));

        Assert.Same(count, _tracker.Count);
    }

    // The own zone that the engine does not count (yet) is not stopped, and stays own.
    [Fact]
    public void StopOwn_OwnZoneThatDoesNotCount_LeavesEverything()
    {
        _tracker.Start("board", 0);

        Assert.False(_tracker.StopOwn("board", 0));

        Assert.True(_tracker.IsOwn("board", 0));
    }

    [Fact]
    public void ForgetOwnCount_EndsTheOwnCountAndKeepsTheCount()
    {
        var count = Counting("board", 0, sequence: 1);
        _tracker.Start("board", 0);
        _tracker.Take(count);

        _tracker.ForgetOwnCount();

        Assert.False(_tracker.IsOwn("board", 0));
        Assert.Same(count, _tracker.Count);
    }

    // A new engine starts its sequence again, and a stop of the old one no longer applies.
    [Fact]
    public void Forget_StartsAgain()
    {
        _tracker.Start("board", 0);
        _tracker.Take(Counting("board", 0, sequence: 9));
        _tracker.Stop("board", 0);

        _tracker.Forget();

        Assert.Same(ZoneCountInfo.None, _tracker.Count);
        Assert.False(_tracker.IsOwn("board", 0));
        Assert.True(_tracker.Take(Counting("board", 0, sequence: 1)));
    }

    private static ZoneCountInfo Counting(string deviceId, int zoneIndex, long sequence, int led = 0) =>
        new(deviceId, zoneIndex, led, 60, ZoneCountInfo.DefaultLedsPerSecond, true) { Sequence = sequence };
}
