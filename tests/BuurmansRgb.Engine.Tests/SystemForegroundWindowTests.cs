using System.Runtime.InteropServices;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Programs;

namespace BuurmansRgb.Engine.Tests;

public sealed class SystemForegroundWindowTests : IDisposable
{
    private const string NoWindow = "This session cannot make a window, or has no shell.";

    private readonly ForegroundHook _hook = new();

    public void Dispose() => _hook.Dispose();

    // The taskbar, Alt+Tab, the lock screen, the Start menu and a screen saver pass by; the desktop
    // and any program take the user away from a game.
    [Theory]
    [InlineData("Shell_TrayWnd", "explorer.exe", false)]
    [InlineData("Shell_SecondaryTrayWnd", "explorer.exe", false)]
    [InlineData("TopLevelWindowForOverflowXamlIsland", "explorer.exe", false)]
    [InlineData("NotifyIconOverflowWindow", "explorer.exe", false)]
    [InlineData("XamlExplorerHostIslandWindow", "explorer.exe", false)]
    [InlineData("MultitaskingViewFrame", "explorer.exe", false)]
    [InlineData("ForegroundStaging", "explorer.exe", false)]
    [InlineData("Windows.UI.Core.CoreWindow", "LockApp.exe", false)]
    [InlineData("Windows.UI.Core.CoreWindow", "StartMenuExperienceHost.exe", false)]
    [InlineData("Windows.UI.Core.CoreWindow", "searchhost.exe", false)]
    [InlineData("WindowsScreenSaverClass", "Mystify.scr", false)]
    [InlineData("", "PhotoScreensaver.scr", false)]
    [InlineData("Progman", "explorer.exe", true)]
    [InlineData("CabinetWClass", "explorer.exe", true)]
    [InlineData("UnityWndClass", "eldenring.exe", true)]
    [InlineData("HwndWrapper[BuurmansRgb.UI;;]", "BuurmansRgb.UI.exe", true)]
    [InlineData("", "eldenring.exe", true)]
    [InlineData("UnityWndClass", null, false)]
    public void Counts_OnlyAWindowThatTakesTheUserToAProgram(string className, string? program, bool counts) =>
        Assert.Equal(counts, SystemForegroundWindow.Counts(className, program));

    // Each part of Windows that shows over a program passes by, also one added to the list later.
    [Fact]
    public void Counts_NoneOfTheShellParts()
    {
        Assert.NotEmpty(ProgramNames.ShellParts);
        Assert.All(ProgramNames.ShellParts, part => Assert.False(SystemForegroundWindow.Counts("Windows.UI.Core.CoreWindow", part), part));
    }

    // The desktop is a window of explorer.exe; a window of the engine itself changes nothing. The
    // same program again is told again too, as it may have stopped and started in between.
    [Fact]
    public void Tell_GivesTheProgramOfEachWindow_AndSkipsTheOwnProcess()
    {
        SystemForegroundWindow? foreground = null;
        try
        {
            foreground = CreateOrSkip();
            var desktop = GetShellWindow();
            if (desktop == 0)
            {
                Assert.Skip(NoWindow);
            }

            var changes = new List<string?>();
            foreground.Changed += (_, program) => changes.Add(program);

            foreground.Tell(foreground.Handle);
            Assert.Empty(changes);

            foreground.Tell(desktop);
            foreground.Tell(foreground.Handle);
            foreground.Tell(0);
            foreground.Tell(desktop);
            Assert.Equal(["explorer.exe", "explorer.exe"], changes, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            foreground?.Dispose();
        }
    }

    // Thrown into Windows or the message loop, it would end the engine; the next window is told too.
    [Fact]
    public void Tell_AListenerThatThrows_DoesNotThrowOut()
    {
        SystemForegroundWindow? foreground = null;
        try
        {
            foreground = CreateOrSkip();
            var desktop = GetShellWindow();
            if (desktop == 0)
            {
                Assert.Skip(NoWindow);
            }

            var calls = 0;
            foreground.Changed += (_, _) =>
            {
                calls++;
                throw new InvalidOperationException("listener");
            };

            foreground.Tell(desktop);
            foreground.Tell(desktop);

            Assert.Equal(2, calls);
        }
        finally
        {
            foreground?.Dispose();
        }
    }

    // Follow posts to the window; sent here on the own thread, the message goes straight through
    // WndProc. Only the last ask counts, and without another listener the hook goes with the last.
    [Fact]
    public void Follow_ListensToTheHookOnlyWhileFollowed()
    {
        SystemForegroundWindow? foreground = null;
        try
        {
            foreground = CreateOrSkip();
            Assert.False(foreground.IsListening);
            Assert.False(_hook.IsSet);

            foreground.Follow(true);
            SendFollow(foreground);
            Assert.True(foreground.IsListening);
            Assert.True(_hook.IsSet);
            SendFollow(foreground);
            Assert.True(foreground.IsListening);

            foreground.Follow(true);
            foreground.Follow(false);
            SendFollow(foreground);
            Assert.False(foreground.IsListening);
            Assert.False(_hook.IsSet);

            foreground.Follow(true);
            SendFollow(foreground);
            foreground.Dispose();
            Assert.False(foreground.IsListening);
            Assert.False(_hook.IsSet);
        }
        finally
        {
            foreground?.Dispose();
        }
    }

    // Refused, it does not stay on the hook while not followed, and the next Follow(true) asks again.
    [Fact]
    public void Follow_WhenWindowsRefusesTheHook_AsksAgainNextTime()
    {
        var takes = new Queue<nint>([0, 42]);
        using var hook = new ForegroundHook(_ => takes.Dequeue(), _ => true);
        SystemForegroundWindow? foreground = null;
        try
        {
            foreground = MessageOnlyWindows.CreateOrSkip(() => new SystemForegroundWindow(hook), NoWindow);
            var desktop = GetShellWindow();
            if (desktop == 0)
            {
                Assert.Skip(NoWindow);
            }

            var changes = new List<string?>();
            foreground.Changed += (_, program) => changes.Add(program);

            foreground.Follow(true);
            SendFollow(foreground);
            Assert.False(foreground.IsListening);
            Action<nint> other = _ => { };
            Assert.True(hook.Listen(other));
            hook.Tell(desktop);
            Assert.Empty(changes);

            foreground.Follow(true);
            SendFollow(foreground);
            Assert.True(foreground.IsListening);
            Assert.True(hook.IsSet);
        }
        finally
        {
            foreground?.Dispose();
        }
    }

    // The hook tells each window it gets to a follower, as Windows would.
    [Fact]
    public void Followed_GetsTheWindowsOfTheHook()
    {
        SystemForegroundWindow? foreground = null;
        try
        {
            foreground = CreateOrSkip();
            var desktop = GetShellWindow();
            if (desktop == 0)
            {
                Assert.Skip(NoWindow);
            }

            var changes = new List<string?>();
            foreground.Follow(true);
            SendFollow(foreground);
            foreground.Changed += (_, program) => changes.Add(program);

            _hook.Tell(desktop);
            foreground.Follow(false);
            SendFollow(foreground);
            _hook.Tell(desktop);

            Assert.Equal(["explorer.exe"], changes, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            foreground?.Dispose();
        }
    }

    private static void SendFollow(SystemForegroundWindow foreground) =>
        MessageOnlyWindows.SendMessage(foreground.Handle, SystemForegroundWindow.WmFollow, 0, 0);

    private SystemForegroundWindow CreateOrSkip() => MessageOnlyWindows.CreateOrSkip(() => new SystemForegroundWindow(_hook), NoWindow);

    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();
}
