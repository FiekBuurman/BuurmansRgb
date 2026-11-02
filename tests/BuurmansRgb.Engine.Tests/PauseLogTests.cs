using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class PauseLogTests
{
    [Theory]
    [InlineData(PauseReasons.Manual, "tray menu")]
    [InlineData(PauseReasons.Locked, "locked")]
    [InlineData(PauseReasons.Asleep, "asleep")]
    [InlineData(PauseReasons.DisplayOff, "screen off")]
    [InlineData(PauseReasons.ScreenSaver, "screen saver")]
    [InlineData(PauseReasons.Locked | PauseReasons.DisplayOff, "locked, screen off")]
    [InlineData(PauseReasons.ScreenSaver | PauseReasons.Manual, "tray menu, screen saver")]
    [InlineData(PauseReasons.None, "none")]
    public void Describe_NamesEachReason(PauseReasons reasons, string words) =>
        Assert.Equal(words, PauseLog.Describe(reasons));

    [Fact]
    public void Line_FirstReason_SaysTheEffectsPaused() =>
        Assert.Equal("Effects paused (screen off).", PauseLog.Line(PauseReasons.DisplayOff, started: true, PauseReasons.DisplayOff));

    [Fact]
    public void Line_AnotherReason_NamesTheOnesThatHoldToo() =>
        Assert.Equal(
            "Effects paused (locked); also paused: screen off.",
            PauseLog.Line(PauseReasons.Locked, started: true, PauseReasons.Locked | PauseReasons.DisplayOff));

    [Fact]
    public void Line_LastReasonEnds_SaysTheEffectsRunAgain() =>
        Assert.Equal("Pause ended (tray menu); effects run again.", PauseLog.Line(PauseReasons.Manual, started: false, PauseReasons.None));

    [Fact]
    public void Line_AReasonEndsWhileOthersHold_NamesThem() =>
        Assert.Equal(
            "Pause ended (screen saver); still paused: locked, asleep.",
            PauseLog.Line(PauseReasons.ScreenSaver, started: false, PauseReasons.Locked | PauseReasons.Asleep));
}
