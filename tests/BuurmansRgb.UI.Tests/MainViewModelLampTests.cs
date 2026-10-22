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
    public async Task PickColor_ForALampThatIsOff_SwitchesOnlyThatLampOnAfterTheColor()
    {
        var viewModel = await ConnectAsync(SwitchOff("strip", "fan"));
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        Api.Calls.Clear();

        viewModel.ColorPicker.Pick("#00FF00");

        Assert.Equal(["color fan #00FF00", "on fan on"], Api.Calls);
        Assert.True(viewModel.SelectedLamp?.IsOn);
        Assert.Equal([false, true], viewModel.Lamps.Lamps.Select(lamp => lamp.IsOn));
    }

    [Fact]
    public async Task PickColor_ForALampThatIsOn_OnlySendsTheColor()
    {
        var viewModel = await ConnectAsync(SwitchOff("strip"));
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        Api.Calls.Clear();

        viewModel.ColorPicker.Pick("#00FF00");

        Assert.Equal(["color fan #00FF00"], Api.Calls);
        Assert.Equal([false, true], viewModel.Lamps.Lamps.Select(lamp => lamp.IsOn));
    }

    [Fact]
    public async Task PickColor_ForTheWholeHouseWithALampOff_SwitchesEveryLampOnAfterTheColor()
    {
        var viewModel = await ConnectAsync(SwitchOff("fan"));

        viewModel.ColorPicker.Pick("#00FF00");

        Assert.Equal(["color all #00FF00", "on all on"], Api.Calls);
        Assert.True(viewModel.Lamps.IsAnyOn);
        Assert.Equal("2 van 2 aan", viewModel.Lamps.Summary);
    }

    [Fact]
    public async Task PickColor_ForTheWholeHouseAllOn_OnlySendsTheColor()
    {
        var viewModel = await ConnectAsync(_ => { });

        viewModel.ColorPicker.Pick("#00FF00");

        Assert.Equal(["color all #00FF00"], Api.Calls);
    }

    [Fact]
    public async Task MixColor_WithoutPuttingItOnTheLamp_LeavesTheLampOff()
    {
        var viewModel = await ConnectAsync(SwitchOff("fan"));
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        Api.Calls.Clear();

        viewModel.ColorPicker.HexColor = "#00FF00";
        viewModel.ColorPicker.SelectPresetCommand.Execute(new RgbColor(255, 59, 59));

        Assert.Empty(Api.Calls);
        Assert.False(viewModel.SelectedLamp?.IsOn);
    }

    [Fact]
    public async Task ColorLeds_OfALampThatIsOff_SwitchesOnlyThatLampOnAfterTheColors()
    {
        var viewModel = await ConnectAsync(SwitchOff("strip", "fan"));
        viewModel.Preview.Devices.Single(preview => preview.DeviceId == "fan").ClickLed(0);

        viewModel.ColorPicker.Pick("#00FF00");

        Assert.Equal(["leds fan 0 #00FF00", "on fan on"], Api.Calls);
        Assert.Equal([false, true], viewModel.Lamps.Lamps.Select(lamp => lamp.IsOn));
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

    // Each device off, with the lighting it has.
    private static Action<FakeEngineApi> SwitchOff(params string[] deviceIds) => api =>
    {
        foreach (var deviceId in deviceIds)
        {
            api.Lighting[deviceId] = api.Lighting.GetValueOrDefault(deviceId, api.Lighting[""]) with { On = false };
        }
    };

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi> arrange)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, arrange));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
