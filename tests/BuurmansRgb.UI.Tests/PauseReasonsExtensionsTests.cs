using BuurmansRgb.Contracts;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The reason to tell when the effects are paused for more than one: the pause message and the
// line above the preview both name this one.
public sealed class PauseReasonsExtensionsTests
{
    [Theory]
    [InlineData(PauseReasons.Manual, PauseReasons.Manual)]
    [InlineData(PauseReasons.Locked, PauseReasons.Locked)]
    [InlineData(PauseReasons.Asleep, PauseReasons.Asleep)]
    [InlineData(PauseReasons.ScreenSaver, PauseReasons.ScreenSaver)]
    [InlineData(PauseReasons.DisplayOff, PauseReasons.DisplayOff)]
    public void OneReason_IsTheOne(PauseReasons reasons, PauseReasons mostTelling) =>
        Assert.Equal(mostTelling, reasons.MostTelling());

    [Fact]
    public void NoReason_IsNone() => Assert.Equal(PauseReasons.None, PauseReasons.None.MostTelling());

    // The pause the user can end comes first; waking up goes to the lock screen, and the screen
    // goes on before the pc is unlocked or awake; behind a screen that is off the screen saver may
    // still run.
    [Theory]
    [InlineData(PauseReasons.Manual | PauseReasons.Locked | PauseReasons.Asleep, PauseReasons.Manual)]
    [InlineData(PauseReasons.Manual | PauseReasons.DisplayOff, PauseReasons.Manual)]
    [InlineData(PauseReasons.Manual | PauseReasons.ScreenSaver, PauseReasons.Manual)]
    [InlineData(PauseReasons.Locked | PauseReasons.Asleep, PauseReasons.Locked)]
    [InlineData(PauseReasons.Locked | PauseReasons.Asleep | PauseReasons.DisplayOff, PauseReasons.Locked)]
    [InlineData(PauseReasons.Locked | PauseReasons.ScreenSaver, PauseReasons.Locked)]
    [InlineData(PauseReasons.Asleep | PauseReasons.DisplayOff, PauseReasons.Asleep)]
    [InlineData(PauseReasons.Asleep | PauseReasons.ScreenSaver, PauseReasons.Asleep)]
    [InlineData(PauseReasons.ScreenSaver | PauseReasons.DisplayOff, PauseReasons.ScreenSaver)]
    public void MoreReasons_TheOneThatMattersMost(PauseReasons reasons, PauseReasons mostTelling) =>
        Assert.Equal(mostTelling, reasons.MostTelling());
}
