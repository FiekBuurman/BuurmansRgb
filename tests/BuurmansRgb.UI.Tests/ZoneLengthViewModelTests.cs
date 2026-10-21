using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.UI.Tests;

public sealed class ZoneLengthViewModelTests
{
    private static readonly RgbZone Header = new("ARGB 2", 20, 0, 60);

    private readonly FakeTimeProvider _time = new();
    private readonly RecordingPreviewActions _actions = new();

    [Fact]
    public void Increase_ShowsTheLengthAtOnceAndSendsItAfterThePause()
    {
        var length = Create();

        length.IncreaseCommand.Execute(null);

        Assert.Equal(21, length.LedCount);
        Assert.Equal("21", length.LedCountText);
        Assert.True(length.HasUnsentChange);
        Assert.Empty(_actions.Requests);

        _time.Advance(ZoneLengthViewModel.SendDelay);

        Assert.Equal([new ZoneResize("argb", 1, 21)], _actions.Requests);
        Assert.False(length.HasUnsentChange);
    }

    // Reloading the devices in OpenRGB takes a while, so clicking on sends one resize.
    [Fact]
    public void Increase_SeveralTimesInARow_SendsOnlyTheLastLength()
    {
        var length = Create();

        for (var click = 0; click < 5; click++)
        {
            length.IncreaseCommand.Execute(null);
            _time.Advance(ZoneLengthViewModel.SendDelay / 2);
        }

        _time.Advance(ZoneLengthViewModel.SendDelay);

        Assert.Equal([new ZoneResize("argb", 1, 25)], _actions.Requests);
    }

    [Fact]
    public void BackToTheSentLength_SendsNothing()
    {
        var length = Create();

        length.IncreaseCommand.Execute(null);
        length.DecreaseCommand.Execute(null);
        _time.Advance(ZoneLengthViewModel.SendDelay);

        Assert.Empty(_actions.Requests);
    }

    // They stay enabled, so a keyboard user who presses on at the end of the range keeps the focus.
    [Fact]
    public void DecreaseAndIncrease_StayEnabledAndStopAtTheEndsOfTheRange()
    {
        var empty = Create(new RgbZone("ARGB 2", 0, 0, 1));

        Assert.True(empty.DecreaseCommand.CanExecute(null));
        empty.DecreaseCommand.Execute(null);
        Assert.Equal(0, empty.LedCount);
        empty.IncreaseCommand.Execute(null);
        empty.IncreaseCommand.Execute(null);
        Assert.Equal(1, empty.LedCount);
        Assert.True(empty.IncreaseCommand.CanExecute(null));
    }

    // Like the slider while its thumb is held: the preview follows, the engine gets the length once.
    [Fact]
    public void Drag_SendsTheLengthWhenItIsLetGo()
    {
        var length = Create();

        length.BeginDrag();
        length.LedCount = 30;
        _time.Advance(ZoneLengthViewModel.SendDelay);
        length.LedCount = 40;
        _time.Advance(ZoneLengthViewModel.SendDelay);
        Assert.Empty(_actions.Requests);

        length.EndDrag();

        Assert.Equal([new ZoneResize("argb", 1, 40)], _actions.Requests);
        _time.Advance(ZoneLengthViewModel.SendDelay);
        Assert.Single(_actions.Requests);
    }

    [Fact]
    public void Apply_SendsTheTypedLengthRightAway()
    {
        var length = Create();

        length.LedCountText = "35";
        length.ApplyCommand.Execute(null);

        Assert.Equal(35, length.LedCount);
        Assert.Equal([new ZoneResize("argb", 1, 35)], _actions.Requests);
        Assert.False(length.ApplyCommand.CanExecute(null));
        _time.Advance(ZoneLengthViewModel.SendDelay);
        Assert.Single(_actions.Requests);
    }

    // Typing alone changes nothing yet: a half-typed "1" of "15" must not reach the zone.
    [Fact]
    public void Typing_ChangesNothingUntilApply()
    {
        var length = Create();

        length.LedCountText = "1";
        _time.Advance(ZoneLengthViewModel.SendDelay);

        Assert.Equal(20, length.LedCount);
        Assert.Empty(_actions.Requests);
    }

    [Fact]
    public void Apply_WhileAStepWaits_SendsItRightAway()
    {
        var length = Create();
        length.IncreaseCommand.Execute(null);

        Assert.True(length.ApplyCommand.CanExecute(null));
        length.ApplyCommand.Execute(null);

        Assert.Equal([new ZoneResize("argb", 1, 21)], _actions.Requests);
    }

    [Fact]
    public void ContinueWith_KeepsAnUnsentLengthThatTheZoneCanHold()
    {
        var length = Create();

        length.ContinueWith(61);
        Assert.False(length.HasUnsentChange);

        length.ContinueWith(25);
        _time.Advance(ZoneLengthViewModel.SendDelay);

        Assert.Equal([new ZoneResize("argb", 1, 25)], _actions.Requests);
    }

    [Fact]
    public void Dispose_StopsALengthThatWaits()
    {
        var length = Create();
        length.IncreaseCommand.Execute(null);

        length.Dispose();
        _time.Advance(ZoneLengthViewModel.SendDelay);

        Assert.Empty(_actions.Requests);
    }

    // The timer runs on the thread pool; the length goes out on the UI thread.
    [Fact]
    public void Pause_SendsOnTheUiThread()
    {
        var ui = new QueuedSynchronizationContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(ui);
        ZoneLengthViewModel length;
        try
        {
            length = Create();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        length.IncreaseCommand.Execute(null);
        _time.Advance(ZoneLengthViewModel.SendDelay);
        Assert.Empty(_actions.Requests);

        ui.RunPending();

        Assert.Equal([new ZoneResize("argb", 1, 21)], _actions.Requests);
    }

    // Two identical controllers have the same names; their controls must still not share a focus key.
    [Fact]
    public void FocusKey_ComesFromTheDeviceAndTheZone()
    {
        var first = new ZoneLengthViewModel("controller-1", 1, Header, "Controller – ARGB 2", _actions, _time);
        var second = new ZoneLengthViewModel("controller-2", 1, Header, "Controller – ARGB 2", _actions, _time);
        var again = new ZoneLengthViewModel("controller-1", 1, Header, "Controller – ARGB 2", _actions, _time);

        Assert.NotEqual(first.FocusKey, second.FocusKey);
        Assert.NotEqual(first.FocusKey, Create().FocusKey);
        Assert.Equal(first.FocusKey, again.FocusKey);
    }

    [Fact]
    public void AccessibleNames_NameTheZone()
    {
        var length = Create();

        Assert.Equal("Aantal LED's van Controller – ARGB 2", length.SliderAccessibleName);
        Assert.Equal("Aantal LED's van Controller – ARGB 2 typen", length.TextAccessibleName);
        Assert.Equal("Aantal LED's van Controller – ARGB 2 verlagen", length.DecreaseAccessibleName);
        Assert.Equal("Aantal LED's van Controller – ARGB 2 verhogen", length.IncreaseAccessibleName);
        Assert.Equal("Aantal LED's toepassen voor Controller – ARGB 2", length.ApplyAccessibleName);
        Assert.Equal("Aantal LED's (0–60):", length.Label);
        Assert.Equal((0, 60), (length.Minimum, length.Maximum));
    }

    private ZoneLengthViewModel Create(RgbZone? zone = null) =>
        new("argb", 1, zone ?? Header, "Controller – ARGB 2", _actions, _time);
}
