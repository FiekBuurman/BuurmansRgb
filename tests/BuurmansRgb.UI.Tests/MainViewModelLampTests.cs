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

    // Like the engine, a color for "Hele huis" also goes on a device with its own lighting (the fan).
    [Fact]
    public async Task MixColor_ForTheWholeHouse_ThenADeviceWithItsOwnLighting_ShowsTheNewColor()
    {
        var viewModel = await ConnectAsync(_ => { });
        viewModel.ColorPicker.HexColor = "#00FF00";

        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        await Eventually.TrueAsync(() => viewModel.Brightness == 50.0);
        Assert.Equal(new RgbColor(0, 255, 0), viewModel.ColorPicker.Color);
        Assert.False(viewModel.ColorPicker.HasColorNotOnDevice);
        Assert.Equal(["color all #00FF00"], Api.Calls.Where(call => call.StartsWith("color", StringComparison.Ordinal)));
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

    // A slider dragged while the engine is busy: the colors in between are replaced in the queue,
    // the last one wins, and the lamp goes on once, after the first color.
    [Fact]
    public async Task DragSlider_ForALampThatIsOff_SendsFewCallsAndSwitchesItOnOnce()
    {
        var viewModel = await ConnectAsync(SwitchOff("fan"));
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        Api.Calls.Clear();
        var busy = new TaskCompletionSource();
        Api.SetGate = busy.Task;

        for (var green = 1; green <= 255; green++)
        {
            viewModel.ColorPicker.Green = green;
        }

        Api.SetGate = null;
        busy.SetResult();
        await Eventually.TrueAsync(() => Api.Calls.Count >= 3);
        Assert.Equal(["color fan #0001FF", "on fan on", "color fan #00FFFF"], Api.Calls);
        Assert.True(viewModel.SelectedLamp?.IsOn);
    }

    // With "Hele huis" a drag colors every device, and the devices that are off go on once.
    [Fact]
    public async Task DragSlider_ForTheWholeHouseWithALampOff_SwitchesEveryLampOnOnce()
    {
        var viewModel = await ConnectAsync(SwitchOff("fan"));
        var busy = new TaskCompletionSource();
        Api.SetGate = busy.Task;

        for (var green = 1; green <= 255; green++)
        {
            viewModel.ColorPicker.Green = green;
        }

        Api.SetGate = null;
        busy.SetResult();
        await Eventually.TrueAsync(() => Api.Calls.Count >= 3);
        Assert.Equal(["color all #FF0100", "on all on", "color all #FFFF00"], Api.Calls);
        Assert.Equal("2 van 2 aan", viewModel.Lamps.Summary);
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

    // The mixed color goes to the LEDs at once, so the lamp goes on with the first of it, and
    // only then.
    [Fact]
    public async Task MixColors_ForLedsOfALampThatIsOff_SwitchesItOnOnce()
    {
        var viewModel = await ConnectAsync(SwitchOff("fan"));
        viewModel.Preview.Devices.Single(preview => preview.DeviceId == "fan").ClickLed(0);

        viewModel.ColorPicker.HexColor = "#00FF00";
        viewModel.ColorPicker.Red = 255;

        Assert.Equal(["leds fan 0 #00FF00", "on fan on", "leds fan 0 #FFFF00"], Api.Calls);
        Assert.Equal([true, true], viewModel.Lamps.Lamps.Select(lamp => lamp.IsOn));
    }

    [Fact]
    public async Task MixColors_ForTheLoopOfTheWholeHouseWithALampOff_SwitchesEveryLampOnOnce()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Effects = [EffectIds.Static, EffectIds.ColorLoop];
            api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.ColorLoop };
            SwitchOff("fan")(api);
        });

        viewModel.ColorPicker.HexColor = "#00FF00";
        viewModel.ColorPicker.Red = 255;

        Assert.Equal(["loop all #00FF00,#00FF00,#0000FF spread", "on all on", "loop all #FFFF00,#00FF00,#0000FF spread"], Api.Calls);
        Assert.Equal("2 van 2 aan", viewModel.Lamps.Summary);
    }

    [Fact]
    public async Task LightingChanged_ShowsTheLightingOfTheLampsAgain()
    {
        var viewModel = await ConnectAsync(_ => { });

        Api.Lighting["strip"] = new(Red, 0.3, EffectIds.Static, On: true);
        Api.Lighting["fan"] = new(Blue, 1.0, EffectIds.Static, On: false);
        Api.RaiseLightingChanged();

        Assert.Equal("1 van 2 aan", viewModel.Lamps.Summary);
        Assert.Equal("LED-strip · Fel rood · 30%", viewModel.Lamps.Lamps[0].Details);
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

    // The line that the simulated devices are there to try the app: only while they are all there
    // is with "Automatisch". With "Altijd" the user chose them, and with "Nooit" they go.
    [Theory]
    [InlineData(new[] { DeviceSource.Simulated, DeviceSource.Simulated }, SimulatedDevicesMode.Automatic, true)]
    [InlineData(new[] { DeviceSource.Hardware, DeviceSource.Simulated }, SimulatedDevicesMode.Automatic, false)]
    [InlineData(new[] { DeviceSource.Hardware, DeviceSource.Hardware }, SimulatedDevicesMode.Automatic, false)]
    [InlineData(new DeviceSource[0], SimulatedDevicesMode.Automatic, false)]
    [InlineData(new[] { DeviceSource.Simulated, DeviceSource.Simulated }, SimulatedDevicesMode.Always, false)]
    [InlineData(new[] { DeviceSource.Hardware, DeviceSource.Simulated }, SimulatedDevicesMode.Always, false)]
    [InlineData(new[] { DeviceSource.Simulated, DeviceSource.Simulated }, SimulatedDevicesMode.Never, false)]
    [InlineData(new[] { DeviceSource.Hardware, DeviceSource.Hardware }, SimulatedDevicesMode.Never, false)]
    public async Task SimulatedDevicesHint_ShowsOnlyWithOnlySimulatedDevicesOnAutomatic(DeviceSource[] sources, SimulatedDevicesMode mode, bool shows)
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Devices = [.. sources.Select((source, i) => TestDevices.Create($"device-{i}", $"Device {i}", DeviceType.Fan, 2, source))];
            api.SimulatedDevices = mode;
        });

        Assert.Equal(shows, viewModel.ShowsSimulatedDevicesHint);
    }

    // A real device that OpenRGB finds takes the line away, and so does picking "Altijd".
    [Fact]
    public async Task SimulatedDevicesHint_GoesWithARealDeviceOrAlways()
    {
        var simulated = TestDevices.Create("sim", "Simulated fan", DeviceType.Fan, 2, DeviceSource.Simulated);
        var viewModel = await ConnectAsync(api => api.Devices = [simulated]);
        List<string?> changes = [];
        viewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.True(viewModel.ShowsSimulatedDevicesHint);

        Api.Devices = [TestDevices.Create("fan", "Fan", DeviceType.Fan, 2)];
        Api.RaiseDevicesChanged();
        Assert.False(viewModel.ShowsSimulatedDevicesHint);
        Assert.Contains(nameof(MainViewModel.ShowsSimulatedDevicesHint), changes);

        Api.Devices = [simulated];
        Api.RaiseDevicesChanged();
        Assert.True(viewModel.ShowsSimulatedDevicesHint);
        changes.Clear();

        viewModel.SimulatedDevices.SelectedOption = SimulatedDevicesViewModel.Options.Single(option => option.Mode == SimulatedDevicesMode.Always);
        Assert.False(viewModel.ShowsSimulatedDevicesHint);
        Assert.Contains(nameof(MainViewModel.ShowsSimulatedDevicesHint), changes);
    }

    // Connecting shows the devices before the engine said the mode of the simulated devices: the
    // line stays away until "Automatisch" is in, so it does not flash up for someone with "Altijd".
    [Fact]
    public async Task SimulatedDevicesHint_WaitsForTheModeOfTheEngine()
    {
        var slowLighting = new TaskCompletionSource();
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api =>
        {
            api.Devices = [TestDevices.Create("sim", "Simulated fan", DeviceType.Fan, 2, DeviceSource.Simulated)];
            api.LightingGate = slowLighting.Task;
        }));

        var connecting = viewModel.ConnectCommand.ExecuteAsync(null);
        await Eventually.TrueAsync(() => viewModel.Lamps.HasOnlySimulatedLamps);
        Assert.Null(viewModel.SimulatedDevices.SelectedOption);
        Assert.False(viewModel.ShowsSimulatedDevicesHint);

        slowLighting.SetResult();
        await connecting;
        Assert.Equal(SimulatedDevicesMode.Automatic, viewModel.SimulatedDevices.SelectedOption?.Mode);
        Assert.True(viewModel.ShowsSimulatedDevicesHint);
    }

    // Without devices, Apparaten says what is going on: "nog geen apparaten gevonden" on a pc that
    // never had a real one, and where the devices come back on a pc that had real devices (the
    // message that the connection is gone is the status line, StatusLineViewModelTests).
    [Theory]
    [InlineData(false, 0, true, false)]
    [InlineData(true, 0, false, true)]
    [InlineData(false, 1, false, false)]
    [InlineData(true, 1, false, false)]
    public async Task EmptyState_FollowsHadRealDevicesOfTheEngine(bool hadRealDevices, int deviceCount, bool showsNoLamps, bool showsDevicesLost)
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Devices = [.. Enumerable.Range(0, deviceCount).Select(i => TestDevices.Create($"device-{i}", $"Device {i}", DeviceType.Fan, 2))];
            api.HadRealDevices = hadRealDevices;
        });

        Assert.Equal(hadRealDevices, viewModel.HadRealDevices);
        Assert.Equal(showsNoLamps, viewModel.ShowsNoLamps);
        Assert.Equal(showsDevicesLost, viewModel.ShowsDevicesLost);
    }

    // OpenRGB connected with real devices, then gone: the engine records the fact (OptionsChanged)
    // and the list empties (DevicesChanged); the state follows both, in either order.
    [Fact]
    public async Task DevicesLost_ShowsWhenTheRealDevicesGoOnAPcThatHadThem()
    {
        var viewModel = await ConnectAsync(_ => { });
        List<string?> changes = [];
        viewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.False(viewModel.ShowsDevicesLost);

        Api.HadRealDevices = true;
        Api.RaiseOptionsChanged();
        await Eventually.TrueAsync(() => viewModel.HadRealDevices == true);
        Assert.False(viewModel.ShowsDevicesLost);
        Assert.False(viewModel.ShowsNoLamps);

        Api.Devices = [];
        Api.RaiseDevicesChanged();
        await Eventually.TrueAsync(() => viewModel.ShowsDevicesLost);
        Assert.False(viewModel.ShowsNoLamps);
        Assert.Contains(nameof(MainViewModel.ShowsDevicesLost), changes);

        // "Opnieuw verbinden" in the status line brought them back.
        Api.Devices = [TestDevices.Create("fan", "Fan", DeviceType.Fan, 2)];
        Api.RaiseDevicesChanged();
        await Eventually.TrueAsync(() => !viewModel.ShowsDevicesLost);
    }

    // Connecting shows the devices before the engine said whether this pc had real ones: neither
    // empty state shows until it did, so the wrong one does not flash up.
    [Fact]
    public async Task EmptyState_WaitsForTheEngine()
    {
        var slowLighting = new TaskCompletionSource();
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api =>
        {
            api.Devices = [];
            api.HadRealDevices = true;
            api.LightingGate = slowLighting.Task;
        }));

        var connecting = viewModel.ConnectCommand.ExecuteAsync(null);
        await Eventually.TrueAsync(() => viewModel.Devices.Count == 1);
        Assert.Null(viewModel.HadRealDevices);
        Assert.False(viewModel.ShowsNoLamps);
        Assert.False(viewModel.ShowsDevicesLost);

        slowLighting.SetResult();
        await connecting;
        Assert.True(viewModel.ShowsDevicesLost);
        Assert.False(viewModel.ShowsNoLamps);
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
