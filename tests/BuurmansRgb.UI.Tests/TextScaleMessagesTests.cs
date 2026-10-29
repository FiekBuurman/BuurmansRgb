using System.Windows.Threading;
using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

// The signals of Windows about its text size, handed over by the test itself, with a value the test
// sets: the registry is never read or changed.
public sealed class TextScaleMessagesTests
{
    // The app starts with the text size Windows has.
    [Fact]
    public void Factor_IsTheValueAtTheStart() => Sta.Run(() =>
    {
        var (messages, _, _) = Listen(150);

        Assert.Equal(1.5, messages.Factor);
    });

    // RegNotifyChangeKeyValue signals on a thread of the pool; Changed comes once, on the dispatcher,
    // with the new factor.
    [Fact]
    public void SignalFromThePool_GivesOneChangedOnTheDispatcher() => Sta.Run(() =>
    {
        var (messages, value, changes) = Listen(100);

        value.Percent = 200;
        Task.Run(messages.OnSignaled).Wait();
        Assert.Empty(changes);
        OffscreenWindow.WaitUntilIdle();

        Assert.Equal([Environment.CurrentManagedThreadId], changes);
        Assert.Equal(2.0, messages.Factor);
    });

    // Windows can set the value more than once for one change, or set another value under the key.
    [Fact]
    public void SameFactor_GivesNoChanged() => Sta.Run(() =>
    {
        var (messages, value, changes) = Listen(150);

        messages.OnSignaled();
        OffscreenWindow.WaitUntilIdle();
        value.Percent = 200;
        messages.OnSignaled();
        messages.OnSignaled();
        OffscreenWindow.WaitUntilIdle();

        Assert.Single(changes);
        Assert.Equal(2.0, messages.Factor);
    });

    // Back to 100 %: Windows removes the value.
    [Fact]
    public void ValueRemoved_IsBackToOne() => Sta.Run(() =>
    {
        var (messages, value, changes) = Listen(150);

        value.Percent = null;
        messages.OnSignaled();
        OffscreenWindow.WaitUntilIdle();

        Assert.Single(changes);
        Assert.Equal(1.0, messages.Factor);
    });

    // Also a signal that was already on its way to the dispatcher gives nothing after Dispose.
    [Fact]
    public void Dispose_StopsTheSignals() => Sta.Run(() =>
    {
        var (messages, value, changes) = Listen(100);

        value.Percent = 200;
        messages.OnSignaled();
        messages.Dispose();
        messages.OnSignaled();
        OffscreenWindow.WaitUntilIdle();

        Assert.Empty(changes);
        Assert.Equal(1.0, messages.Factor);
    });

    // The value Windows stores, a percentage from 100 to 225; anything else is 100 %.
    [Theory]
    [InlineData(null, 1.0)]
    [InlineData(100, 1.0)]
    [InlineData(150, 1.5)]
    [InlineData(225, 2.25)]
    [InlineData(300, 2.25)]
    [InlineData(50, 1.0)]
    [InlineData("150", 1.0)]
    public void FactorOf_ReadsThePercentageOfWindows(object? value, double factor) =>
        Assert.Equal(factor, TextScaleMessages.FactorOf(value));

    // The messages for the dispatcher of this thread with a value of percent, and per Changed the
    // thread it came on.
    private static (TextScaleMessages Messages, FakeValue Value, List<int> Changes) Listen(int? percent)
    {
        var value = new FakeValue { Percent = percent };
        var messages = new TextScaleMessages(Dispatcher.CurrentDispatcher, () => value.Percent);
        var changes = new List<int>();
        messages.Changed += (_, _) => changes.Add(Environment.CurrentManagedThreadId);
        return (messages, value, changes);
    }

    // TextScaleFactor as the registry would give it: an int, or null without one.
    private sealed class FakeValue
    {
        public int? Percent { get; set; }
    }
}
