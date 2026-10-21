using System.ComponentModel;
using System.Runtime.InteropServices;
using BuurmansRgb.Engine.Programs;

namespace BuurmansRgb.Engine.Tests;

public sealed class SystemForegroundWindowTests
{
    private const string NoWindow = "This session cannot make a window, or has no shell.";

    // The taskbar, Alt+Tab, the lock screen and the Start menu pass by; the desktop and any program
    // take the user away from a game.
    [Theory]
    [InlineData("Shell_TrayWnd", "explorer.exe", false)]
    [InlineData("Shell_SecondaryTrayWnd", "explorer.exe", false)]
    [InlineData("XamlExplorerHostIslandWindow", "explorer.exe", false)]
    [InlineData("MultitaskingViewFrame", "explorer.exe", false)]
    [InlineData("ForegroundStaging", "explorer.exe", false)]
    [InlineData("Windows.UI.Core.CoreWindow", "LockApp.exe", false)]
    [InlineData("Windows.UI.Core.CoreWindow", "StartMenuExperienceHost.exe", false)]
    [InlineData("Windows.UI.Core.CoreWindow", "searchhost.exe", false)]
    [InlineData("Progman", "explorer.exe", true)]
    [InlineData("CabinetWClass", "explorer.exe", true)]
    [InlineData("UnityWndClass", "eldenring.exe", true)]
    [InlineData("HwndWrapper[BuurmansRgb.UI;;]", "BuurmansRgb.UI.exe", true)]
    [InlineData("", "eldenring.exe", true)]
    [InlineData("UnityWndClass", null, false)]
    public void Counts_OnlyAWindowThatTakesTheUserToAProgram(string className, string? program, bool counts) =>
        Assert.Equal(counts, SystemForegroundWindow.Counts(className, program));

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

    // Follow posts to the window; sent here on the own thread, the message goes straight through
    // WndProc. Only the last ask counts.
    [Fact]
    public void Follow_SetsTheHookOnlyWhileFollowed()
    {
        SystemForegroundWindow? foreground = null;
        try
        {
            foreground = CreateOrSkip();
            Assert.False(foreground.IsHooked);

            foreground.Follow(true);
            SendFollow(foreground);
            Assert.True(foreground.IsHooked);
            SendFollow(foreground);
            Assert.True(foreground.IsHooked);

            foreground.Follow(true);
            foreground.Follow(false);
            SendFollow(foreground);
            Assert.False(foreground.IsHooked);

            foreground.Follow(true);
            SendFollow(foreground);
            foreground.Dispose();
            Assert.False(foreground.IsHooked);
        }
        finally
        {
            foreground?.Dispose();
        }
    }

    private static void SendFollow(SystemForegroundWindow foreground) =>
        SendMessage(foreground.Handle, SystemForegroundWindow.WmFollow, 0, 0);

    private static SystemForegroundWindow CreateOrSkip()
    {
        try
        {
            return new SystemForegroundWindow();
        }
        catch (Win32Exception)
        {
            Assert.Skip(NoWindow);
            throw;
        }
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();
}
