using BuurmansRgb.Core;
using Microsoft.Extensions.Time.Testing;
using OpenRgbDeviceType = OpenRGB.NET.DeviceType;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

public sealed class OpenRgbDeviceProviderTests : IDisposable
{
    private readonly FakeOpenRgbConnector _connector = new();
    private readonly FakeTimeProvider _time = new();
    private readonly OpenRgbDeviceProvider _provider;
    private int _devicesChanged;

    public OpenRgbDeviceProviderTests()
    {
        _provider = new OpenRgbDeviceProvider(_connector, _time, OpenRgbDeviceProvider.RetryDelay);
        _provider.DevicesChanged += (_, _) => Interlocked.Increment(ref _devicesChanged);
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task DiscoverAsync_FirstCall_ConnectsInBackgroundAndReportsTheDevices()
    {
        Assert.Empty(await _provider.DiscoverAsync(Ct));

        await _provider.WhenIdleAsync();

        Assert.Equal(1, _devicesChanged);
        var device = Assert.Single(await _provider.DiscoverAsync(Ct));
        Assert.Equal(OpenRgbDeviceMapper.CreateId(_connector.Devices[0]), device.Id);
        Assert.Equal(1, _connector.ConnectCount);
    }

    // Some graphics cards only know their own hardware effects in OpenRGB: they stay in the list,
    // without a Direct mode being forced on them.
    [Fact]
    public async Task Connect_SetsCustomModeOnDevicesWithDirectMode()
    {
        _connector.Devices =
        [
            TestDevices.Create(index: 0, name: "Strip", modes: ["Direct"]),
            TestDevices.Create(index: 1, name: "Old GPU", type: OpenRgbDeviceType.Gpu, modes: ["Static", "Breathing"]),
            TestDevices.Create(index: 2, name: "Keyboard", modes: ["Custom"]),
        ];

        await ConnectAsync();

        Assert.Equal([0, 2], _connector.CustomModeIndexes);
        var devices = await _provider.DiscoverAsync(Ct);
        Assert.Equal(3, devices.Count);
        Assert.Equal(DeviceType.Gpu, devices[1].Type);
        Assert.Equal([true, false, true], devices.Select(device => device.HasDirectMode));
    }

    [Fact]
    public async Task Connect_MotherboardAndRamSticks_AllAppearWithTheirTypeAndLedCount()
    {
        _connector.Devices =
        [
            TestDevices.Create(index: 0, name: "ASUS TUF GAMING B650-PLUS", type: OpenRgbDeviceType.Motherboard,
                zones: [new("Aura Mainboard", 3, 3, 3), new("Aura Addressable 1", 0, 0, 120)]),
            TestDevices.Create(index: 1, name: "ENE DRAM", type: OpenRgbDeviceType.Dram, location: "I2C: PIIX4, address 0x70", ledCount: 8),
            TestDevices.Create(index: 2, name: "ENE DRAM", type: OpenRgbDeviceType.Dram, location: "I2C: PIIX4, address 0x71", ledCount: 8),
        ];

        var devices = await ConnectAsync();

        Assert.Equal([(DeviceType.Motherboard, 3), (DeviceType.Ram, 8), (DeviceType.Ram, 8)], devices.Select(device => (device.Type, device.LedCount)));
        Assert.Equal([null, "0x70", "0x71"], devices.Select(device => device.Location?.Address));
        Assert.Equal([0, 1, 2], _connector.CustomModeIndexes);

        _provider.SetLeds(devices[2].Id, [.. Enumerable.Repeat(Orange, 8)]);

        Assert.Equal(2, Assert.Single(_connector.Updates).Index);
    }

    [Fact]
    public async Task Connect_Gpu_AppearsWithItsTypeAndLedCountAndTakesColors()
    {
        _connector.Devices =
        [
            TestDevices.Create(index: 0, name: "Strip"),
            TestDevices.Create(index: 1, name: "ASUS ROG STRIX RTX 4080", type: OpenRgbDeviceType.Gpu,
                location: "I2C: NVIDIA GPU, address 0x67", zones: [new("GPU", 22, 22, 22), new("Addressable header", 0, 0, 60)]),
        ];

        var devices = await ConnectAsync();

        var gpu = devices[1];
        Assert.Equal((DeviceType.Gpu, 22, 2), (gpu.Type, gpu.LedCount, gpu.Zones.Count));
        Assert.Equal([0, 1], _connector.CustomModeIndexes);

        _provider.SetLeds(gpu.Id, [.. Enumerable.Repeat(Orange, 22)]);

        var update = Assert.Single(_connector.Updates);
        Assert.Equal(1, update.Index);
        Assert.Equal(22, update.Colors.Length);
    }

    [Fact]
    public async Task SetLeds_SendsColorsToTheServerIndexOfTheDevice()
    {
        _connector.Devices = [TestDevices.Create(index: 0, name: "Fan"), TestDevices.Create(index: 1, name: "Strip", ledCount: 2)];
        var devices = await ConnectAsync();

        _provider.SetLeds(devices[1].Id, [Orange, Orange]);

        var update = Assert.Single(_connector.Updates);
        Assert.Equal(1, update.Index);
        Assert.Equal([Orange, Orange], update.Colors);
    }

    [Fact]
    public async Task SetLeds_UnknownDeviceOrOtherLedCount_SendsNothing()
    {
        var device = Assert.Single(await ConnectAsync());

        _provider.SetLeds("openrgb-gone-12345678", [Orange, Orange, Orange]);
        _provider.SetLeds(device.Id, [Orange]);

        Assert.Empty(_connector.Updates);
    }

    [Fact]
    public async Task SetLeds_DeviceWithoutLeds_SendsNothing()
    {
        _connector.Devices = [TestDevices.Create(zones: [new("Addressable header", 0, 0, 120)])];
        var device = Assert.Single(await ConnectAsync());

        _provider.SetLeds(device.Id, []);

        Assert.Empty(_connector.Updates);
    }

    [Fact]
    public async Task SetLeds_BeforeConnecting_SendsNothing()
    {
        _provider.SetLeds(OpenRgbDeviceMapper.CreateId(_connector.Devices[0]), [Orange, Orange, Orange]);

        await _provider.WhenIdleAsync();
        Assert.Empty(_connector.Updates);
    }

    [Fact]
    public async Task ServerNotRunning_TriesAgainEveryFiveSeconds()
    {
        _connector.IsRunning = false;
        await ConnectAsync();

        _time.Advance(OpenRgbDeviceProvider.RetryDelay - TimeSpan.FromMilliseconds(1));
        await _provider.WhenIdleAsync();
        Assert.Equal(1, _connector.ConnectCount);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        await _provider.WhenIdleAsync();
        Assert.Equal(2, _connector.ConnectCount);

        _time.Advance(OpenRgbDeviceProvider.RetryDelay);
        await _provider.WhenIdleAsync();
        Assert.Equal(3, _connector.ConnectCount);
        Assert.Equal(0, _devicesChanged);
    }

    [Fact]
    public async Task ServerStartsLater_DevicesAppearAfterTheNextTry()
    {
        _connector.IsRunning = false;
        await ConnectAsync();

        _connector.IsRunning = true;
        await RetryAsync();

        Assert.Single(await _provider.DiscoverAsync(Ct));
        Assert.Equal(1, _devicesChanged);
    }

    [Fact]
    public async Task Connected_DoesNotTryAgain()
    {
        await ConnectAsync();

        _time.Advance(TimeSpan.FromHours(1));
        await _provider.WhenIdleAsync();

        Assert.Equal(1, _connector.ConnectCount);
    }

    [Fact]
    public async Task ServerStops_DevicesDisappearAndComeBackWhenItRestarts()
    {
        await ConnectAsync();
        var firstConnection = _connector.LastConnection;

        _connector.Stop();
        await _provider.WhenIdleAsync();

        Assert.True(firstConnection.IsDisposed);
        Assert.Empty(await _provider.DiscoverAsync(Ct));
        Assert.Equal(2, _devicesChanged);

        await RetryAsync();
        Assert.Empty(await _provider.DiscoverAsync(Ct));

        _connector.IsRunning = true;
        await RetryAsync();

        Assert.Single(await _provider.DiscoverAsync(Ct));
        Assert.Equal(3, _devicesChanged);
        Assert.Equal([0, 0], _connector.CustomModeIndexes);
    }

    [Fact]
    public async Task SetLeds_SendFails_DropsTheConnectionAndReconnects()
    {
        var device = Assert.Single(await ConnectAsync());
        _connector.LastConnection.FailSends = true;

        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);
        await _provider.WhenIdleAsync();

        Assert.True(_connector.Connections[0].IsDisposed);
        Assert.Empty(await _provider.DiscoverAsync(Ct));

        await RetryAsync();

        Assert.Equal(2, _connector.ConnectCount);
        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);
        Assert.Single(_connector.Updates);
    }

    [Fact]
    public async Task SetLeds_ConnectionBusy_SkipsTheColorsAndKeepsTheConnection()
    {
        var device = Assert.Single(await ConnectAsync());
        _connector.LastConnection.IsBusy = true;

        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);
        await _provider.WhenIdleAsync();

        Assert.Empty(_connector.Updates);
        Assert.False(_connector.LastConnection.IsDisposed);
        Assert.Single(await _provider.DiscoverAsync(Ct));
    }

    [Fact]
    public async Task DeviceListChanged_ReloadsTheDevices()
    {
        await ConnectAsync();
        _connector.Devices = [TestDevices.Create(index: 0, name: "Strip"), TestDevices.Create(index: 1, name: "Mouse", type: OpenRgbDeviceType.Mouse)];

        _connector.LastConnection.RaiseDeviceListChanged();
        await _provider.WhenIdleAsync();

        Assert.Equal(["Strip", "Mouse"], (await _provider.DiscoverAsync(Ct)).Select(device => device.Name));
        Assert.Equal(2, _devicesChanged);
        Assert.Equal([0, 0, 1], _connector.CustomModeIndexes);
    }

    [Fact]
    public async Task DeviceListChanged_UntilReloaded_SendsNoColorsToTheOldIndexes()
    {
        var device = Assert.Single(await ConnectAsync());
        var answer = new TaskCompletionSource();
        _connector.DevicesAnswered = answer.Task;
        _connector.Devices = [TestDevices.Create(index: 0, name: "Mouse", type: OpenRgbDeviceType.Mouse), TestDevices.Create(index: 1)];

        _connector.LastConnection.RaiseDeviceListChanged();
        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);

        Assert.Empty(_connector.Updates);
        answer.SetResult();
        await _provider.WhenIdleAsync();
        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);
        Assert.Equal(1, Assert.Single(_connector.Updates).Index);
    }

    [Fact]
    public async Task RestoreDevices_SetsCustomModeAgainAndReportsTheDevices()
    {
        _connector.Devices =
        [
            TestDevices.Create(index: 0, name: "Motherboard", type: OpenRgbDeviceType.Motherboard),
            TestDevices.Create(index: 1, name: "ENE DRAM", type: OpenRgbDeviceType.Dram),
            TestDevices.Create(index: 2, name: "GPU", type: OpenRgbDeviceType.Gpu),
        ];
        await ConnectAsync();

        _provider.RestoreDevices();
        await _provider.WhenIdleAsync();

        Assert.Equal([0, 1, 2, 0, 1, 2], _connector.CustomModeIndexes);
        Assert.Equal(2, _devicesChanged);
        Assert.Equal(1, _connector.ConnectCount);
    }

    // USB keyboards, mice and headsets often go back to their own effect after sleep.
    [Fact]
    public async Task RestoreDevices_KeyboardMouseAndHeadset_TakeColorsAgainWithTheirKeyMap()
    {
        var keys = new LedMatrix(1, 3, [0, LedMatrix.NoLed, 1]);
        _connector.Devices =
        [
            TestDevices.Create(index: 0, name: "Keyboard", type: OpenRgbDeviceType.Keyboard, zones: [new("Keyboard", 2, 2, 2, keys)]),
            TestDevices.Create(index: 1, name: "Mouse", type: OpenRgbDeviceType.Mouse, ledCount: 2),
            TestDevices.Create(index: 2, name: "Headset", type: OpenRgbDeviceType.Headset, ledCount: 2),
        ];
        await ConnectAsync();

        _provider.RestoreDevices();
        await _provider.WhenIdleAsync();
        var devices = await _provider.DiscoverAsync(Ct);
        foreach (var device in devices)
        {
            _provider.SetLeds(device.Id, [Orange, Orange]);
        }

        Assert.Equal([0, 1, 2, 0, 1, 2], _connector.CustomModeIndexes);
        Assert.Equal(keys, devices[0].Zones[0].Matrix);
        Assert.Equal([0, 1, 2], _connector.Updates.Select(update => update.Index));
    }

    [Fact]
    public async Task RestoreDevices_UntilReloaded_SendsNoColors()
    {
        var device = Assert.Single(await ConnectAsync());
        var answer = new TaskCompletionSource();
        _connector.DevicesAnswered = answer.Task;

        _provider.RestoreDevices();
        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);

        Assert.Empty(_connector.Updates);
        answer.SetResult();
        await _provider.WhenIdleAsync();
        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);
        Assert.Single(_connector.Updates);
    }

    [Fact]
    public async Task RestoreDevices_WithoutConnection_DoesNothing()
    {
        _connector.IsRunning = false;
        await ConnectAsync();

        _provider.RestoreDevices();
        await _provider.WhenIdleAsync();

        Assert.Equal(1, _connector.ConnectCount);
        Assert.Equal(0, _devicesChanged);
    }

    // An ARGB controller with two channels; the second one is resized.
    [Fact]
    public async Task ResizeZone_ResizesOnTheServerAndReportsTheNewLength()
    {
        _connector.Devices =
        [
            TestDevices.Create(index: 0, name: "Fan"),
            TestDevices.Create(index: 1, name: "ARGB controller", zones: [new("Channel 1", 10, 0, 60), new("Channel 2", 0, 0, 60)]),
        ];
        var devices = await ConnectAsync();

        _provider.ResizeZone(devices[1].Id, 1, 24);
        await _provider.WhenIdleAsync();

        Assert.Equal([(1, 1, 24)], _connector.Resizes);
        Assert.Equal(2, _devicesChanged);
        var controller = (await _provider.DiscoverAsync(Ct))[1];
        Assert.Equal(34, controller.LedCount);
        Assert.Equal(new RgbZone("Channel 2", 24, 0, 60), controller.Zones[1]);
        Assert.Equal([0, 1, 0, 1], _connector.CustomModeIndexes);

        _provider.SetLeds(controller.Id, [.. Enumerable.Repeat(Orange, 34)]);
        Assert.Equal(34, Assert.Single(_connector.Updates).Colors.Length);
    }

    // OpenRGB keeps the length, so waking up from sleep does not undo it.
    [Fact]
    public async Task RestoreDevices_AfterResize_KeepsTheNewLength()
    {
        _connector.Devices = [TestDevices.Create(name: "ARGB controller", zones: [new("Channel 1", 10, 0, 60)])];
        var device = Assert.Single(await ConnectAsync());
        _provider.ResizeZone(device.Id, 0, 30);
        await _provider.WhenIdleAsync();

        _provider.RestoreDevices();
        await _provider.WhenIdleAsync();

        Assert.Equal(30, Assert.Single(await _provider.DiscoverAsync(Ct)).LedCount);
        Assert.Equal([0, 0, 0], _connector.CustomModeIndexes);
    }

    [Fact]
    public async Task ResizeZone_UntilReloaded_SendsNoColors()
    {
        _connector.Devices = [TestDevices.Create(zones: [new("Strip", 3, 0, 60)])];
        var device = Assert.Single(await ConnectAsync());
        var answer = new TaskCompletionSource();
        _connector.DevicesAnswered = answer.Task;

        _provider.ResizeZone(device.Id, 0, 5);
        await Eventually.TrueAsync(() => _connector.Resizes.Count == 1, TimeSpan.FromSeconds(5));
        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);

        Assert.Empty(_connector.Updates);
        answer.SetResult();
        await _provider.WhenIdleAsync();
        _provider.SetLeds(device.Id, [Orange, Orange, Orange, Orange, Orange]);
        Assert.Single(_connector.Updates);
    }

    // A rescan clears the devices at once, while its reload waits behind the resize.
    [Fact]
    public async Task ResizeZone_DeviceListChangesBeforeItRuns_StillResizes()
    {
        _connector.Devices = [TestDevices.Create(zones: [new("Strip", 3, 0, 60)])];
        var device = Assert.Single(await ConnectAsync());
        var answer = new TaskCompletionSource();
        _connector.DevicesAnswered = answer.Task;
        _provider.RestoreDevices();
        _provider.ResizeZone(device.Id, 0, 5);

        // Raised by the reload in front of the resize, so the resize has not started yet.
        var rescanned = false;
        _provider.DevicesChanged += (_, _) =>
        {
            if (!rescanned)
            {
                rescanned = true;
                _connector.LastConnection.RaiseDeviceListChanged();
            }
        };
        answer.SetResult();
        await _provider.WhenIdleAsync();

        Assert.True(rescanned);
        Assert.Equal([(0, 0, 5)], _connector.Resizes);
        Assert.Equal(5, Assert.Single(await _provider.DiscoverAsync(Ct)).LedCount);
    }

    [Fact]
    public async Task ResizeZone_UnknownDevice_ResizesNothing()
    {
        await ConnectAsync();

        _provider.ResizeZone("openrgb-unknown", 0, 5);
        await _provider.WhenIdleAsync();

        Assert.Empty(_connector.Resizes);
        Assert.Single(await _provider.DiscoverAsync(Ct));
    }

    [Fact]
    public async Task ResizeZone_SendFails_DropsTheConnection()
    {
        _connector.Devices = [TestDevices.Create(zones: [new("Strip", 3, 0, 60)])];
        var device = Assert.Single(await ConnectAsync());
        _connector.LastConnection.FailSends = true;

        _provider.ResizeZone(device.Id, 0, 5);
        await _provider.WhenIdleAsync();

        Assert.True(_connector.LastConnection.IsDisposed);
        Assert.Empty(await _provider.DiscoverAsync(Ct));
    }

    [Fact]
    public async Task ResizeZone_WithoutConnection_DoesNothing()
    {
        _connector.IsRunning = false;
        await ConnectAsync();

        _provider.ResizeZone(OpenRgbDeviceMapper.CreateId(_connector.Devices[0]), 0, 5);
        await _provider.WhenIdleAsync();

        Assert.Empty(_connector.Resizes);
        Assert.Equal(0, _devicesChanged);
    }

    [Fact]
    public async Task DeviceListChanged_ReloadFails_DropsTheConnection()
    {
        await ConnectAsync();
        _connector.GetDevicesException = new TimeoutException();

        _connector.LastConnection.RaiseDeviceListChanged();
        await _provider.WhenIdleAsync();

        Assert.True(_connector.LastConnection.IsDisposed);
        Assert.Empty(await _provider.DiscoverAsync(Ct));
    }

    [Fact]
    public async Task Connect_LoadingDevicesFails_ClosesConnectionAndTriesAgain()
    {
        _connector.GetDevicesException = new TimeoutException();
        await ConnectAsync();

        Assert.True(_connector.LastConnection.IsDisposed);
        Assert.Equal(0, _devicesChanged);

        _connector.GetDevicesException = null;
        await RetryAsync();

        Assert.Single(await _provider.DiscoverAsync(Ct));
    }

    [Fact]
    public async Task ClosedWhileLoading_DropsTheConnectionAfterwards()
    {
        _connector.CloseWhileLoadingDevices = true;

        Assert.Empty(await ConnectAsync());

        Assert.True(_connector.LastConnection.IsDisposed);
        _connector.CloseWhileLoadingDevices = false;
        await RetryAsync();
        Assert.Single(await _provider.DiscoverAsync(Ct));
    }

    [Fact]
    public async Task ClosedBeforeSubscribing_DropsTheConnection()
    {
        _connector.CloseBeforeSubscribing = true;

        Assert.Empty(await ConnectAsync());

        Assert.True(_connector.LastConnection.IsDisposed);
        Assert.Equal(2, _devicesChanged);
    }

    [Fact]
    public async Task Dispose_ClosesTheConnectionAndStopsTrying()
    {
        await ConnectAsync();
        var connection = _connector.LastConnection;

        _provider.Dispose();
        connection.Close();
        _time.Advance(TimeSpan.FromHours(1));
        await _provider.WhenIdleAsync();

        Assert.True(connection.IsDisposed);
        Assert.Equal(1, _connector.ConnectCount);
        Assert.Empty(await _provider.DiscoverAsync(Ct));
    }

    [Fact]
    public async Task Dispose_WhileWaitingToRetry_StopsTrying()
    {
        _connector.IsRunning = false;
        await ConnectAsync();

        _provider.Dispose();
        _connector.IsRunning = true;
        _time.Advance(TimeSpan.FromHours(1));
        await _provider.WhenIdleAsync();

        Assert.Equal(1, _connector.ConnectCount);
    }

    private async Task<IReadOnlyList<RgbDevice>> ConnectAsync()
    {
        await _provider.DiscoverAsync(Ct);
        await _provider.WhenIdleAsync();
        return await _provider.DiscoverAsync(Ct);
    }

    private async Task RetryAsync()
    {
        _time.Advance(OpenRgbDeviceProvider.RetryDelay);
        await _provider.WhenIdleAsync();
    }
}
