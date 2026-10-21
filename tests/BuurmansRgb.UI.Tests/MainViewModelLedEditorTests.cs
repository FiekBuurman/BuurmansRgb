using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class MainViewModelLedEditorTests
{
    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task ClickLed_SelectsItAndShowsItsStoredColorWithoutSending()
    {
        var viewModel = await ConnectAsync();
        Api.LedColors["fan"] = [new RgbColor(1, 2, 3), new RgbColor(9, 8, 7)];

        Click(viewModel, "fan", 1);

        Assert.Equal(new LedSelection("fan", "Fan", 1, 1), viewModel.LedEditor.Selection);
        Assert.Equal(new RgbColor(9, 8, 7), viewModel.ColorPicker.Color);
        var fan = viewModel.Preview.Devices.Single(preview => preview.DeviceId == "fan");
        Assert.Equal((1, 1), fan.ShownSelection());
        Assert.Equal((-1, -1), viewModel.Preview.Devices.Single(preview => preview.DeviceId == "strip").ShownSelection());
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task ColorPicked_WithLedsSelected_ColorsOnlyThoseLeds()
    {
        var viewModel = await ConnectAsync();
        Click(viewModel, "strip", 0);
        Click(viewModel, "strip", 1, extend: true);

        viewModel.ColorPicker.HexColor = "#00FF00";

        Assert.Equal(["leds strip 0 #00FF00,#00FF00"], Api.Calls);
    }

    [Fact]
    public async Task ClickLed_ExtendIntoTheNextZone_SelectsAcrossBothZones()
    {
        var viewModel = await ConnectAsync();
        Api.Devices = [new DeviceInfo("board", "Board", DeviceType.Motherboard, 4,
            [RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB header", 2, 0, 120)]) { Source = DeviceSource.Hardware }];
        Api.RaiseDevicesChanged();

        Click(viewModel, "board", 1);
        Click(viewModel, "board", 2, extend: true);
        viewModel.ColorPicker.HexColor = "#00FF00";

        Assert.Equal(new LedSelection("board", "Board", 1, 2), viewModel.LedEditor.Selection);
        var board = Assert.Single(viewModel.Preview.Devices);
        Assert.Equal([(1, 1), (0, 0)], board.Zones.Select(zone => (zone.SelectionFirst, zone.SelectionLast)));
        Assert.Equal(["leds board 1 #00FF00,#00FF00"], Api.Calls);
    }

    [Fact]
    public async Task SelectZone_ColorsOnlyThatRingOfTheFan()
    {
        var viewModel = await ConnectAsync();
        Api.Devices = [new DeviceInfo("fan", "Fan", DeviceType.Fan, 5, [RgbZone.Fixed("Inner ring", 2), RgbZone.Fixed("Outer ring", 3)]) { Source = DeviceSource.Hardware }];
        Api.RaiseDevicesChanged();

        Assert.Single(viewModel.Preview.Devices).Zones[1].SelectZoneCommand.Execute(null);
        viewModel.ColorPicker.HexColor = "#00FF00";

        Assert.Equal(new LedSelection("fan", "Fan", 2, 4), viewModel.LedEditor.Selection);
        Assert.Equal((2, 4), viewModel.Preview.Devices[0].ShownSelection());
        Assert.Equal(["leds fan 2 #00FF00,#00FF00,#00FF00"], Api.Calls);
    }

    [Fact]
    public async Task SelectZone_WhileEditingTheDevice_SelectsTheZoneInstead()
    {
        var viewModel = await ConnectAsync();
        Api.Devices = [new DeviceInfo("fan", "Fan", DeviceType.Fan, 5, [RgbZone.Fixed("Inner ring", 2), RgbZone.Fixed("Outer ring", 3)]) { Source = DeviceSource.Hardware }];
        Api.RaiseDevicesChanged();
        Click(viewModel, "fan", 3);

        viewModel.Preview.Devices[0].Zones[0].SelectZoneCommand.Execute(null);

        Assert.Equal(new LedSelection("fan", "Fan", 0, 1), viewModel.LedEditor.Selection);
    }

    [Fact]
    public async Task ApplyLedCount_AsksTheEngineToResizeTheZone()
    {
        var viewModel = await ConnectAsync();
        Api.Devices = [new DeviceInfo("board", "Board", DeviceType.Motherboard, 2, [RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB header", 0, 0, 120)]) { Source = DeviceSource.Hardware }];
        Api.RaiseDevicesChanged();
        var header = Assert.Single(viewModel.Preview.Devices).Zones[1];

        header.LedCountText = "24";
        header.ApplyLedCountCommand.Execute(null);

        Assert.Equal(["resize board 1 24"], Api.Calls);
    }

    [Fact]
    public async Task DevicesChanged_EditedZoneGrew_ClearsTheLedSelection()
    {
        var viewModel = await ConnectAsync();
        var board = new DeviceInfo("board", "Board", DeviceType.Motherboard, 2, [RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB header", 0, 0, 120)]) { Source = DeviceSource.Hardware };
        Api.Devices = [board];
        Api.RaiseDevicesChanged();
        Click(viewModel, "board", 1);

        Api.Devices = [board with { LedCount = 26, Zones = [RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB header", 24, 0, 120)] }];
        Api.RaiseDevicesChanged();

        Assert.Null(viewModel.LedEditor.Selection);
        Assert.Equal(24, viewModel.Preview.Devices[0].Zones[1].LedCount);
    }

    [Fact]
    public async Task FillGradient_SendsTheGradientOfTheRange()
    {
        var viewModel = await ConnectAsync();
        Api.LedColors["strip"] = [new RgbColor(255, 0, 0), RgbColor.White, new RgbColor(0, 0, 255)];
        Click(viewModel, "strip", 0);
        Click(viewModel, "strip", 2, extend: true);

        Assert.True(viewModel.FillGradientCommand.CanExecute(null));
        viewModel.FillGradientCommand.Execute(null);

        Assert.Equal(["leds strip 0 #FF0000,#800080,#0000FF"], Api.Calls);
    }

    [Fact]
    public async Task FillGradient_OneLedSelected_CannotRun()
    {
        var viewModel = await ConnectAsync();

        Click(viewModel, "strip", 0);

        Assert.False(viewModel.FillGradientCommand.CanExecute(null));
    }

    [Fact]
    public async Task EditingTheSelectedDevice_ShowsThePerLedEffect()
    {
        var viewModel = await ConnectAsync();
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        Click(viewModel, "fan", 0);

        viewModel.ColorPicker.HexColor = "#00FF00";

        Assert.Equal(EffectIds.PerLed, viewModel.SelectedEffect?.Id);
        Assert.Equal(["flash fan", "leds fan 0 #00FF00"], Api.Calls);
    }

    [Fact]
    public async Task ClearLedSelection_GivesThePickerBackToTheDevice()
    {
        var viewModel = await ConnectAsync();
        Click(viewModel, "fan", 0);

        await viewModel.ClearLedSelectionCommand.ExecuteAsync(null);
        viewModel.ColorPicker.HexColor = "#00FF00";

        Assert.Null(viewModel.LedEditor.Selection);
        Assert.All(viewModel.Preview.Devices, preview => Assert.Equal((-1, -1), preview.ShownSelection()));
        Assert.Equal(["color all #00FF00"], Api.Calls);
    }

    [Fact]
    public async Task SelectAnotherDevice_ClearsTheLedSelection()
    {
        var viewModel = await ConnectAsync();
        Click(viewModel, "fan", 0);

        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "strip");

        Assert.Null(viewModel.LedEditor.Selection);
    }

    [Fact]
    public async Task DevicesChanged_EditedDeviceGone_ClearsTheLedSelection()
    {
        var viewModel = await ConnectAsync();
        Click(viewModel, "fan", 0);

        Api.Devices = [Api.Devices[0]];
        Api.RaiseDevicesChanged();

        Assert.Null(viewModel.LedEditor.Selection);
    }

    [Fact]
    public async Task DevicesChanged_EditedDeviceGotFewerLeds_ClearsTheLedSelection()
    {
        var viewModel = await ConnectAsync();
        Click(viewModel, "strip", 2);

        Api.Devices = [Api.Devices[0] with { LedCount = 2 }, Api.Devices[1]];
        Api.RaiseDevicesChanged();

        Assert.Null(viewModel.LedEditor.Selection);
    }

    [Fact]
    public async Task DevicesChanged_EditedDeviceUnchanged_KeepsTheLedSelection()
    {
        var viewModel = await ConnectAsync();
        Click(viewModel, "strip", 2);

        Api.Devices = [.. Api.Devices];
        Api.RaiseDevicesChanged();

        Assert.Equal(new LedSelection("strip", "LED strip", 2, 2), viewModel.LedEditor.Selection);
    }

    private static void Click(MainViewModel viewModel, string deviceId, int index, bool extend = false) =>
        viewModel.Preview.Devices.Single(preview => preview.DeviceId == deviceId).ClickLed(index, extend);

    private async Task<MainViewModel> ConnectAsync()
    {
        var viewModel = new MainViewModel(_connector);
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
