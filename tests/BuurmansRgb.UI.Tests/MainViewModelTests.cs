using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using StreamJsonRpc;

namespace BuurmansRgb.UI.Tests;

public sealed class MainViewModelTests
{
    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task Connect_EngineNotRunning_ShowsDutchMessage()
    {
        _connector.ConnectException = new TimeoutException();
        var viewModel = new MainViewModel(_connector);

        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.Equal(ConnectionState.Failed, viewModel.ConnectionState);
        Assert.True(viewModel.IsFailed);
        Assert.False(viewModel.IsConnected);
        Assert.Equal(Strings.EngineNotRunning, viewModel.StatusMessage);
    }

    [Fact]
    public async Task Connect_OtherError_ShowsGeneralMessage()
    {
        _connector.ConnectException = new UnauthorizedAccessException();
        var viewModel = new MainViewModel(_connector);

        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.Equal(Strings.ConnectFailed, viewModel.StatusMessage);
    }

    [Fact]
    public async Task Connect_AfterFailure_RetryConnects()
    {
        _connector.ConnectException = new TimeoutException();
        var viewModel = new MainViewModel(_connector);
        await viewModel.ConnectCommand.ExecuteAsync(null);

        _connector.ConnectException = null;
        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsConnected);
        Assert.Null(viewModel.StatusMessage);
        Assert.Equal(2, _connector.ConnectCount);
    }

    [Fact]
    public async Task Connect_ListsAllDevicesFirstAndShowsGlobalLighting()
    {
        var viewModel = await ConnectAsync();

        Assert.Equal([null, "strip", "fan"], viewModel.Devices.Select(device => device.DeviceId));
        Assert.Equal(Strings.AllDevices, viewModel.Devices[0].Name);
        Assert.Same(viewModel.Devices[0], viewModel.SelectedDevice);
        Assert.Equal(new RgbColor(255, 0, 0), viewModel.ColorPicker.Color);
        Assert.Equal("#FF0000", viewModel.ColorPicker.HexColor);
        Assert.Equal(0.0, viewModel.ColorPicker.Hue);
        Assert.Equal(100.0, viewModel.ColorPicker.Saturation);
        Assert.Equal(80.0, viewModel.Brightness);
        Assert.Equal(EffectIds.Static, viewModel.SelectedEffect?.Id);
        Assert.Equal("Statisch", viewModel.SelectedEffect?.DisplayName);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task Connect_ListsTheRealDevicesBeforeTheSimulatedOnes()
    {
        var viewModel = await ConnectAsync(api => api.Devices =
        [
            TestDevices.Create("simulated-fan", "Simulated fan", DeviceType.Fan, 12, DeviceSource.Simulated),
            TestDevices.Create("openrgb-ram", "ENE DRAM", DeviceType.Ram, 8),
            TestDevices.Create("simulated-strip", "Simulated LED strip", DeviceType.LedStrip, 30, DeviceSource.Simulated),
        ]);

        Assert.Equal([null, "openrgb-ram", "simulated-fan", "simulated-strip"], viewModel.Devices.Select(device => device.DeviceId));
        Assert.Equal([null, Strings.DeviceSource_Hardware, Strings.DeviceSource_Simulated, Strings.DeviceSource_Simulated], viewModel.Devices.Select(device => device.Group));
        Assert.Equal(["openrgb-ram", "simulated-fan", "simulated-strip"], viewModel.Preview.Devices.Select(preview => preview.DeviceId));
    }

    [Fact]
    public async Task Connect_TwinsShowTheAddressThatTellsThemApart()
    {
        var viewModel = await ConnectAsync(api => api.Devices =
        [
            TestDevices.Create("ram-1", "ENE DRAM 1", DeviceType.Ram, 8) with { Location = new DeviceLocation("PIIX4", "0x70") },
            TestDevices.Create("ram-2", "ENE DRAM 2", DeviceType.Ram, 8) with { Location = new DeviceLocation("PIIX4", "0x71") },
            TestDevices.Create("fan", "Fan", DeviceType.Fan, 12),
        ]);

        Assert.Equal([null, "Adres 0x70", "Adres 0x71", null], viewModel.Devices.Select(device => device.LocationLabel));
    }

    [Fact]
    public async Task Connect_ShowsPreviewForEveryDeviceFromCurrentFrame()
    {
        var viewModel = await ConnectAsync(api => api.Frame = Frame(1, new RgbColor(9, 9, 9)));

        Assert.Equal(["strip", "fan"], viewModel.Preview.Devices.Select(preview => preview.DeviceId));
        Assert.All(viewModel.Preview.Devices.SelectMany(preview => preview.Colors), led => Assert.Equal(new RgbColor(9, 9, 9), led));
    }

    [Fact]
    public async Task Hue_Changed_SendsColorForAllDevices()
    {
        var viewModel = await ConnectAsync();

        viewModel.ColorPicker.Hue = 120;

        Assert.Equal(["color all #00FF00"], Api.Calls);
        Assert.Equal("#00FF00", viewModel.ColorPicker.HexColor);
        Assert.Equal(new RgbColor(0, 255, 0), viewModel.ColorPicker.Color);
    }

    [Fact]
    public async Task Saturation_Changed_SendsLighterColor()
    {
        var viewModel = await ConnectAsync();

        viewModel.ColorPicker.Saturation = 0;

        Assert.Equal(["color all #FFFFFF"], Api.Calls);
    }

    [Fact]
    public async Task HexColor_Valid_SendsAndUpdatesSliders()
    {
        var viewModel = await ConnectAsync();

        viewModel.ColorPicker.HexColor = "#0000ff";

        Assert.Equal(["color all #0000FF"], Api.Calls);
        Assert.Equal(240.0, viewModel.ColorPicker.Hue, precision: 6);
        Assert.Equal(100.0, viewModel.ColorPicker.Saturation, precision: 6);
    }

    [Theory]
    [InlineData("#00")]
    [InlineData("blue")]
    [InlineData("")]
    public async Task HexColor_Invalid_SendsNothing(string text)
    {
        var viewModel = await ConnectAsync();

        viewModel.ColorPicker.HexColor = text;

        Assert.Empty(Api.Calls);
        Assert.Equal(new RgbColor(255, 0, 0), viewModel.ColorPicker.Color);
    }

    [Fact]
    public async Task SelectPreset_SendsColorAndShowsIt()
    {
        var viewModel = await ConnectAsync();

        viewModel.ColorPicker.SelectPresetCommand.Execute(new RgbColor(255, 136, 0));

        Assert.Equal(["color all #FF8800"], Api.Calls);
        Assert.Equal("#FF8800", viewModel.ColorPicker.HexColor);
    }

    [Fact]
    public async Task Brightness_Changed_SendsFraction()
    {
        var viewModel = await ConnectAsync();

        viewModel.Brightness = 50;

        Assert.Equal(["brightness all 0.50"], Api.Calls);
    }

    [Fact]
    public async Task SelectDevice_ShowsItsLightingAndOnlyFlashesIt()
    {
        var viewModel = await ConnectAsync();

        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        Assert.Equal(new RgbColor(0, 0, 255), viewModel.ColorPicker.Color);
        Assert.Equal(50.0, viewModel.Brightness);
        Assert.Equal(["fan"], viewModel.Preview.Devices.Select(preview => preview.DeviceId));
        Assert.Equal(["flash fan"], Api.Calls);
    }

    [Fact]
    public async Task SelectAllDevices_FlashesNothing()
    {
        var viewModel = await ConnectAsync();
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        viewModel.SelectedDevice = viewModel.Devices[0];

        Assert.Equal(["flash fan"], Api.Calls);
    }

    [Fact]
    public async Task FlashInThePreview_FlashesThatDevice()
    {
        var viewModel = await ConnectAsync();

        viewModel.Preview.Devices.Single(preview => preview.DeviceId == "strip").FlashCommand.Execute(null);

        Assert.Equal(["flash strip"], Api.Calls);
        Assert.Null(viewModel.SelectedDevice?.DeviceId);
    }

    [Fact]
    public async Task SelectDevice_AnswerArrivesAfterReconnect_IsIgnored()
    {
        var viewModel = await ConnectAsync();
        var oldApi = Api;
        var slowAnswer = new TaskCompletionSource();
        oldApi.LightingGate = slowAnswer.Task;
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        await viewModel.ConnectCommand.ExecuteAsync(null);
        oldApi.Lighting["fan"] = new(new RgbColor(0, 255, 0), 0.3, EffectIds.Static);
        slowAnswer.SetResult();

        Assert.Equal("fan", viewModel.SelectedDevice?.DeviceId);
        Assert.Equal(new RgbColor(0, 0, 255), viewModel.ColorPicker.Color);
        Assert.Equal(50.0, viewModel.Brightness);
    }

    [Fact]
    public async Task SelectDevice_ThenChange_SendsForThatDevice()
    {
        var viewModel = await ConnectAsync();
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        viewModel.ColorPicker.HexColor = "#00FF00";
        viewModel.Brightness = 25;
        viewModel.SelectedEffect = viewModel.Effects[0];
        viewModel.SelectedEffect = null;
        viewModel.SelectedEffect = viewModel.Effects[0];

        Assert.Equal(["flash fan", "color fan #00FF00", "brightness fan 0.25", "effect fan static"], Api.Calls);
    }

    [Fact]
    public async Task SelectNothing_KeepsPreviousDeviceSoChangesStayOnIt()
    {
        var viewModel = await ConnectAsync();
        var fan = viewModel.Devices.Single(device => device.DeviceId == "fan");
        viewModel.SelectedDevice = fan;

        viewModel.SelectedDevice = null;
        viewModel.Brightness = 30;

        Assert.Same(fan, viewModel.SelectedDevice);
        Assert.Equal(["fan"], viewModel.Preview.Devices.Select(preview => preview.DeviceId));
        Assert.Equal(["flash fan", "brightness fan 0.30"], Api.Calls);
    }

    [Fact]
    public async Task SelectNothing_OnUiThread_RestoresSelectionAfterwards()
    {
        var context = new QueuedSynchronizationContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var viewModel = await ConnectAsync();
            var fan = viewModel.Devices.Single(device => device.DeviceId == "fan");
            viewModel.SelectedDevice = fan;

            viewModel.SelectedDevice = null;
            viewModel.Brightness = 30;
            Assert.Null(viewModel.SelectedDevice);

            context.RunPending();
            Assert.Same(fan, viewModel.SelectedDevice);

            // Putting the selection back is no new pick, so the fan does not flash again.
            Assert.Equal(["flash fan"], Api.Calls);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public async Task FrameRendered_UpdatesPreview()
    {
        var viewModel = await ConnectAsync();

        Api.RaiseFrame(Frame(5, new RgbColor(1, 2, 3)));

        Assert.All(viewModel.Preview.Devices.SelectMany(preview => preview.Colors), led => Assert.Equal(new RgbColor(1, 2, 3), led));
    }

    [Fact]
    public async Task FrameRendered_OlderThanShown_IsIgnored()
    {
        var viewModel = await ConnectAsync();
        Api.RaiseFrame(Frame(5, new RgbColor(1, 2, 3)));

        Api.RaiseFrame(Frame(4, new RgbColor(7, 7, 7)));

        Assert.All(viewModel.Preview.Devices.SelectMany(preview => preview.Colors), led => Assert.Equal(new RgbColor(1, 2, 3), led));
    }

    [Fact]
    public async Task DevicesChanged_ShowsNewListAndKeepsTheSelectedDevice()
    {
        var viewModel = await ConnectAsync();
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        Api.Devices = [.. Api.Devices, TestDevices.Create("openrgb-gpu", "GPU", DeviceType.Gpu, 4)];

        Api.RaiseDevicesChanged();

        Assert.Equal([null, "strip", "fan", "openrgb-gpu"], viewModel.Devices.Select(device => device.DeviceId));
        Assert.Equal(string.Format(Strings.AllDevicesDetails, 3), viewModel.Devices[0].Details);
        Assert.Equal("fan", viewModel.SelectedDevice?.DeviceId);

        // Only the user's own pick flashed; the new list keeps the selection without a flash.
        Assert.Equal(["flash fan"], Api.Calls);
    }

    [Fact]
    public async Task DevicesChanged_SelectedDeviceGone_SelectsAllDevices()
    {
        var viewModel = await ConnectAsync();
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        Api.Devices = [Api.Devices[0]];

        Api.RaiseDevicesChanged();

        Assert.Null(viewModel.SelectedDevice?.DeviceId);
        Assert.Same(viewModel.Devices[0], viewModel.SelectedDevice);
        Assert.Equal(80, viewModel.Brightness);
    }

    [Fact]
    public async Task DevicesChanged_AllDevicesSelected_PreviewShowsTheNewDevice()
    {
        var viewModel = await ConnectAsync();
        Api.Devices = [.. Api.Devices, TestDevices.Create("openrgb-gpu", "GPU", DeviceType.Gpu, 4)];

        Api.RaiseDevicesChanged();

        Assert.Equal(["strip", "fan", "openrgb-gpu"], viewModel.Preview.Devices.Select(preview => preview.DeviceId));
    }

    [Fact]
    public async Task Connect_ShowsTheModeOfTheSimulatedDevices()
    {
        var viewModel = await ConnectAsync(api => api.SimulatedDevices = SimulatedDevicesMode.Never);

        Assert.Equal(SimulatedDevicesMode.Never, viewModel.SimulatedDevices.SelectedOption?.Mode);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task SimulatedDevicesPicked_SendsTheModeAndFollowsTheNewList()
    {
        var viewModel = await ConnectAsync();

        viewModel.SimulatedDevices.SelectedOption = SimulatedDevicesViewModel.Options.Single(option => option.Mode == SimulatedDevicesMode.Never);
        Api.Devices = [];
        Api.RaiseDevicesChanged();

        Assert.Equal(["simulated Never"], Api.Calls);
        Assert.Equal([null], viewModel.Devices.Select(device => device.DeviceId));
    }

    [Fact]
    public async Task Disconnected_ShowsMessageAndReleasesConnection()
    {
        var viewModel = await ConnectAsync();
        var connection = _connector.Connection;

        connection.Disconnect();

        Assert.Equal(ConnectionState.Failed, viewModel.ConnectionState);
        Assert.Equal(Strings.ConnectionLost, viewModel.StatusMessage);
        Assert.True(connection.IsDisposed);
        Assert.False(connection.FakeApi.HasFrameSubscribers);
    }

    [Fact]
    public async Task ChangeRejectedByEngine_ShowsMessage()
    {
        var viewModel = await ConnectAsync();
        Api.SetException = new RemoteInvocationException("Unknown device.", 0, errorData: null);

        viewModel.Brightness = 10;

        Assert.Equal(Strings.ChangeRejected, viewModel.StatusMessage);
        Assert.True(viewModel.IsConnected);
    }

    [Fact]
    public async Task Dispose_ClosesConnection()
    {
        var viewModel = await ConnectAsync();

        viewModel.Dispose();

        Assert.True(_connector.Connection.IsDisposed);
    }

    private static LightingFrame Frame(long sequence, RgbColor color) =>
        new(sequence, [DeviceFrame.FromColors("strip", [color, color, color]), DeviceFrame.FromColors("fan", [color, color])]);

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null)
    {
        var viewModel = new MainViewModel(arrange is null ? _connector : new ArrangingConnector(_connector, arrange));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
