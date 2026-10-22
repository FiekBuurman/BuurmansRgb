using System.Runtime.InteropServices;
using BuurmansRgb.Engine.Power;
using BuurmansRgb.Engine.Programs;

namespace BuurmansRgb.Engine.Tests;

// With the real hook on the thread of the test. No test starts a screen saver: whether one runs is
// told through Show, as a window of a running .scr would; a real window (the desktop) is no screen
// saver. Whether Windows says one runs is only read, never set.
public sealed class SystemScreenSaverEventsTests : IDisposable
{
    private const string NoShell = "This session has no shell.";

    private readonly ForegroundHook _hook = new();
    private readonly List<bool> _changes = [];

    public void Dispose() => _hook.Dispose();

    [Theory]
    [InlineData("Mystify.scr", true)]
    [InlineData("scrnsave.scr", true)]
    [InlineData("PhotoScreensaver.SCR", true)]
    [InlineData("Ribbons.Scr", true)]
    [InlineData(".scr", false)]
    [InlineData("scr.exe", false)]
    [InlineData("screensaver.exe", false)]
    [InlineData("explorer.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsScreenSaver_AnExeNameThatEndsInScr(string? program, bool screenSaver) =>
        Assert.Equal(screenSaver, SystemScreenSaverEvents.IsScreenSaver(program));

    // Each screen saver window is told, also a second one (another screen, or after AutoPause ended
    // the pause); the end only once.
    [Fact]
    public void Show_TellsEachScreenSaverWindow_AndTheEndOnce()
    {
        using var screenSaver = Create();

        screenSaver.Show(false);
        Assert.Empty(_changes);
        screenSaver.Show(true);
        Assert.True(screenSaver.IsRunning);
        screenSaver.Show(true);
        screenSaver.Show(false);
        Assert.False(screenSaver.IsRunning);
        screenSaver.Show(false);
        screenSaver.Show(true);

        Assert.Equal([true, true, false, true], _changes);
    }

    // The window the user was in comes back to the front when the screen saver goes away.
    [Fact]
    public void Tell_AnotherWindowAfterTheScreenSaver_EndsIt()
    {
        using var screenSaver = Create();
        var desktop = GetShellWindow();
        if (desktop == 0)
        {
            Assert.Skip(NoShell);
        }

        screenSaver.Tell(desktop);
        Assert.Empty(_changes);

        screenSaver.Show(true);
        screenSaver.Tell(desktop);
        screenSaver.Tell(desktop);

        Assert.Equal([true, false], _changes);
        Assert.False(screenSaver.IsRunning);
    }

    // A window that is gone, or none at all, is no screen saver either.
    [Fact]
    public void Tell_NoWindow_EndsIt()
    {
        using var screenSaver = Create();
        screenSaver.Show(true);

        screenSaver.Tell(0);

        Assert.Equal([true, false], _changes);
    }

    // Through the hook, as Windows tells it.
    [Fact]
    public void Created_ListensToTheHookUntilDisposed()
    {
        var desktop = GetShellWindow();
        if (desktop == 0)
        {
            Assert.Skip(NoShell);
        }

        var screenSaver = Create();
        Assert.True(_hook.IsSet);
        screenSaver.Show(true);
        _hook.Tell(desktop);
        Assert.Equal([true, false], _changes);

        screenSaver.Dispose();
        screenSaver.Show(true);
        _hook.Tell(desktop);

        Assert.False(_hook.IsSet);
        Assert.Equal([true, false, true], _changes);
    }

    [Fact]
    public void CreateAndDispose_WithoutWarnings()
    {
        using var trace = new TraceCapture();

        Create().Dispose();

        Assert.DoesNotContain(trace.Messages, message => message.Contains("screen saver", StringComparison.Ordinal) || message.Contains("foreground", StringComparison.Ordinal));
    }

    // Not running, also should the screen saver of this pc run while the tests do.
    private SystemScreenSaverEvents Create()
    {
        var screenSaver = new SystemScreenSaverEvents(_hook);
        screenSaver.Show(false);
        screenSaver.Switched += (_, running) => _changes.Add(running);
        return screenSaver;
    }

    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();
}
