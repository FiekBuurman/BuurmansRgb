using BuurmansRgb.Core;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

public sealed class OpenRgbProviderStatusTests : IDisposable
{
    private readonly FakeOpenRgbConnector _connector = new();
    private readonly FakeTimeProvider _time = new();
    private readonly OpenRgbDeviceProvider _provider;
    private readonly List<ServerStatus> _changes = [];

    public OpenRgbProviderStatusTests()
    {
        _provider = new OpenRgbDeviceProvider(_connector, _time, OpenRgbDeviceProvider.RetryDelay);
        _provider.StatusChanged += (_, status) =>
        {
            lock (_changes)
            {
                _changes.Add(status);
            }
        };
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void New_IsConnecting()
    {
        Assert.Equal(ServerStatus.Connecting, _provider.Status);
    }

    [Fact]
    public async Task Connected_TellsHowManyDevicesTheServerGave()
    {
        _connector.Devices = [TestDevices.Create(index: 0, name: "Strip"), TestDevices.Create(index: 1, name: "Fan")];

        await ConnectAsync();

        Assert.Equal(new ServerStatus(ServerState.Connected, 2), _provider.Status);
        Assert.Equal([new ServerStatus(ServerState.Connected, 2)], _changes);
    }

    [Fact]
    public async Task ConnectedWithoutDevices_IsConnectedWithNone()
    {
        _connector.Devices = [];

        await ConnectAsync();

        Assert.Equal(new ServerStatus(ServerState.Connected, 0), _provider.Status);
    }

    // The retries every five seconds change nothing, so they raise nothing either.
    [Fact]
    public async Task ServerNotRunning_IsUnreachableOnceWhileTrying()
    {
        _connector.IsRunning = false;
        await ConnectAsync();

        await RetryAsync();
        await RetryAsync();

        Assert.Equal(3, _connector.ConnectCount);
        Assert.Equal([new ServerStatus(ServerState.Unreachable)], _changes);
    }

    [Fact]
    public async Task ServerAnswersButConnectingFails_IsFailed()
    {
        _connector.ConnectException = new TimeoutException("No protocol version.");

        await ConnectAsync();

        Assert.Equal(new ServerStatus(ServerState.Failed), _provider.Status);
    }

    [Fact]
    public async Task LoadingTheDevicesFails_IsFailedUntilTheNextTryWorks()
    {
        _connector.GetDevicesException = new InvalidOperationException("Bad packet.");
        await ConnectAsync();
        Assert.Equal(new ServerStatus(ServerState.Failed), _provider.Status);

        _connector.GetDevicesException = null;
        await RetryAsync();

        Assert.Equal(new ServerStatus(ServerState.Connected, 1), _provider.Status);
    }

    [Fact]
    public async Task ServerStartsLater_GoesFromUnreachableToConnected()
    {
        _connector.IsRunning = false;
        await ConnectAsync();

        _connector.IsRunning = true;
        await RetryAsync();

        Assert.Equal([new ServerStatus(ServerState.Unreachable), new ServerStatus(ServerState.Connected, 1)], _changes);
    }

    [Fact]
    public async Task ServerStops_IsConnectingUntilTheNextTryFindsNothing()
    {
        await ConnectAsync();

        _connector.Stop();
        await _provider.WhenIdleAsync();
        Assert.Equal(ServerStatus.Connecting, _provider.Status);

        await RetryAsync();

        Assert.Equal(
            [new ServerStatus(ServerState.Connected, 1), ServerStatus.Connecting, new ServerStatus(ServerState.Unreachable)],
            _changes);
    }

    [Fact]
    public async Task DeviceListChanged_TellsTheNewNumberOfDevices()
    {
        await ConnectAsync();
        _connector.Devices = [TestDevices.Create(index: 0, name: "Strip"), TestDevices.Create(index: 1, name: "Fan")];

        _connector.LastConnection.RaiseDeviceListChanged();
        await _provider.WhenIdleAsync();

        Assert.Equal([new ServerStatus(ServerState.Connected, 1), new ServerStatus(ServerState.Connected, 2)], _changes);
    }

    [Fact]
    public async Task Reload_SameNumberOfDevices_RaisesNothing()
    {
        await ConnectAsync();

        _provider.RestoreDevices();
        await _provider.WhenIdleAsync();

        Assert.Single(_changes);
    }

    [Fact]
    public async Task Dispose_RaisesNothingMore()
    {
        await ConnectAsync();

        _provider.Dispose();
        _connector.Stop();
        await _provider.WhenIdleAsync();

        Assert.Single(_changes);
    }

    private async Task ConnectAsync()
    {
        await _provider.DiscoverAsync(Ct);
        await _provider.WhenIdleAsync();
    }

    private async Task RetryAsync()
    {
        _time.Advance(OpenRgbDeviceProvider.RetryDelay);
        await _provider.WhenIdleAsync();
    }
}
