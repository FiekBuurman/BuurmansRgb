using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class MainViewModelLampTests
{
    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task Connect_ShowsEveryLampWithWhetherItIsOn()
    {
        var viewModel = await ConnectAsync(api => api.Lighting["fan"] = new(Blue, 0.5, EffectIds.Static, On: false));

        Assert.Equal(["strip", "fan"], viewModel.Lamps.Lamps.Select(lamp => lamp.DeviceId));
        Assert.Equal([true, false], viewModel.Lamps.Lamps.Select(lamp => lamp.IsOn));
        Assert.Equal("1 van 2 aan", viewModel.Lamps.Summary);
    }

    [Fact]
    public async Task Lamp_Switched_SendsItForThatDevice()
    {
        var viewModel = await ConnectAsync(_ => { });

        viewModel.Lamps.Lamps[1].IsOn = false;

        Assert.Equal(["on fan off"], Api.Calls);
    }

    [Fact]
    public async Task WholeHouse_SwitchedOff_SendsItForAllDevices()
    {
        var viewModel = await ConnectAsync(_ => { });

        viewModel.Lamps.IsAnyOn = false;

        Assert.Equal(["on all off"], Api.Calls);
    }

    [Fact]
    public async Task LightingChanged_ShowsTheLightingOfTheLampsAgain()
    {
        var viewModel = await ConnectAsync(_ => { });

        Api.Lighting["strip"] = new(Red, 0.3, EffectIds.Static, On: true);
        Api.Lighting["fan"] = new(Blue, 1.0, EffectIds.Static, On: false);
        Api.RaiseLightingChanged();

        Assert.Equal("1 van 2 aan", viewModel.Lamps.Summary);
        Assert.Equal("LED-strip · 30%", viewModel.Lamps.Lamps[0].Details);
        Assert.Equal(2, Api.AllLightingCalls);
    }

    [Fact]
    public async Task FrameRendered_ShowsTheColorsOfTheLamps()
    {
        var viewModel = await ConnectAsync(_ => { });

        Api.RaiseFrame(new LightingFrame(1, [DeviceFrame.FromColors("fan", [Blue, Blue])]));

        Assert.Equal(Blue, viewModel.Lamps.Lamps[1].Color);
    }

    [Fact]
    public async Task Open_ShowsTheLampOnTheColorPageAndLetsItBlink()
    {
        var viewModel = await ConnectAsync(_ => { });

        viewModel.Lamps.OpenCommand.Execute(viewModel.Lamps.Lamps[1]);

        Assert.Equal("fan", viewModel.SelectedDevice?.DeviceId);
        Assert.Equal(AppPage.Color, viewModel.Navigation.CurrentPage);
        Assert.Equal(["flash fan"], Api.Calls);
    }

    [Fact]
    public async Task DevicesChanged_ShowsTheNewLamps()
    {
        var viewModel = await ConnectAsync(_ => { });

        Api.Devices = [TestDevices.Create("keyboard", "Keyboard", DeviceType.Keyboard, 4)];
        Api.RaiseDevicesChanged();

        Assert.Equal("keyboard", Assert.Single(viewModel.Lamps.Lamps).DeviceId);
    }

    [Fact]
    public async Task Disconnected_StopsFollowingTheLighting()
    {
        await ConnectAsync(_ => { });
        var api = Api;

        _connector.Connection.Disconnect();

        Assert.False(api.HasLightingSubscribers);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi> arrange)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, arrange));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
