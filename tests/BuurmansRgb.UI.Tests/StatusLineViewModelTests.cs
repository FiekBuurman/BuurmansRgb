using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.UI.Tests;

// Which message the status line shows when more than one holds, what the others do under
// "Details", and that each keeps its own action; with fake sources, no engine.
public sealed class StatusLineViewModelTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly FakeAnimationEffects _animations = new();
    private readonly OpenRgbStatusViewModel _openRgb = new();
    private readonly EffectsPauseViewModel _pause;
    private readonly PreviewMotionViewModel _motion;
    private readonly DeviceRescanViewModel _rescan;
    private readonly StatusLineViewModel _line;

    public StatusLineViewModelTests()
    {
        _pause = new EffectsPauseViewModel(_time);
        _motion = new PreviewMotionViewModel(_animations);
        _rescan = new DeviceRescanViewModel(_time);
        _line = new StatusLineViewModel(_openRgb, _pause, _motion, _rescan);
    }

    public void Dispose()
    {
        _line.Dispose();
        _pause.Dispose();
        _motion.Dispose();
        _rescan.Dispose();
    }

    [Fact]
    public void New_SaysNothing()
    {
        Assert.False(_line.IsShown);
        Assert.Null(_line.Current);
        Assert.Empty(_line.Others);
        Assert.False(_line.HasDetails);
    }

    // One message: in the line, with its action; no "Details".
    [Fact]
    public void Paused_OneMessageWithHervatten()
    {
        _pause.Show(PauseReasons.Manual);

        var current = Assert.IsType<StatusMessage>(_line.Current);
        Assert.True(_line.IsShown);
        Assert.Equal(StatusMessageKind.Paused, current.Kind);
        Assert.Equal(Strings.EffectsPausedManual, current.Text);
        Assert.Equal(Strings.ResumeEffects, current.ActionText);
        Assert.Equal(Strings.ResumeEffectsAccessibleName, current.ActionAccessibleName);
        Assert.Equal(Strings.ResumeEffectsToolTip, current.ActionHelpText);
        Assert.True(current.HasAction);
        Assert.True(current.ActionEndsMessage);
        Assert.False(_line.HasDetails);
    }

    // A pause from Windows has no "Hervatten".
    [Fact]
    public void PausedByWindows_NoAction()
    {
        _pause.Show(PauseReasons.Locked);

        Assert.Equal(Strings.EffectsPausedLocked, _line.Current?.Text);
        Assert.False(_line.Current?.HasAction);
    }

    // The pause comes before the preview that stands still: the preview goes under "Details",
    // with "Laten lopen" still its own.
    [Fact]
    public void PausedAndStill_PauseFirstAndThePreviewUnderDetails()
    {
        _animations.Turn(on: false);
        _pause.Show(PauseReasons.Manual);

        Assert.Equal(StatusMessageKind.Paused, _line.Current?.Kind);
        var other = Assert.Single(_line.Others);
        Assert.Equal(StatusMessageKind.PreviewStill, other.Kind);
        Assert.Equal(Strings.PreviewStill, other.Text);
        Assert.Equal(Strings.PreviewRun, other.ActionText);
        Assert.Equal(Strings.PreviewRunAccessibleName, other.ActionAccessibleName);
        Assert.True(_line.HasDetails);
        Assert.Equal("Details: nog 1 melding", _line.DetailsAccessibleName);
        Assert.Equal(Strings.PreviewStill, other.ToString());
    }

    // The connection gone comes before everything, with "Opnieuw verbinden"; the rest in order.
    [Fact]
    public void ConnectionLost_ComesFirstWithTheRestInOrder()
    {
        _animations.Turn(on: false);
        _pause.Show(PauseReasons.Manual);
        _line.DevicesExpected = true;

        _openRgb.Show(new ServerStatus(ServerState.Unreachable));

        var current = Assert.IsType<StatusMessage>(_line.Current);
        Assert.Equal(StatusMessageKind.ConnectionLost, current.Kind);
        Assert.Equal(Strings.StatusConnectionLost, current.Text);
        Assert.Equal(Strings.Reconnect, current.ActionText);
        Assert.Equal(Strings.Reconnect, current.ActionAccessibleName);
        Assert.Equal(Strings.ReconnectToolTip, current.ActionHelpText);
        Assert.Same(_rescan.RescanCommand, current.ActionCommand);
        Assert.Equal([StatusMessageKind.Paused, StatusMessageKind.PreviewStill], _line.Others.Select(message => message.Kind));
        Assert.Equal(Strings.ResumeEffects, _line.Others[0].ActionText);
        Assert.Equal("Details: nog 2 meldingen", _line.DetailsAccessibleName);
    }

    // OpenRGB that gives no good answer is a lost connection too; connecting is not.
    [Theory]
    [InlineData(ServerState.Failed, true)]
    [InlineData(ServerState.Connecting, false)]
    [InlineData(ServerState.Connected, false)]
    public void OpenRgbState_OnlyOutOfReachIsALostConnection(ServerState state, bool lost)
    {
        _line.DevicesExpected = true;

        _openRgb.Show(new ServerStatus(state, 2));

        Assert.Equal(lost, _line.IsShown);
    }

    // Without a real device ever on this pc, OpenRGB out of reach is no message: the simulated
    // devices are all there is, and the status at the top right says it.
    [Fact]
    public void ConnectionLost_OnlyOnAPcThatHadRealDevices()
    {
        _openRgb.Show(new ServerStatus(ServerState.Unreachable));
        Assert.False(_line.IsShown);

        _line.DevicesExpected = true;
        Assert.Equal(StatusMessageKind.ConnectionLost, _line.Current?.Kind);

        _line.DevicesExpected = false;
        Assert.False(_line.IsShown);
    }

    // The message that matters most falls away: the next one takes the line, and leaves "Details".
    [Fact]
    public void ConnectionBack_TheNextMessageTakesTheLine()
    {
        _animations.Turn(on: false);
        _pause.Show(PauseReasons.Manual);
        _line.DevicesExpected = true;
        _openRgb.Show(new ServerStatus(ServerState.Unreachable));
        _line.ShowsDetails = true;

        _openRgb.Show(new ServerStatus(ServerState.Connected, 2));

        Assert.Equal(StatusMessageKind.Paused, _line.Current?.Kind);
        Assert.Equal([StatusMessageKind.PreviewStill], _line.Others.Select(message => message.Kind));
        Assert.True(_line.ShowsDetails);
    }

    // A pause that ends says so for a moment, without a button, and then goes.
    [Fact]
    public void Resumed_SaysSoWithoutActionAndThenGoes()
    {
        _pause.Show(PauseReasons.Manual);

        _pause.Show(PauseReasons.None);

        Assert.Equal(Strings.EffectsResumed, _line.Current?.Text);
        Assert.False(_line.Current?.HasAction);

        _time.Advance(EffectsPauseViewModel.ResumedMessageTime);

        Assert.False(_line.IsShown);
    }

    // The action follows its source: "Laten lopen" becomes "Stilzetten", "Opnieuw verbinden" says
    // "Zoeken…" while the search runs, on the same message.
    [Fact]
    public void Actions_FollowTheirSource()
    {
        _animations.Turn(on: false);
        var still = Assert.IsType<StatusMessage>(_line.Current);
        still.ActionCommand!.Execute(null);
        Assert.Same(still, _line.Current);
        Assert.Equal(Strings.PreviewRunsAnyway, still.Text);
        Assert.Equal(Strings.PreviewStop, still.ActionText);
        Assert.Equal(Strings.PreviewStopAccessibleName, still.ActionAccessibleName);
        Assert.Equal(Strings.PreviewStopHelp, still.ActionHelpText);

        _line.DevicesExpected = true;
        _openRgb.Show(new ServerStatus(ServerState.Unreachable));
        var lost = Assert.IsType<StatusMessage>(_line.Current);
        lost.ActionCommand!.Execute(null);
        Assert.Equal(Strings.RescanSearching, lost.ActionText);
        Assert.Equal(Strings.RescanSearching, lost.ActionAccessibleName);
    }

    // "Details" closes by itself once nothing is left under it, so it does not open on its own
    // the next time.
    [Fact]
    public void Details_CloseWhenNothingIsLeftUnderThem()
    {
        _animations.Turn(on: false);
        _pause.Show(PauseReasons.Locked);
        _line.ShowsDetails = true;

        _animations.Turn(on: true);

        Assert.Empty(_line.Others);
        Assert.False(_line.HasDetails);
        Assert.False(_line.ShowsDetails);
        Assert.Equal(StatusMessageKind.Paused, _line.Current?.Kind);
    }

    [Fact]
    public void Dispose_StopsFollowing()
    {
        _line.Dispose();

        _pause.Show(PauseReasons.Manual);

        Assert.False(_line.IsShown);
    }
}
