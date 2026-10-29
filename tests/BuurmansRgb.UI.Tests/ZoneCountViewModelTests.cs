using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class ZoneCountViewModelTests
{
    private const string Board = "board";
    private static readonly RgbZone Header = new("ARGB 1", 20, 0, 60);

    private readonly RecordingPreviewActions _actions = new();

    [Fact]
    public void Show_ShowsTheLedWithItsNameForAScreenReader()
    {
        var count = Create(new ZoneCountInfo(Board, 1, 37, 60, 2, true));

        Assert.Equal("LED 37", count.LedText);
        Assert.Equal(string.Format(Strings.ZoneCountAccessibleName, "Board – ARGB 1", 37, 60), count.AccessibleName);
        Assert.Equal(Strings.ZoneCountPause, count.PauseOrResumeText);
        Assert.Equal(string.Format(Strings.ZoneCountRunning, 2), count.Status);
        Assert.Equal([false, true, false], count.PaceOptions.Select(option => option.IsChecked));
    }

    // A screen reader reads the status as a live region: the steps of a running count leave it
    // alone, so it is not read out at every LED.
    [Fact]
    public void Show_RunningSteps_LeaveTheStatusAlone()
    {
        var count = Create(new ZoneCountInfo(Board, 1, 1, 60, 1, true));
        var changed = new List<string?>();
        count.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        count.Show(new ZoneCountInfo(Board, 1, 2, 60, 1, true));
        count.Show(new ZoneCountInfo(Board, 1, 3, 60, 1, true));

        Assert.DoesNotContain(nameof(ZoneCountViewModel.Status), changed);
        Assert.Contains(nameof(ZoneCountViewModel.LedText), changed);
    }

    [Fact]
    public void PauseOrResume_Running_PausesAtTheLedTheUserSees()
    {
        var count = Create(new ZoneCountInfo(Board, 1, 12, 60, 1, true));

        count.PauseOrResumeCommand.Execute(null);

        Assert.False(count.Running);
        Assert.Equal(Strings.ZoneCountResume, count.PauseOrResumeText);
        Assert.Equal(string.Format(Strings.ZoneCountPaused, 12, 60), count.Status);
        Assert.Equal([new ZoneCountUpdate(Board, 1, new ZoneCountChange(Led: 12, Running: false))], _actions.Requests);
    }

    [Fact]
    public void PauseOrResume_Paused_GoesOn()
    {
        var count = Create(new ZoneCountInfo(Board, 1, 12, 60, 1, false));

        count.PauseOrResumeCommand.Execute(null);

        Assert.True(count.Running);
        Assert.Equal([new ZoneCountUpdate(Board, 1, new ZoneCountChange(Running: true))], _actions.Requests);
    }

    // At the last LED there is nothing to go on with: it starts again.
    [Fact]
    public void PauseOrResume_AtTheEnd_StartsAgainFromTheFirstLed()
    {
        var count = Create(new ZoneCountInfo(Board, 1, 60, 60, 1, false));
        Assert.Equal(string.Format(Strings.ZoneCountAtEnd, 60), count.Status);

        count.PauseOrResumeCommand.Execute(null);

        Assert.Equal(1, count.Led);
        Assert.Equal([new ZoneCountUpdate(Board, 1, new ZoneCountChange(Led: 1, Running: true))], _actions.Requests);
    }

    [Theory]
    [InlineData(12, false, 11)]
    [InlineData(12, true, 13)]
    [InlineData(1, false, 1)]
    [InlineData(60, true, 60)]
    public void PreviousAndNext_GoOneLedAndPause(int led, bool next, int expected)
    {
        var count = Create(new ZoneCountInfo(Board, 1, led, 60, 1, true));

        (next ? count.NextCommand : count.PreviousCommand).Execute(null);

        Assert.Equal(expected, count.Led);
        Assert.False(count.Running);
        Assert.Equal([new ZoneCountUpdate(Board, 1, new ZoneCountChange(Led: expected, Running: false))], _actions.Requests);
    }

    [Fact]
    public void Pace_CheckingAnotherOne_SendsIt()
    {
        var count = Create(new ZoneCountInfo(Board, 1, 4, 60, 1, true));

        count.PaceOptions[2].IsChecked = true;

        Assert.Equal(5, count.LedsPerSecond);
        Assert.Equal([false, false, true], count.PaceOptions.Select(option => option.IsChecked));
        Assert.Equal([new ZoneCountUpdate(Board, 1, new ZoneCountChange(LedsPerSecond: 5))], _actions.Requests);
        Assert.Equal(string.Format(Strings.ZoneCountPace, 5), count.PaceOptions[2].AccessibleName);
        Assert.Equal(string.Format(Strings.ZoneCountPaceOne, 1), count.PaceOptions[0].AccessibleName);
        Assert.Equal(count.PaceOptions.Select(option => option.AccessibleName), count.PaceOptions.Select(option => option.ToString()));
    }

    [Fact]
    public void Pace_TheOneItHas_SendsNothing()
    {
        var count = Create(new ZoneCountInfo(Board, 1, 4, 60, 2, true));

        count.PaceOptions[1].IsChecked = true;
        count.PaceOptions[0].IsChecked = false;

        Assert.Empty(_actions.Requests);
    }

    // "Dit is de laatste": the zone gets as many LEDs as the counter says.
    [Fact]
    public void Keep_ResizesTheZoneToTheLed()
    {
        var count = Create(new ZoneCountInfo(Board, 1, 37, 60, 1, false));

        count.KeepCommand.Execute(null);

        Assert.Equal([new ZoneResize(Board, 1, 37)], _actions.Requests);
    }

    [Fact]
    public void Keep_ALengthTheZoneCannotHold_CannotBeChosen()
    {
        var count = new ZoneCountViewModel(new ZoneCountInfo(Board, 1, 3, 60, 1, false), new RgbZone("Strip", 30, 5, 60), "Board – Strip", _actions);

        Assert.False(count.KeepCommand.CanExecute(null));

        count.Show(new ZoneCountInfo(Board, 1, 5, 60, 1, false));
        Assert.True(count.KeepCommand.CanExecute(null));
    }

    [Fact]
    public void Stop_AsksToStop()
    {
        var count = Create(new ZoneCountInfo(Board, 1, 37, 60, 1, true));

        count.StopCommand.Execute(null);

        Assert.Equal([new ZoneCountStop(Board, 1)], _actions.Requests);
    }

    private ZoneCountViewModel Create(ZoneCountInfo info) => new(info, Header, "Board – ARGB 1", _actions);
}
