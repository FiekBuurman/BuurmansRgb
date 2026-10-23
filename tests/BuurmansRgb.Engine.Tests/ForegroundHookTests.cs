using System.ComponentModel;
using System.Windows.Forms;

namespace BuurmansRgb.Engine.Tests;

// With the real SetWinEventHook on the thread of the test; Tell gives a window as Windows would.
public sealed class ForegroundHookTests : IDisposable
{
    private readonly ForegroundHook _hook = new();
    private readonly List<string> _calls = [];

    public void Dispose() => _hook.Dispose();

    // One hook however many listen; it goes after the last, and a listener that asks twice counts once.
    [Fact]
    public void Listen_SetsTheHookWhileAnybodyListens()
    {
        Action<nint> first = _ => { };
        Action<nint> second = _ => { };
        Assert.False(_hook.IsSet);

        Assert.True(_hook.Listen(first));
        Assert.True(_hook.Listen(first));
        Assert.True(_hook.Listen(second));
        Assert.True(_hook.IsSet);

        _hook.Stop(first);
        Assert.True(_hook.IsSet);
        _hook.Stop(second);
        Assert.False(_hook.IsSet);

        _hook.Stop(second);
        Assert.False(_hook.IsSet);
    }

    // A listener that Windows refused the hook to stays on, and gets the windows once a later Listen
    // gets the hook; it goes with the last listener as always.
    [Fact]
    public void Listen_WhenWindowsRefusesTheHook_TheListenerGetsTheWindowsOfALaterHook()
    {
        using var trace = new TraceCapture();
        var takes = new Queue<nint>([0, 42]);
        var unhooked = new List<nint>();
        using var hook = new ForegroundHook(_ => takes.Dequeue(), handle =>
        {
            unhooked.Add(handle);
            return true;
        });
        Action<nint> first = window => _calls.Add($"first {window}");
        Action<nint> second = window => _calls.Add($"second {window}");

        Assert.False(hook.Listen(first));
        Assert.False(hook.IsSet);
        Assert.Contains(trace.Messages, message => message.Contains("SetWinEventHook failed", StringComparison.Ordinal));
        Assert.True(hook.Listen(second));
        Assert.True(hook.IsSet);
        hook.Tell(7);
        hook.Stop(first);
        hook.Stop(second);

        Assert.Equal(["first 7", "second 7"], _calls);
        Assert.False(hook.IsSet);
        Assert.Equal([42], unhooked);
    }

    [Fact]
    public void Tell_GivesTheWindowToEachListener_UntilItStops()
    {
        Action<nint> first = window => _calls.Add($"first {window}");
        Action<nint> second = window => _calls.Add($"second {window}");
        _hook.Listen(first);
        _hook.Listen(second);

        _hook.Tell(7);
        _hook.Stop(first);
        _hook.Tell(8);

        Assert.Equal(["first 7", "second 7", "second 8"], _calls);
    }

    // Thrown into Windows or the message loop, it would end the engine.
    [Fact]
    public void Tell_AListenerThatThrows_DoesNotKeepTheWindowFromTheOthers()
    {
        using var trace = new TraceCapture();
        _hook.Listen(_ => throw new InvalidOperationException("The foreground listener failed."));
        _hook.Listen(window => _calls.Add($"other {window}"));

        _hook.Tell(7);

        Assert.Equal(["other 7"], _calls);
        Assert.Contains(trace.Messages, message => message.Contains("The foreground listener failed.", StringComparison.Ordinal));
    }

    // A listener that stops while it gets a window: the others get that window still.
    [Fact]
    public void Tell_AListenerThatStopsMeanwhile_TheOthersStillGetTheWindow()
    {
        Action<nint>? first = null;
        first = window =>
        {
            _calls.Add($"first {window}");
            _hook.Stop(first!);
        };
        _hook.Listen(first);
        _hook.Listen(window => _calls.Add($"second {window}"));

        _hook.Tell(7);
        _hook.Tell(8);

        Assert.Equal(["first 7", "second 7", "second 8"], _calls);
    }

    [Fact]
    public void Dispose_TakesTheHookAway_AndTellsNobody()
    {
        _hook.Listen(window => _calls.Add($"first {window}"));

        _hook.Dispose();
        _hook.Tell(7);

        Assert.False(_hook.IsSet);
        Assert.Empty(_calls);
    }

    [Fact]
    public void ProcessOf_TheOwnProcess_AndZeroForNoWindow()
    {
        var window = new NativeWindow();
        try
        {
            window.CreateHandle(new CreateParams());
        }
        catch (Win32Exception)
        {
            Assert.Skip("This session cannot make a window.");
        }

        try
        {
            Assert.Equal((uint)Environment.ProcessId, ForegroundHook.ProcessOf(window.Handle));
            Assert.Equal(0u, ForegroundHook.ProcessOf(0));
        }
        finally
        {
            window.DestroyHandle();
        }
    }
}
