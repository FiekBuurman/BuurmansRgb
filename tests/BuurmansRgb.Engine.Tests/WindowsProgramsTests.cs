using BuurmansRgb.Engine.Programs;

namespace BuurmansRgb.Engine.Tests;

public sealed class WindowsProgramsTests
{
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
        Assert.Equal(screenSaver, WindowsPrograms.IsScreenSaver(program));

    [Theory]
    [InlineData("LockApp.exe", true)]
    [InlineData("lockapp.EXE", true)]
    [InlineData("LockApp.scr", false)]
    [InlineData("LogonUI.exe", false)]
    [InlineData(null, false)]
    public void IsLockScreen_LockAppInAnyCase(string? program, bool lockScreen) =>
        Assert.Equal(lockScreen, WindowsPrograms.IsLockScreen(program));
}
