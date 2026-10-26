using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;
using StreamJsonRpc;

namespace BuurmansRgb.UI.Tests;

public sealed class MainViewModelTests
{
    // A board with LEDs of its own and an addressable header with a fan on it.
    private static readonly DeviceInfo Board = new("board", "Board", DeviceType.Motherboard, 5,
        [RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB 1", 3, 0, 60)]) { Source = DeviceSource.Hardware };

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
        Assert.Equal((255.0, 0.0, 0.0), (viewModel.ColorPicker.Red, viewModel.ColorPicker.Green, viewModel.ColorPicker.Blue));
        Assert.False(viewModel.ColorPicker.HasUnappliedChange);
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
    public async Task Green_Changed_MixesTheColorButSendsItOnlyWhenApplied()
    {
        var viewModel = await ConnectAsync();

        viewModel.ColorPicker.Green = 255;

        Assert.Empty(Api.Calls);
        Assert.Equal("#FFFF00", viewModel.ColorPicker.HexColor);
        Assert.True(viewModel.ColorPicker.HasUnappliedChange);

        viewModel.ColorPicker.ApplyCommand.Execute(null);

        Assert.Equal(["color all #FFFF00"], Api.Calls);
        Assert.False(viewModel.ColorPicker.HasUnappliedChange);
    }

    [Fact]
    public async Task HexColor_ValidAndApplied_SendsAndUpdatesSliders()
    {
        var viewModel = await ConnectAsync();

        viewModel.ColorPicker.Pick("#0000ff");

        Assert.Equal(["color all #0000FF"], Api.Calls);
        Assert.Equal((0.0, 0.0, 255.0), (viewModel.ColorPicker.Red, viewModel.ColorPicker.Green, viewModel.ColorPicker.Blue));
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
    public async Task SelectPreset_ShowsTheColorAndApplySendsIt()
    {
        var viewModel = await ConnectAsync();

        viewModel.ColorPicker.SelectPresetCommand.Execute(new RgbColor(255, 154, 61));
        Assert.Empty(Api.Calls);
        Assert.Equal("#FF9A3D", viewModel.ColorPicker.HexColor);

        viewModel.ColorPicker.ApplyCommand.Execute(null);

        Assert.Equal(["color all #FF9A3D"], Api.Calls);
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

    // Running through the list with the arrow keys flashes only the lamp the choice rests on.
    [Fact]
    public async Task SelectDevicesQuickly_FlashesOnlyTheLastOne_OnceTheChoiceRests()
    {
        var time = new FakeTimeProvider();
        var viewModel = new MainViewModel(_connector, time);
        await viewModel.ConnectCommand.ExecuteAsync(null);
        DeviceItem Device(string id) => viewModel.Devices.Single(device => device.DeviceId == id);

        viewModel.SelectedDevice = Device("fan");
        viewModel.SelectedDevice = Device("strip");
        time.Advance(TimeSpan.FromMilliseconds(100));
        viewModel.SelectedDevice = Device("fan");
        Assert.Equal(["flash fan"], Api.Calls);

        time.Advance(MainViewModel.FlashRest);
        Assert.Equal(["flash fan", "flash fan"], Api.Calls);

        time.Advance(TimeSpan.FromSeconds(1));
        viewModel.SelectedDevice = Device("strip");
        Assert.Equal(["flash fan", "flash fan", "flash strip"], Api.Calls);
    }

    [Fact]
    public async Task SelectWholeHouseQuickly_CancelsTheFlashThatWaits()
    {
        var time = new FakeTimeProvider();
        var viewModel = new MainViewModel(_connector, time);
        await viewModel.ConnectCommand.ExecuteAsync(null);

        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "strip");
        viewModel.SelectedDevice = viewModel.Devices[0];
        time.Advance(TimeSpan.FromSeconds(1));

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

    // A device does not blink while the effects are paused, so the button is grey then.
    [Theory]
    [InlineData(PauseReasons.Manual)]
    [InlineData(PauseReasons.Locked)]
    public async Task Paused_GreysOutFlash_AndRunningAgainNot(PauseReasons pause)
    {
        var viewModel = await ConnectAsync();
        var flash = viewModel.Preview.Devices.Single(preview => preview.DeviceId == "strip").FlashCommand;

        Api.RaisePause(pause);
        Assert.False(flash.CanExecute(null));

        Api.RaisePause(PauseReasons.None);
        Assert.True(flash.CanExecute(null));
    }

    [Fact]
    public async Task FlashZoneInThePreview_FlashesThatZone()
    {
        var viewModel = await ConnectAsync(api => api.Devices = [Board]);

        viewModel.Preview.Devices.Single().Zones[1].FlashCommand.Execute(null);

        Assert.Equal(["flash board zone 1"], Api.Calls);
    }

    // The engine reports the device again with the choice, and the preview shows it.
    [Fact]
    public async Task ZonesAlikeInThePreview_GoesToTheEngineAndStays()
    {
        var viewModel = await ConnectAsync(api => api.Devices = [Board]);

        viewModel.Preview.Devices.Single().ZonesAlike = true;

        Assert.Equal(["zones-alike board on"], Api.Calls);
        await Eventually.TrueAsync(() => viewModel.Preview.Devices.Single().ZonesAlike);

        viewModel.Preview.Devices.Single().ZonesAlike = false;

        Assert.Equal(["zones-alike board on", "zones-alike board off"], Api.Calls);
        await Eventually.TrueAsync(() => !viewModel.Preview.Devices.Single().ZonesAlike);
    }

    // The switch is on the Kleur page: a color the user is mixing there stays in the picker when
    // the engine reports the device again with the choice.
    [Fact]
    public async Task ZonesAlikeInThePreview_WhileMixingAColor_KeepsTheMixedColor()
    {
        var viewModel = await ConnectAsync(api => api.Devices = [Board]);
        var before = viewModel.Preview.Devices.Single();
        viewModel.ColorPicker.Green = 255;
        var mixed = viewModel.ColorPicker.Color;

        before.ZonesAlike = true;

        await Eventually.TrueAsync(() => viewModel.Preview.Devices.Single() is { ZonesAlike: true } shown && shown != before);
        Assert.Equal(mixed, viewModel.ColorPicker.Color);
        Assert.True(viewModel.ColorPicker.HasUnappliedChange);
    }

    // Another settings app, or settings.json by hand: the engine reports the devices again.
    [Fact]
    public async Task ZonesAlikeElsewhere_ShowsInThePreview()
    {
        var viewModel = await ConnectAsync(api => api.Devices = [Board]);

        Api.Devices = [Board with { ZonesAlike = true }];
        Api.RaiseDevicesChanged();

        await Eventually.TrueAsync(() => viewModel.Preview.Devices.Single().ZonesAlike);
        Assert.Empty(Api.Calls);
    }

    // A button in the preview flashes at once; the flash that waits from running through the list
    // then does not blink over it later.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FlashInThePreview_CancelsTheFlashThatWaits(bool zone)
    {
        var time = new FakeTimeProvider();
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api => api.Devices = [.. api.Devices, Board]), time);
        await viewModel.ConnectCommand.ExecuteAsync(null);
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "board");

        var preview = viewModel.Preview.Devices.Single();
        (zone ? preview.Zones[1].FlashCommand : preview.FlashCommand).Execute(null);
        time.Advance(MainViewModel.FlashRest);

        Assert.Equal(["flash fan", zone ? "flash board zone 1" : "flash board"], Api.Calls);
    }

    [Theory]
    [InlineData(PauseReasons.Manual)]
    [InlineData(PauseReasons.DisplayOff)]
    [InlineData(PauseReasons.ScreenSaver)]
    public async Task Paused_GreysOutFlashOfTheZones_AndRunningAgainNot(PauseReasons pause)
    {
        var viewModel = await ConnectAsync(api => api.Devices = [Board]);
        var flash = viewModel.Preview.Devices.Single().Zones[1].FlashCommand;

        Api.RaisePause(pause);
        Assert.False(flash.CanExecute(null));

        Api.RaisePause(PauseReasons.None);
        Assert.True(flash.CanExecute(null));
    }

    [Fact]
    public async Task Connect_WhilePaused_GreysOutFlash()
    {
        var viewModel = await ConnectAsync(api => api.Pause = PauseReasons.Manual);

        Assert.All(viewModel.Preview.Devices, preview => Assert.False(preview.FlashCommand.CanExecute(null)));
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

        viewModel.ColorPicker.Pick("#00FF00");
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
        using var context = QueuedSynchronizationContext.Install();
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

    // A color mixed for a lamp that is gone does not stay for "Hele huis".
    [Fact]
    public async Task DevicesChanged_SelectedDeviceGoneWhileMixingAColor_ShowsTheColorOfAllDevices()
    {
        var viewModel = await ConnectAsync();
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        viewModel.ColorPicker.Green = 255;
        Api.Devices = [Api.Devices[0]];

        Api.RaiseDevicesChanged();

        Assert.Null(viewModel.SelectedDevice?.DeviceId);
        Assert.Equal(new RgbColor(255, 0, 0), viewModel.ColorPicker.Color);
        Assert.False(viewModel.ColorPicker.HasUnappliedChange);
    }

    // Not only the Zones gelijk switch: any new list keeps the color mixed for the same lamp.
    [Fact]
    public async Task DevicesChanged_WhileMixingAColor_KeepsTheMixedColor()
    {
        var viewModel = await ConnectAsync();
        viewModel.ColorPicker.Green = 255;
        var mixed = viewModel.ColorPicker.Color;
        Api.Devices = [.. Api.Devices, TestDevices.Create("openrgb-gpu", "GPU", DeviceType.Gpu, 4)];

        Api.RaiseDevicesChanged();

        Assert.Equal(4, viewModel.Devices.Count);
        Assert.Equal(mixed, viewModel.ColorPicker.Color);
        Assert.True(viewModel.ColorPicker.HasUnappliedChange);
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

    // As the catch-up of scene times and the hotkeys start before the engine answers.
    [Fact]
    public void SceneTimesAreOffAndHotkeysOnAtFirst()
    {
        var viewModel = new MainViewModel(_connector);

        Assert.False(viewModel.SceneTimes.IsOn);
        Assert.True(viewModel.Hotkeys.IsOn);
    }

    [Fact]
    public async Task Connect_ShowsTheOptionsWithoutSendingThem()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.FramesPerSecond = 60;
            api.PauseOnLockAndSleep = false;
            api.PausedLights = PausedLightsMode.Off;
            api.CatchUpSceneTimes = true;
            api.Hotkeys = false;
            api.CustomColors = [new RgbColor(255, 0, 0), new RgbColor(0, 0, 255)];
        });

        Assert.Equal(60, viewModel.EffectOptions.SelectedFrameRate?.FramesPerSecond);
        Assert.False(viewModel.EffectOptions.PauseOnLockAndSleep);
        Assert.Equal(PausedLightsMode.Off, viewModel.EffectOptions.SelectedPausedLights?.Mode);
        Assert.True(viewModel.SceneTimes.IsOn);
        Assert.False(viewModel.Hotkeys.IsOn);
        Assert.Equal([new RgbColor(255, 0, 0), new RgbColor(0, 0, 255)], viewModel.CustomColors.Colors.Select(item => item.Color));
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task OptionsChangedByTheUser_SendsThem()
    {
        var viewModel = await ConnectAsync();

        viewModel.EffectOptions.SelectedFrameRate = viewModel.EffectOptions.FrameRates.Single(rate => rate.FramesPerSecond == 15);
        viewModel.EffectOptions.PauseOnLockAndSleep = false;
        viewModel.EffectOptions.SelectedPausedLights = EffectOptionsViewModel.PausedLightsOptions.Single(option => option.Mode == PausedLightsMode.Dim);
        viewModel.SceneTimes.IsOn = true;
        viewModel.Hotkeys.IsOn = false;
        viewModel.ColorPicker.HexColor = "#00FF00";
        viewModel.CustomColors.AddCommand.Execute(null);

        Assert.Equal(["fps 15", "pause on lock False", "paused lights Dim", "catch up True", "hotkeys False", "custom colors #00FF00"], Api.Calls);
    }

    [Fact]
    public async Task Connect_ShowsTheOpenRgbStatusAndFollowsIt()
    {
        var viewModel = await ConnectAsync(api => api.OpenRgbStatus = new ServerStatus(ServerState.Unreachable));
        Assert.Equal(Strings.OpenRgbUnreachable, viewModel.OpenRgbStatus.Text);

        Api.RaiseOpenRgbStatus(new ServerStatus(ServerState.Connected, 3));

        Assert.Equal("OpenRGB: verbonden, 3 apparaten", viewModel.OpenRgbStatus.Text);
        Assert.False(viewModel.OpenRgbStatus.HasHint);
    }

    [Fact]
    public async Task Connect_StatusEventOvertakesTheAnswer_KeepsTheNewerStatus()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.OpenRgbStatus = new ServerStatus(ServerState.Unreachable);
            api.StatusChangeWhileAnswering = new ServerStatus(ServerState.Connected, 2);
        });

        Assert.Equal("OpenRGB: verbonden, 2 apparaten", viewModel.OpenRgbStatus.Text);
    }

    [Fact]
    public async Task Disconnected_StopsFollowingTheOpenRgbStatus()
    {
        await ConnectAsync();
        var api = Api;

        _connector.Connection.Disconnect();

        Assert.False(api.HasOpenRgbStatusSubscribers);
    }

    [Fact]
    public async Task Disconnected_ShowsNoOpenRgbStatusAnyMore()
    {
        var viewModel = await ConnectAsync(api => api.OpenRgbStatus = new ServerStatus(ServerState.Unreachable));

        _connector.Connection.Disconnect();

        Assert.Equal(string.Empty, viewModel.OpenRgbStatus.Text);
        Assert.False(viewModel.OpenRgbStatus.HasHint);
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

    // Another settings app saved a scene: the list follows, the selection stays.
    [Fact]
    public async Task ProfilesChangedElsewhere_ShowsTheNewList()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.AddRange(["Gamen", "Nacht"]));
        viewModel.Profiles.SelectedName = "Nacht";

        Api.Profiles.Insert(0, "Avond");
        Api.RaiseProfilesChanged();

        Assert.Equal(["Avond", "Gamen", "Nacht"], viewModel.Profiles.Names);
        Assert.Equal("Nacht", viewModel.Profiles.SelectedName);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task OptionsChangedElsewhere_ShowsThemWithoutSendingThemBack()
    {
        var viewModel = await ConnectAsync();

        Api.SimulatedDevices = SimulatedDevicesMode.Never;
        Api.FramesPerSecond = 15;
        Api.PauseOnLockAndSleep = false;
        Api.PausedLights = PausedLightsMode.Dim;
        Api.CatchUpSceneTimes = true;
        Api.Hotkeys = false;
        Api.CustomColors = [new RgbColor(0, 255, 0)];
        Api.RaiseOptionsChanged();

        Assert.Equal(SimulatedDevicesMode.Never, viewModel.SimulatedDevices.SelectedOption?.Mode);
        Assert.Equal(15, viewModel.EffectOptions.SelectedFrameRate?.FramesPerSecond);
        Assert.False(viewModel.EffectOptions.PauseOnLockAndSleep);
        Assert.Equal(PausedLightsMode.Dim, viewModel.EffectOptions.SelectedPausedLights?.Mode);
        Assert.True(viewModel.SceneTimes.IsOn);
        Assert.False(viewModel.Hotkeys.IsOn);
        Assert.Equal([new RgbColor(0, 255, 0)], viewModel.CustomColors.Colors.Select(item => item.Color));
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task Disconnect_StopsListeningToProfilesAndOptions()
    {
        var viewModel = await ConnectAsync();
        var connection = _connector.Connection;

        viewModel.Dispose();

        Assert.False(connection.FakeApi.HasProfilesOrOptionsSubscribers);
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
