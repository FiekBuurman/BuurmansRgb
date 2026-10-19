using BuurmansRgb.Core;
using Microsoft.Extensions.Time.Testing;
using OpenRgbDeviceType = OpenRGB.NET.DeviceType;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

public sealed class OpenRgbDeviceProviderTests : IDisposable
{
    private static readonly RgbColor Orange = new(255, 136, 0);

    private readonly FakeOpenRgbServer _server = new();
    private readonly FakeTimeProvider _time = new();
    private readonly OpenRgbDeviceProvider _provider;
    private int _devicesChanged;

    public OpenRgbDeviceProviderTests()
    {
        _provider = new OpenRgbDeviceProvider(_server, _time, OpenRgbDeviceProvider.RetryDelay);
        _provider.DevicesChanged += (_, _) => Interlocked.Increment(ref _devicesChanged);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task DiscoverAsync_FirstCall_ConnectsInBackgroundAndReportsTheDevices()
    {
        Assert.Empty(await _provider.DiscoverAsync(Ct));

        await _provider.WhenIdleAsync();

        Assert.Equal(1, _devicesChanged);
        var device = Assert.Single(await _provider.DiscoverAsync(Ct));
        Assert.Equal(OpenRgbDeviceMapper.CreateId(_server.Devices[0]), device.Id);
        Assert.Equal(1, _server.ConnectCount);
    }

    [Fact]
    public async Task Connect_SetsCustomModeOnDevicesWithDirectMode()
    {
        _server.Devices =
        [
            TestDevices.Create(index: 0, name: "Strip", modes: ["Direct"]),
            TestDevices.Create(index: 1, name: "Old GPU", modes: ["Static", "Breathing"]),
            TestDevices.Create(index: 2, name: "Keyboard", modes: ["Custom"]),
        ];

        await ConnectAsync();

        Assert.Equal([0, 2], _server.CustomModeIndexes);
        Assert.Equal(3, (await _provider.DiscoverAsync(Ct)).Count);
    }

    [Fact]
    public async Task Connect_MotherboardAndRamSticks_AllAppearWithTheirTypeAndLedCount()
    {
        _server.Devices =
        [
            TestDevices.Create(index: 0, name: "ASUS TUF GAMING B650-PLUS", type: OpenRgbDeviceType.Motherboard,
                zones: [new("Aura Mainboard", 3, 3, 3), new("Aura Addressable 1", 0, 0, 120)]),
            TestDevices.Create(index: 1, name: "ENE DRAM", type: OpenRgbDeviceType.Dram, location: "I2C: PIIX4, address 0x70", ledCount: 8),
            TestDevices.Create(index: 2, name: "ENE DRAM", type: OpenRgbDeviceType.Dram, location: "I2C: PIIX4, address 0x71", ledCount: 8),
        ];

        var devices = await ConnectAsync();

        Assert.Equal([(DeviceType.Motherboard, 3), (DeviceType.Ram, 8), (DeviceType.Ram, 8)], devices.Select(device => (device.Type, device.LedCount)));
        Assert.Equal([0, 1, 2], _server.CustomModeIndexes);

        _provider.SetLeds(devices[2].Id, [.. Enumerable.Repeat(Orange, 8)]);

        Assert.Equal(2, Assert.Single(_server.Updates).Index);
    }

    [Fact]
    public async Task Connect_Gpu_AppearsWithItsTypeAndLedCountAndTakesColors()
    {
        _server.Devices =
        [
            TestDevices.Create(index: 0, name: "Strip"),
            TestDevices.Create(index: 1, name: "ASUS ROG STRIX RTX 4080", type: OpenRgbDeviceType.Gpu,
                location: "I2C: NVIDIA GPU, address 0x67", zones: [new("GPU", 22, 22, 22), new("Addressable header", 0, 0, 60)]),
        ];

        var devices = await ConnectAsync();

        var gpu = devices[1];
        Assert.Equal((DeviceType.Gpu, 22, 2), (gpu.Type, gpu.LedCount, gpu.Zones.Count));
        Assert.Equal([0, 1], _server.CustomModeIndexes);

        _provider.SetLeds(gpu.Id, [.. Enumerable.Repeat(Orange, 22)]);

        var update = Assert.Single(_server.Updates);
        Assert.Equal(1, update.Index);
        Assert.Equal(22, update.Colors.Length);
    }

    // Some cards only know their own hardware effects in OpenRGB: they stay in the list, without
    // a Direct mode being forced on them.
    [Fact]
    public async Task Connect_GpuWithoutDirectMode_StillAppears()
    {
        _server.Devices = [TestDevices.Create(name: "Gigabyte RTX 2070", type: OpenRgbDeviceType.Gpu, modes: ["Static", "Breathing", "Color Cycle"])];

        var gpu = Assert.Single(await ConnectAsync());

        Assert.Equal(DeviceType.Gpu, gpu.Type);
        Assert.Empty(_server.CustomModeIndexes);
    }

    [Fact]
    public async Task SetLeds_SendsColorsToTheServerIndexOfTheDevice()
    {
        _server.Devices = [TestDevices.Create(index: 0, name: "Fan"), TestDevices.Create(index: 1, name: "Strip", ledCount: 2)];
        var devices = await ConnectAsync();

        _provider.SetLeds(devices[1].Id, [Orange, Orange]);

        var update = Assert.Single(_server.Updates);
        Assert.Equal(1, update.Index);
        Assert.Equal([Orange, Orange], update.Colors);
    }

    [Fact]
    public async Task SetLeds_UnknownDeviceOrOtherLedCount_SendsNothing()
    {
        var device = Assert.Single(await ConnectAsync());

        _provider.SetLeds("openrgb-gone-12345678", [Orange, Orange, Orange]);
        _provider.SetLeds(device.Id, [Orange]);

        Assert.Empty(_server.Updates);
    }

    [Fact]
    public async Task SetLeds_DeviceWithoutLeds_SendsNothing()
    {
        _server.Devices = [TestDevices.Create(zones: [new("Addressable header", 0, 0, 120)])];
        var device = Assert.Single(await ConnectAsync());

        _provider.SetLeds(device.Id, []);

        Assert.Empty(_server.Updates);
    }

    [Fact]
    public async Task SetLeds_BeforeConnecting_SendsNothing()
    {
        _provider.SetLeds(OpenRgbDeviceMapper.CreateId(_server.Devices[0]), [Orange, Orange, Orange]);

        await _provider.WhenIdleAsync();
        Assert.Empty(_server.Updates);
    }

    [Fact]
    public async Task ServerNotRunning_TriesAgainEveryFiveSeconds()
    {
        _server.IsRunning = false;
        await ConnectAsync();

        _time.Advance(OpenRgbDeviceProvider.RetryDelay - TimeSpan.FromMilliseconds(1));
        await _provider.WhenIdleAsync();
        Assert.Equal(1, _server.ConnectCount);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        await _provider.WhenIdleAsync();
        Assert.Equal(2, _server.ConnectCount);

        _time.Advance(OpenRgbDeviceProvider.RetryDelay);
        await _provider.WhenIdleAsync();
        Assert.Equal(3, _server.ConnectCount);
        Assert.Equal(0, _devicesChanged);
    }

    [Fact]
    public async Task ServerStartsLater_DevicesAppearAfterTheNextTry()
    {
        _server.IsRunning = false;
        await ConnectAsync();

        _server.IsRunning = true;
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

        Assert.Equal(1, _server.ConnectCount);
    }

    [Fact]
    public async Task ServerStops_DevicesDisappearAndComeBackWhenItRestarts()
    {
        await ConnectAsync();
        var firstConnection = _server.LastConnection;

        _server.Stop();
        await _provider.WhenIdleAsync();

        Assert.True(firstConnection.IsDisposed);
        Assert.Empty(await _provider.DiscoverAsync(Ct));
        Assert.Equal(2, _devicesChanged);

        await RetryAsync();
        Assert.Empty(await _provider.DiscoverAsync(Ct));

        _server.IsRunning = true;
        await RetryAsync();

        Assert.Single(await _provider.DiscoverAsync(Ct));
        Assert.Equal(3, _devicesChanged);
        Assert.Equal([0, 0], _server.CustomModeIndexes);
    }

    [Fact]
    public async Task SetLeds_SendFails_DropsTheConnectionAndReconnects()
    {
        var device = Assert.Single(await ConnectAsync());
        _server.LastConnection.FailSends = true;

        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);
        await _provider.WhenIdleAsync();

        Assert.True(_server.Connections[0].IsDisposed);
        Assert.Empty(await _provider.DiscoverAsync(Ct));

        await RetryAsync();

        Assert.Equal(2, _server.ConnectCount);
        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);
        Assert.Single(_server.Updates);
    }

    [Fact]
    public async Task SetLeds_ConnectionBusy_SkipsTheColorsAndKeepsTheConnection()
    {
        var device = Assert.Single(await ConnectAsync());
        _server.LastConnection.IsBusy = true;

        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);
        await _provider.WhenIdleAsync();

        Assert.Empty(_server.Updates);
        Assert.False(_server.LastConnection.IsDisposed);
        Assert.Single(await _provider.DiscoverAsync(Ct));
    }

    [Fact]
    public async Task DeviceListChanged_ReloadsTheDevices()
    {
        await ConnectAsync();
        _server.Devices = [TestDevices.Create(index: 0, name: "Strip"), TestDevices.Create(index: 1, name: "Mouse", type: OpenRgbDeviceType.Mouse)];

        _server.LastConnection.RaiseDeviceListChanged();
        await _provider.WhenIdleAsync();

        Assert.Equal(["Strip", "Mouse"], (await _provider.DiscoverAsync(Ct)).Select(device => device.Name));
        Assert.Equal(2, _devicesChanged);
        Assert.Equal([0, 0, 1], _server.CustomModeIndexes);
    }

    [Fact]
    public async Task DeviceListChanged_UntilReloaded_SendsNoColorsToTheOldIndexes()
    {
        var device = Assert.Single(await ConnectAsync());
        var answer = new TaskCompletionSource();
        _server.DevicesAnswered = answer.Task;
        _server.Devices = [TestDevices.Create(index: 0, name: "Mouse", type: OpenRgbDeviceType.Mouse), TestDevices.Create(index: 1)];

        _server.LastConnection.RaiseDeviceListChanged();
        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);

        Assert.Empty(_server.Updates);
        answer.SetResult();
        await _provider.WhenIdleAsync();
        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);
        Assert.Equal(1, Assert.Single(_server.Updates).Index);
    }

    [Fact]
    public async Task RestoreDevices_SetsCustomModeAgainAndReportsTheDevices()
    {
        _server.Devices =
        [
            TestDevices.Create(index: 0, name: "Motherboard", type: OpenRgbDeviceType.Motherboard),
            TestDevices.Create(index: 1, name: "ENE DRAM", type: OpenRgbDeviceType.Dram),
            TestDevices.Create(index: 2, name: "GPU", type: OpenRgbDeviceType.Gpu),
        ];
        await ConnectAsync();

        _provider.RestoreDevices();
        await _provider.WhenIdleAsync();

        Assert.Equal([0, 1, 2, 0, 1, 2], _server.CustomModeIndexes);
        Assert.Equal(2, _devicesChanged);
        Assert.Equal(1, _server.ConnectCount);
    }

    [Fact]
    public async Task RestoreDevices_UntilReloaded_SendsNoColors()
    {
        var device = Assert.Single(await ConnectAsync());
        var answer = new TaskCompletionSource();
        _server.DevicesAnswered = answer.Task;

        _provider.RestoreDevices();
        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);

        Assert.Empty(_server.Updates);
        answer.SetResult();
        await _provider.WhenIdleAsync();
        _provider.SetLeds(device.Id, [Orange, Orange, Orange]);
        Assert.Single(_server.Updates);
    }

    [Fact]
    public async Task RestoreDevices_WithoutConnection_DoesNothing()
    {
        _server.IsRunning = false;
        await ConnectAsync();

        _provider.RestoreDevices();
        await _provider.WhenIdleAsync();

        Assert.Equal(1, _server.ConnectCount);
        Assert.Equal(0, _devicesChanged);
    }

    [Fact]
    public async Task DeviceListChanged_ReloadFails_DropsTheConnection()
    {
        await ConnectAsync();
        _server.GetDevicesException = new TimeoutException();

        _server.LastConnection.RaiseDeviceListChanged();
        await _provider.WhenIdleAsync();

        Assert.True(_server.LastConnection.IsDisposed);
        Assert.Empty(await _provider.DiscoverAsync(Ct));
    }

    [Fact]
    public async Task Connect_LoadingDevicesFails_ClosesConnectionAndTriesAgain()
    {
        _server.GetDevicesException = new TimeoutException();
        await ConnectAsync();

        Assert.True(_server.LastConnection.IsDisposed);
        Assert.Equal(0, _devicesChanged);

        _server.GetDevicesException = null;
        await RetryAsync();

        Assert.Single(await _provider.DiscoverAsync(Ct));
    }

    [Fact]
    public async Task ClosedWhileLoading_DropsTheConnectionAfterwards()
    {
        _server.CloseWhileLoadingDevices = true;

        Assert.Empty(await ConnectAsync());

        Assert.True(_server.LastConnection.IsDisposed);
        _server.CloseWhileLoadingDevices = false;
        await RetryAsync();
        Assert.Single(await _provider.DiscoverAsync(Ct));
    }

    [Fact]
    public async Task ClosedBeforeSubscribing_DropsTheConnection()
    {
        _server.CloseBeforeSubscribing = true;

        Assert.Empty(await ConnectAsync());

        Assert.True(_server.LastConnection.IsDisposed);
        Assert.Equal(2, _devicesChanged);
    }

    [Fact]
    public async Task Dispose_ClosesTheConnectionAndStopsTrying()
    {
        await ConnectAsync();
        var connection = _server.LastConnection;

        _provider.Dispose();
        connection.Close();
        _time.Advance(TimeSpan.FromHours(1));
        await _provider.WhenIdleAsync();

        Assert.True(connection.IsDisposed);
        Assert.Equal(1, _server.ConnectCount);
        Assert.Empty(await _provider.DiscoverAsync(Ct));
    }

    [Fact]
    public async Task Dispose_WhileWaitingToRetry_StopsTrying()
    {
        _server.IsRunning = false;
        await ConnectAsync();

        _provider.Dispose();
        _server.IsRunning = true;
        _time.Advance(TimeSpan.FromHours(1));
        await _provider.WhenIdleAsync();

        Assert.Equal(1, _server.ConnectCount);
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
