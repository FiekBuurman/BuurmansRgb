using System.Diagnostics;
using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// The provider with the real OpenRGB.NET connection, against the test server: the acceptance
// "stop OpenRGB and start it again" without OpenRGB itself.
public sealed class OpenRgbDeviceProviderServerTests
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    // Small, so a search that takes longer is quick to test.
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromMilliseconds(300);

    private static readonly OpenRgbDeviceData Strip = TestDevices.Create(index: 0, name: "Strip", location: "HID: strip");

    private static readonly OpenRgbDeviceData Headset =
        TestDevices.Create(index: 1, name: "Headset", type: OpenRGB.NET.DeviceType.Headset, location: "HID: headset", ledCount: 2);

    [Fact]
    public async Task ServerRestarts_DevicesComeBackAndTakeColorsAgain()
    {
        var server = new OpenRgbTestServer();
        var port = server.Port;
        using var provider = new OpenRgbDeviceProvider(server.CreateConnector(), TimeProvider.System, RetryDelay);
        try
        {
            await provider.DiscoverAsync(Ct);
            var device = Assert.Single(await EventuallyDevices.CountAsync(provider, 1, TestTimeouts.Long));
            provider.SetLeds(device.Id, [Orange, Orange, Orange]);
            Assert.True(server.Updates.TryTake(out var update, TestTimeouts.Long));
            Assert.Equal([Orange, Orange, Orange], update.Colors);

            await Eventually.TrueAsync(() => provider.Status == new ServerStatus(ServerState.Connected, 1), TestTimeouts.Long);

            await server.DisposeAsync();
            await EventuallyDevices.CountAsync(provider, 0, TestTimeouts.Long);
            await Eventually.TrueAsync(() => provider.Status.State == ServerState.Unreachable, TestTimeouts.Long);

            server = new OpenRgbTestServer(port);
            var restarted = Assert.Single(await EventuallyDevices.CountAsync(provider, 1, TestTimeouts.Long));
            await Eventually.TrueAsync(() => provider.Status == new ServerStatus(ServerState.Connected, 1), TestTimeouts.Long);
            Assert.Equal(device.Id, restarted.Id);
            await Eventually.TrueAsync(() => !server.CustomModeIndexes.IsEmpty, TestTimeouts.Long);
            provider.SetLeds(restarted.Id, [Orange, Orange, Orange]);
            Assert.True(server.Updates.TryTake(out _, TestTimeouts.Long));
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    // The headset that was switched off and on: after "Verversen" it is in the list again and
    // takes colors, over the request the watcher sends.
    [Fact]
    public async Task Rescan_DeviceThatCameBack_IsListedAndTakesColors()
    {
        await using var server = new OpenRgbTestServer { Devices = [Strip] };
        using var provider = new OpenRgbDeviceProvider(server.CreateConnector(), TimeProvider.System, RetryDelay);
        await provider.DiscoverAsync(Ct);
        await EventuallyDevices.CountAsync(provider, 1, TestTimeouts.Long);
        server.DevicesAfterRescan = [Strip, Headset];

        Assert.Equal(RescanOutcome.Rescanned, await provider.RequestRescanAsync(Ct).WaitAsync(TestTimeouts.Long, Ct));

        Assert.Equal(1, server.RescanRequests);

        // Over the update OpenRGB sent, not by connecting anew: still the two clients of the first.
        Assert.Equal(2, server.ClientNames.Count);
        var headset = (await provider.DiscoverAsync(Ct)).Single(device => device.Type == DeviceType.Headset);
        await Eventually.TrueAsync(() => server.CustomModeIndexes.Contains(1), TestTimeouts.Long);
        provider.SetLeds(headset.Id, [Orange, Orange]);
        Assert.True(server.Updates.TryTake(out var update, TestTimeouts.Long));
        Assert.Equal(1, update.Index);
        Assert.Equal([Orange, Orange], update.Colors);
    }

    // OpenRGB that answers only once its search got further, longer than connecting may take: the
    // provider waits on the same connection, gets the whole list, and the answers stay in step.
    [Fact]
    public async Task Rescan_SearchLongerThanTheLoadTimeout_KeepsTheConnectionAndListsEveryDevice()
    {
        await using var server = new OpenRgbTestServer { Devices = [Strip] };
        using var provider = new OpenRgbDeviceProvider(
            server.CreateConnector(), TimeProvider.System, RetryDelay, loadTimeout: LoadTimeout, reloadTimeout: TestTimeouts.Long);
        await provider.DiscoverAsync(Ct);
        await EventuallyDevices.CountAsync(provider, 1, TestTimeouts.Long);
        var searching = new TaskCompletionSource();
        server.ControllerDataAnswered = searching.Task;
        server.DevicesAfterRescan = [Strip, Headset];

        var rescan = provider.RequestRescanAsync(Ct);
        await Eventually.TrueAsync(() => server.ControllerDataRequests == 2, TestTimeouts.Long);
        await Task.Delay(LoadTimeout * 3, Ct);
        Assert.False(rescan.IsCompleted);
        searching.SetResult();

        Assert.Equal(RescanOutcome.Rescanned, await rescan.WaitAsync(TestTimeouts.Long, Ct));
        Assert.Equal(["Strip", "Headset"], (await provider.DiscoverAsync(Ct)).Select(device => device.Name));
        Assert.Equal(new ServerStatus(ServerState.Connected, 2), provider.Status);
        Assert.Equal(2, server.ClientNames.Count);
        Assert.Equal(2, server.ClientCount);

        // The next list is read as the server has it, not from an answer that came late.
        server.Devices = [Strip];
        server.NotifyDeviceListUpdated();
        await EventuallyDevices.CountAsync(provider, 1, TestTimeouts.Long);
        server.Devices = [Strip, Headset];
        server.NotifyDeviceListUpdated();
        var headset = (await EventuallyDevices.CountAsync(provider, 2, TestTimeouts.Long)).Single(device => device.Type == DeviceType.Headset);
        provider.SetLeds(headset.Id, [Orange, Orange]);
        Assert.True(server.Updates.TryTake(out var update, TestTimeouts.Long));
        Assert.Equal(1, update.Index);
        Assert.Equal(2, server.ClientNames.Count);
    }

    // OpenRGB goes away while the provider waits for its list: lost at once, not after the
    // reload timeout, and connected again once it is back.
    [Fact]
    public async Task Reload_ServerGoesAwayWhileWaiting_DropsAtOnceAndConnectsAgain()
    {
        await using var server = new OpenRgbTestServer { Devices = [Strip] };
        using var provider = new OpenRgbDeviceProvider(server.CreateConnector(), TimeProvider.System, RetryDelay, loadTimeout: LoadTimeout);
        await provider.DiscoverAsync(Ct);
        await EventuallyDevices.CountAsync(provider, 1, TestTimeouts.Long);
        var statuses = new ServerStatusRecorder(provider);
        server.ControllerDataAnswered = new TaskCompletionSource().Task;

        server.NotifyDeviceListUpdated();
        await Eventually.TrueAsync(() => server.ControllerDataRequests == 2, TestTimeouts.Long);
        server.ControllerDataAnswered = Task.CompletedTask;
        var stopwatch = Stopwatch.StartNew();
        server.CloseClients();

        await Eventually.TrueAsync(() => statuses.Snapshot().Contains(ServerStatus.Connecting), TestTimeouts.Long);
        Assert.True(stopwatch.Elapsed < OpenRgbDeviceProvider.ReloadTimeout / 3, $"Dropped after {stopwatch.Elapsed}.");
        await EventuallyDevices.CountAsync(provider, 1, TestTimeouts.Long);
        await Eventually.TrueAsync(() => provider.Status == new ServerStatus(ServerState.Connected, 1), TestTimeouts.Long);
    }

    // A server that ignores the request, like OpenRGB before 1.0: the provider connects anew.
    [Fact]
    public async Task Rescan_ServerIgnoresTheRequest_ConnectsAgainAndLoadsTheDevices()
    {
        await using var server = new OpenRgbTestServer { Devices = [Strip], AnswersRescan = false };
        using var provider = new OpenRgbDeviceProvider(server.CreateConnector(), TimeProvider.System, RetryDelay, rescanTimeout: TimeSpan.FromMilliseconds(200));
        await provider.DiscoverAsync(Ct);
        await EventuallyDevices.CountAsync(provider, 1, TestTimeouts.Long);
        server.Devices = [Strip, Headset];

        Assert.Equal(RescanOutcome.Rescanned, await provider.RequestRescanAsync(Ct).WaitAsync(TestTimeouts.Long, Ct));

        await Eventually.TrueAsync(() => server.RescanRequests == 1, TestTimeouts.Long);
        Assert.Equal(2, (await provider.DiscoverAsync(Ct)).Count);

        // The new watcher sends its name without an answer, so the server may not have it yet.
        await Eventually.TrueAsync(() => server.ClientNames.Count == 4, TestTimeouts.Long);
        await Eventually.TrueAsync(() => server.ClientCount == 2, TestTimeouts.Long);
    }
}
