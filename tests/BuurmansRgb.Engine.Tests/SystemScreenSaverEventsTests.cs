using System.Runtime.InteropServices;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Power;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Win32;

namespace BuurmansRgb.Engine.Tests;

// With the real hook on the thread of the test. No test starts a screen saver: whether one runs is
// told through Show, as a window of a running .scr would, or through made-up windows with what
// Windows says made up too (CreateFake); a real window (the desktop) is no screen saver. Whether
// Windows says one runs is only read, never set.
public sealed class SystemScreenSaverEventsTests : IDisposable
{
    private const string NoShell = "This session has no shell.";
    private const string ScreenSaverProgram = "Ribbons.scr";
    private const nint ScreenSaverWindow = 1;
    private const nint ProgramWindow = 2;
    private const nint LockScreenWindow = 3;

    private static readonly Dictionary<nint, string> ProgramsOfWindows = new()
    {
        [ScreenSaverWindow] = ScreenSaverProgram,
        [ProgramWindow] = "notepad.exe",
        [LockScreenWindow] = "LockApp.exe",
    };

    private readonly ForegroundHook _hook = new();
    private readonly FakeTimeProvider _time = new();
    private readonly List<bool> _changes = [];
    private bool _windowsSaysRunning;
    private nint _foreground;
    private int _asks;

    public void Dispose() => _hook.Dispose();

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

    // Unlocking ends the pause of a screen saver that went without another window coming to the
    // front; the next screen saver window must pause again, though it ran for this all along. And
    // the window the user was in ends it.
    [Fact]
    public void InAutoPause_PausesForEachScreenSaverWindow_AndTheNextWindowEndsIt()
    {
        var desktop = GetShellWindow();
        if (desktop == 0)
        {
            Assert.Skip(NoShell);
        }

        using var screenSaver = Create();
        var session = new FakeSessionEvents();
        var pause = PauseReasons.None;
        using var autoPause = new AutoPause(
            new FakePowerEvents(),
            session,
            new FakeDisplayEvents(),
            screenSaver,
            (reason, paused) => pause = paused ? pause | reason : pause & ~reason);

        screenSaver.Show(true);
        session.Raise(SessionSwitchReason.SessionUnlock);
        Assert.Equal(PauseReasons.None, pause);

        screenSaver.Show(true);
        Assert.Equal(PauseReasons.ScreenSaver, pause);

        _hook.Tell(desktop);
        Assert.Equal(PauseReasons.None, pause);
    }

    // "Voorbeeld" in the screen saver settings: the .scr comes to the front, but Windows did not
    // start it. Written to the log, and asked once more, still without counting.
    [Fact]
    public void Tell_AScreenSaverWindowsDoesNotConfirm_DoesNotCount()
    {
        using var trace = new TraceCapture();
        using var screenSaver = CreateFake();

        _foreground = ScreenSaverWindow;
        screenSaver.Tell(ScreenSaverWindow);
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Empty(_changes);
        Assert.False(screenSaver.IsRunning);
        Assert.Equal(2, _asks);
        Assert.Contains(trace.Messages, message => message.Contains(ScreenSaverProgram, StringComparison.Ordinal));
    }

    // Windows says it started the screen saver; the next window ends it without asking again, as the
    // flag may still be on while the screen saver closes.
    [Fact]
    public void Tell_AScreenSaverWindowsConfirms_Counts_UntilAnotherWindow()
    {
        using var screenSaver = CreateFake();
        _windowsSaysRunning = true;

        screenSaver.Tell(ScreenSaverWindow);
        Assert.True(screenSaver.IsRunning);
        screenSaver.Tell(ProgramWindow);
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal([true, false], _changes);
        Assert.Equal(1, _asks);
    }

    // Should Windows set the flag a little after the window came to the front, a second later the
    // screen saver counts still; asked only once, not polled.
    [Fact]
    public void Tell_WindowsConfirmsASecondLater_Counts()
    {
        using var screenSaver = CreateFake();
        _foreground = ScreenSaverWindow;

        screenSaver.Tell(ScreenSaverWindow);
        _windowsSaysRunning = true;
        _time.Advance(SystemScreenSaverEvents.AskAgainAfter - TimeSpan.FromMilliseconds(1));
        Assert.Empty(_changes);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal([true], _changes);
        Assert.True(screenSaver.IsRunning);

        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal([true], _changes);
        Assert.Equal(2, _asks);
    }

    // The second ask is only about the .scr window that came to the front: not when another window
    // came after it, or the .scr is no longer in front.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tell_AnotherWindowBeforeTheSecondAsk_DoesNotCount(bool told)
    {
        using var screenSaver = CreateFake();
        _foreground = ScreenSaverWindow;
        screenSaver.Tell(ScreenSaverWindow);

        _foreground = ProgramWindow;
        if (told)
        {
            screenSaver.Tell(ProgramWindow);
        }

        _windowsSaysRunning = true;
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Empty(_changes);
        Assert.False(screenSaver.IsRunning);
    }

    // With "aanmeldingsscherm bij hervatten" the lock screen comes to the front before Windows
    // reports the lock: the pause goes on without a gap, so dimmed or black lights do not go bright
    // in between, and unlocking ends it. Also when the lock screen comes without the lock.
    [Fact]
    public void Tell_TheLockScreen_DoesNotEndTheScreenSaver()
    {
        using var screenSaver = CreateFake();
        var session = new FakeSessionEvents();
        var pauses = new List<PauseReasons>();
        var pause = PauseReasons.None;
        using var autoPause = new AutoPause(
            new FakePowerEvents(),
            session,
            new FakeDisplayEvents(),
            screenSaver,
            (reason, paused) =>
            {
                pause = paused ? pause | reason : pause & ~reason;
                pauses.Add(pause);
            });
        _windowsSaysRunning = true;

        screenSaver.Tell(ScreenSaverWindow);
        pauses.Clear();
        screenSaver.Tell(LockScreenWindow);
        Assert.True(screenSaver.IsRunning);
        session.Raise(SessionSwitchReason.SessionLock);
        Assert.Equal(PauseReasons.Locked | PauseReasons.ScreenSaver, pause);
        session.Raise(SessionSwitchReason.SessionUnlock);

        Assert.Equal(PauseReasons.None, pause);
        Assert.DoesNotContain(PauseReasons.None, pauses.SkipLast(1));
        Assert.Equal([true], _changes);

        screenSaver.Tell(ScreenSaverWindow);
        screenSaver.Tell(LockScreenWindow);
        screenSaver.Tell(ProgramWindow);
        Assert.Equal([true, true, false], _changes);
    }

    [Fact]
    public void Dispose_BeforeTheSecondAsk_AsksNoMore()
    {
        var screenSaver = CreateFake();
        _foreground = ScreenSaverWindow;
        screenSaver.Tell(ScreenSaverWindow);

        screenSaver.Dispose();
        _windowsSaysRunning = true;
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Empty(_changes);
        Assert.Equal(1, _asks);
    }

    // An engine that starts while the screen saver runs pauses at once (AutoPause reads IsRunning).
    [Fact]
    public void Created_WhileAScreenSaverRuns_IsRunning()
    {
        _foreground = ScreenSaverWindow;
        _windowsSaysRunning = true;

        using var screenSaver = CreateFake();

        Assert.True(screenSaver.IsRunning);
        Assert.Empty(_changes);
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

    // With made-up windows and their exe names, what Windows says in _windowsSaysRunning, the
    // window in front in _foreground and a fake clock for the second ask.
    private SystemScreenSaverEvents CreateFake()
    {
        var screenSaver = new SystemScreenSaverEvents(
            _hook,
            window => ProgramsOfWindows.GetValueOrDefault(window),
            () =>
            {
                _asks++;
                return _windowsSaysRunning;
            },
            () => _foreground,
            _time);
        screenSaver.Switched += (_, running) => _changes.Add(running);
        return screenSaver;
    }

    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();
}
