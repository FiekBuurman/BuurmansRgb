using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The texts are spelled out: the plain ones so a change to them is a choice, the technical ones the
// same as in TrayPresenterTests, so the tooltip and the tray say the same.
public sealed class OpenRgbStatusViewModelTests
{
    private readonly OpenRgbStatusViewModel _status = new();

    [Fact]
    public void New_IsConnectingWithoutHintOrRestart()
    {
        Assert.Equal("Verbinden met je apparaten…", _status.Text);
        Assert.Equal("OpenRGB: verbinden… (SDK-server op 127.0.0.1:6742)", _status.TechnicalText);
        Assert.Equal(_status.TechnicalText, _status.ToolTip);
        Assert.False(_status.HasHint);
        Assert.False(_status.CanRestart);
        Assert.False(_status.RestartCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(0, "Verbonden, geen apparaten gevonden", "OpenRGB: verbonden, geen apparaten gevonden")]
    [InlineData(1, "1 apparaat verbonden", "OpenRGB: verbonden, 1 apparaat")]
    [InlineData(5, "5 apparaten verbonden", "OpenRGB: verbonden, 5 apparaten")]
    public void Connected_TellsTheNumberOfDevices(int deviceCount, string text, string technical)
    {
        _status.Show(new ServerStatus(ServerState.Connected, deviceCount));

        Assert.Equal(text, _status.Text);
        Assert.Equal($"{technical} (SDK-server op 127.0.0.1:6742)", _status.TechnicalText);
        Assert.Null(_status.Hint);
        Assert.False(_status.IsDisconnected);
        Assert.False(_status.IsConnectionLost);
        Assert.False(_status.CanRestart);
    }

    // Plain words in view; OpenRGB, the SDK server and the tip in the tooltip, with "Herstarten".
    [Fact]
    public void Unreachable_SaysNoConnectionWithTheTechnicalStatusAndTheTipBehindIt()
    {
        _status.DevicesExpected = true;
        _status.Show(new ServerStatus(ServerState.Unreachable));

        Assert.Equal("Geen verbinding met je apparaten", _status.Text);
        Assert.Equal("OpenRGB: niet bereikbaar (SDK-server op 127.0.0.1:6742)", _status.TechnicalText);
        Assert.Equal(Strings.OpenRgbUnreachableHint, _status.Hint);
        Assert.True(_status.HasHint);
        Assert.Equal($"{_status.TechnicalText}\n{Strings.OpenRgbUnreachableHint}", _status.ToolTip);
        Assert.Contains("SDK-server", _status.Hint);
        Assert.Contains("Herstarten", _status.Hint);
        Assert.True(_status.IsDisconnected);
        Assert.True(_status.IsConnectionLost);
        Assert.True(_status.CanRestart);
        Assert.True(_status.RestartCommand.CanExecute(null));
    }

    // Without a real device ever on this pc, OpenRGB out of reach is how someone tries the app:
    // nothing is broken, so no "Herstarten", and the tip says the simulated devices play along.
    [Fact]
    public void Unreachable_WithoutRealDevicesEver_StaysCalmWithoutHerstarten()
    {
        var changed = new List<string?>();
        _status.Show(new ServerStatus(ServerState.Unreachable));
        _status.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal("Nog geen echte apparaten gevonden", _status.Text);
        Assert.Equal("OpenRGB: niet bereikbaar (SDK-server op 127.0.0.1:6742)", _status.TechnicalText);
        Assert.Equal(Strings.OpenRgbUnreachableNoDevicesHint, _status.Hint);
        Assert.Contains("gesimuleerde", _status.Hint);
        Assert.True(_status.IsDisconnected);
        Assert.False(_status.IsConnectionLost);
        Assert.False(_status.CanRestart);
        Assert.False(_status.RestartCommand.CanExecute(null));

        _status.DevicesExpected = true;

        Assert.Equal("Geen verbinding met je apparaten", _status.Text);
        Assert.True(_status.IsConnectionLost);
        Assert.True(_status.CanRestart);
        Assert.Contains(nameof(OpenRgbStatusViewModel.Text), changed);
        Assert.Contains(nameof(OpenRgbStatusViewModel.IsConnectionLost), changed);
        Assert.Contains(nameof(OpenRgbStatusViewModel.CanRestart), changed);
    }

    // OpenRGB that answers wrongly is trouble on any pc.
    [Fact]
    public void Failed_SaysConnectingFailsAndKeepsTrying()
    {
        _status.Show(new ServerStatus(ServerState.Failed));

        Assert.Equal("Verbinden met je apparaten lukt niet", _status.Text);
        Assert.Equal("OpenRGB: geen goed antwoord (SDK-server op 127.0.0.1:6742)", _status.TechnicalText);
        Assert.Equal(Strings.OpenRgbFailedHint, _status.Hint);
        Assert.True(_status.CanRestart);
    }

    [Fact]
    public void Restart_AsksForIt()
    {
        var requests = 0;
        _status.RestartRequested += (_, _) => requests++;
        _status.DevicesExpected = true;
        _status.Show(new ServerStatus(ServerState.Unreachable));

        _status.RestartCommand.Execute(null);

        Assert.Equal(1, requests);
    }

    [Fact]
    public void ConnectedAgain_TakesRestartAway()
    {
        var changed = new List<string?>();
        _status.DevicesExpected = true;
        _status.Show(new ServerStatus(ServerState.Unreachable));
        _status.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _status.Show(new ServerStatus(ServerState.Connected, 2));

        Assert.False(_status.CanRestart);
        Assert.False(_status.RestartCommand.CanExecute(null));
        Assert.Contains(nameof(OpenRgbStatusViewModel.IsConnectionLost), changed);
        Assert.Contains(nameof(OpenRgbStatusViewModel.CanRestart), changed);
    }

    [Fact]
    public void Clear_ShowsNothing()
    {
        _status.DevicesExpected = true;
        _status.Show(new ServerStatus(ServerState.Unreachable));

        _status.Clear();

        Assert.Equal(string.Empty, _status.Text);
        Assert.Equal(string.Empty, _status.TechnicalText);
        Assert.Null(_status.ToolTip);
        Assert.False(_status.HasHint);
        Assert.False(_status.CanRestart);
    }

    [Fact]
    public void BackToConnecting_DropsTheHint()
    {
        var changed = new List<string?>();
        _status.Show(new ServerStatus(ServerState.Unreachable));
        _status.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _status.Show(ServerStatus.Connecting);

        Assert.Equal(Strings.DevicesConnecting, _status.Text);
        Assert.False(_status.HasHint);
        Assert.Contains(nameof(OpenRgbStatusViewModel.HasHint), changed);
        Assert.Contains(nameof(OpenRgbStatusViewModel.Text), changed);
    }
}
