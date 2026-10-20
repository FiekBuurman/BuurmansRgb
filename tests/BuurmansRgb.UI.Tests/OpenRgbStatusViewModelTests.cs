using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class OpenRgbStatusViewModelTests
{
    private readonly OpenRgbStatusViewModel _status = new();

    [Fact]
    public void New_IsConnectingWithoutHint()
    {
        Assert.Equal("OpenRGB: verbinden…", _status.Text);
        Assert.False(_status.HasHint);
    }

    [Theory]
    [InlineData(0, "OpenRGB: verbonden, maar OpenRGB vindt geen apparaten")]
    [InlineData(1, "OpenRGB: verbonden, 1 apparaat")]
    [InlineData(5, "OpenRGB: verbonden, 5 apparaten")]
    public void Connected_TellsTheNumberOfDevices(int deviceCount, string text)
    {
        _status.Show(new ServerStatus(ServerState.Connected, deviceCount));

        Assert.Equal(text, _status.Text);
        Assert.Null(_status.Hint);
    }

    [Fact]
    public void Unreachable_GivesTheTipToStartOpenRgb()
    {
        _status.Show(new ServerStatus(ServerState.Unreachable));

        Assert.Equal("OpenRGB: niet bereikbaar", _status.Text);
        Assert.Equal(Strings.OpenRgbUnreachableHint, _status.Hint);
        Assert.True(_status.HasHint);
        Assert.Contains("SDK-server", _status.Hint);
    }

    [Fact]
    public void Failed_SaysItKeepsTrying()
    {
        _status.Show(new ServerStatus(ServerState.Failed));

        Assert.Equal(Strings.OpenRgbFailed, _status.Text);
        Assert.Equal(Strings.OpenRgbFailedHint, _status.Hint);
    }

    [Fact]
    public void Clear_ShowsNothing()
    {
        _status.Show(new ServerStatus(ServerState.Unreachable));

        _status.Clear();

        Assert.Equal(string.Empty, _status.Text);
        Assert.False(_status.HasHint);
    }

    [Fact]
    public void BackToConnecting_DropsTheHint()
    {
        var changed = new List<string?>();
        _status.Show(new ServerStatus(ServerState.Unreachable));
        _status.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _status.Show(ServerStatus.Connecting);

        Assert.Equal(Strings.OpenRgbConnecting, _status.Text);
        Assert.False(_status.HasHint);
        Assert.Contains(nameof(OpenRgbStatusViewModel.HasHint), changed);
    }
}
