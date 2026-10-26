using BuurmansRgb.Core;
using Microsoft.Extensions.Time.Testing;
using OpenRgbDeviceType = OpenRGB.NET.DeviceType;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// The button "Verversen" on Apparaten: OpenRGB searches again, or the provider connects anew.
public sealed class OpenRgbProviderRescanTests : IDisposable
{
    private static readonly OpenRgbDeviceData Strip = TestDevices.Create(index: 0, name: "Strip", location: "HID: strip");

    // A headset that was switched off and on again: OpenRGB only sees it after a search.
    private static readonly OpenRgbDeviceData Headset =
        TestDevices.Create(index: 1, name: "Headset", type: OpenRgbDeviceType.Headset, location: "HID: headset", ledCount: 2);

    private readonly FakeOpenRgbConnector _connector = new() { Devices = [Strip] };
    private readonly FakeTimeProvider _time = new();
    private readonly OpenRgbDeviceProvider _provider;
    private int _devicesChanged;

    public OpenRgbProviderRescanTests()
    {
        _provider = new OpenRgbDeviceProvider(_connector, _time, OpenRgbDeviceProvider.RetryDelay);
        _provider.DevicesChanged += (_, _) => Interlocked.Increment(ref _devicesChanged);
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Rescan_AsksTheServerAndReloadsItsNewList()
    {
        await ConnectAsync();
        _connector.DevicesAfterRescan = [Strip, Headset];

        Assert.True(await _provider.RequestRescanAsync(Ct));

        Assert.Equal(1, _connector.RescanRequests);
        Assert.Equal(1, _connector.ConnectCount);
        Assert.Equal(2, _devicesChanged);
        Assert.Equal(["Strip", "Headset"], (await _provider.DiscoverAsync(Ct)).Select(device => device.Name));
        Assert.Equal(new ServerStatus(ServerState.Connected, 2), _provider.Status);
    }

    [Fact]
    public async Task Rescan_DeviceThatCameBack_TakesColorsAgain()
    {
        await ConnectAsync();
        _connector.DevicesAfterRescan = [Strip, Headset];

        await _provider.RequestRescanAsync(Ct);
        var headset = (await _provider.DiscoverAsync(Ct)).Single(device => device.Name == "Headset");
        _provider.SetLeds(headset.Id, [Orange, Orange]);

        Assert.Contains(1, _connector.CustomModeIndexes);
        var update = Assert.Single(_connector.Updates);
        Assert.Equal(1, update.Index);
        Assert.Equal([Orange, Orange], update.Colors);
    }

    // OpenRGB before 1.0 does not know the request.
    [Fact]
    public async Task Rescan_NoUpdateInTime_ConnectsAgainAndLoadsTheDevices()
    {
        await ConnectAsync();
        var first = _connector.LastConnection;
        _connector.AnswersRescan = false;
        _connector.Devices = [Strip, Headset];

        Assert.True(await RescanWhileTimePassesAsync());

        Assert.Equal(2, _connector.ConnectCount);
        Assert.True(first.IsDisposed);
        Assert.False(_connector.LastConnection.IsDisposed);
        Assert.Equal(2, (await _provider.DiscoverAsync(Ct)).Count);
        Assert.Equal(new ServerStatus(ServerState.Connected, 2), _provider.Status);
    }

    // The old connection stays until the new one works, so the devices do not leave the list.
    [Fact]
    public async Task Rescan_ConnectingAgain_KeepsTheDevicesInTheListMeanwhile()
    {
        await ConnectAsync();
        _connector.AnswersRescan = false;
        var answered = new TaskCompletionSource();
        _connector.DevicesAnswered = answered.Task;

        var rescan = _provider.RequestRescanAsync(Ct);
        await Eventually.TrueAsync(() =>
        {
            _time.Advance(OpenRgbDeviceProvider.RescanTimeout);
            return _connector.ConnectCount == 2;
        });

        Assert.Single(await _provider.DiscoverAsync(Ct));
        Assert.Equal(1, _devicesChanged);
        answered.SetResult();
        Assert.True(await rescan);
        Assert.Equal(2, _devicesChanged);
    }

    [Fact]
    public async Task Rescan_SendFails_ConnectsAgain()
    {
        await ConnectAsync();
        _connector.LastConnection.FailSends = true;
        _connector.Devices = [Strip, Headset];

        Assert.True(await _provider.RequestRescanAsync(Ct));

        Assert.Equal(2, _connector.ConnectCount);
        Assert.Equal(2, (await _provider.DiscoverAsync(Ct)).Count);
    }

    [Fact]
    public async Task Rescan_ConnectionClosesWhileWaiting_ConnectsAgainAtOnce()
    {
        await ConnectAsync();
        _connector.AnswersRescan = false;

        var rescan = _provider.RequestRescanAsync(Ct);
        await Eventually.TrueAsync(() => _connector.RescanRequests == 1);
        _connector.LastConnection.Close();

        Assert.True(await rescan);
        Assert.Equal(2, _connector.ConnectCount);
        Assert.Single(await _provider.DiscoverAsync(Ct));
    }

    // Without a connection it does not wait for the next try after RetryDelay.
    [Fact]
    public async Task Rescan_WithoutConnection_ConnectsAtOnce()
    {
        _connector.IsRunning = false;
        await ConnectAsync();
        _connector.IsRunning = true;

        Assert.True(await _provider.RequestRescanAsync(Ct));

        Assert.Equal(2, _connector.ConnectCount);
        Assert.Equal(0, _connector.RescanRequests);
        Assert.Single(await _provider.DiscoverAsync(Ct));
        Assert.Equal(new ServerStatus(ServerState.Connected, 1), _provider.Status);
    }

    [Fact]
    public async Task Rescan_ServerDoesNotRun_IsFalseAndTriesAgainLater()
    {
        _connector.IsRunning = false;
        await ConnectAsync();

        Assert.False(await _provider.RequestRescanAsync(Ct));

        Assert.Equal(ServerState.Unreachable, _provider.Status.State);
        _connector.IsRunning = true;
        _time.Advance(OpenRgbDeviceProvider.RetryDelay);
        await _provider.WhenIdleAsync();
        Assert.Single(await _provider.DiscoverAsync(Ct));
    }

    // Connecting anew fails while the old connection still works: that one stays.
    [Fact]
    public async Task Rescan_ConnectingAgainFails_KeepsTheConnection()
    {
        await ConnectAsync();
        var first = _connector.LastConnection;
        _connector.AnswersRescan = false;
        _connector.ConnectException = new InvalidOperationException("Not the protocol");

        Assert.False(await RescanWhileTimePassesAsync());

        Assert.False(first.IsDisposed);
        Assert.Single(await _provider.DiscoverAsync(Ct));
        Assert.Equal(new ServerStatus(ServerState.Connected, 1), _provider.Status);
    }

    [Fact]
    public async Task Rescan_WhileOneIsOnItsWay_JoinsIt()
    {
        await ConnectAsync();
        _connector.AnswersRescan = false;

        var first = _provider.RequestRescanAsync(Ct);
        var second = _provider.RequestRescanAsync(Ct);
        await Eventually.TrueAsync(() =>
        {
            _time.Advance(OpenRgbDeviceProvider.RescanTimeout);
            return first.IsCompleted && second.IsCompleted;
        });

        Assert.True(await first);
        Assert.True(await second);
        Assert.Equal(1, _connector.RescanRequests);
        Assert.Equal(2, _connector.ConnectCount);
    }

    [Fact]
    public async Task Rescan_AfterTheLastOne_AsksAgain()
    {
        await ConnectAsync();

        await _provider.RequestRescanAsync(Ct);
        await _provider.RequestRescanAsync(Ct);

        Assert.Equal(2, _connector.RescanRequests);
    }

    [Fact]
    public async Task Rescan_AfterDispose_IsFalse()
    {
        await ConnectAsync();
        _provider.Dispose();

        Assert.False(await _provider.RequestRescanAsync(Ct));
        Assert.Equal(0, _connector.RescanRequests);
    }

    private async Task ConnectAsync()
    {
        await _provider.DiscoverAsync(Ct);
        await _provider.WhenIdleAsync();
    }

    // The provider waits for the update on the fake clock, which only moves when told.
    private async Task<bool> RescanWhileTimePassesAsync()
    {
        var rescan = _provider.RequestRescanAsync(Ct);
        await Eventually.TrueAsync(() =>
        {
            _time.Advance(OpenRgbDeviceProvider.RescanTimeout);
            return rescan.IsCompleted;
        });
        return await rescan;
    }
}
