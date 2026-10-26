using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// The provider with the real OpenRGB.NET connection, against the test server: the acceptance
// "stop OpenRGB and start it again" without OpenRGB itself.
public sealed class OpenRgbProviderServerTests
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

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

        Assert.True(await provider.RequestRescanAsync(Ct).WaitAsync(TestTimeouts.Long, Ct));

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

    // A server that ignores the request, like OpenRGB before 1.0: the provider connects anew.
    [Fact]
    public async Task Rescan_ServerIgnoresTheRequest_ConnectsAgainAndLoadsTheDevices()
    {
        await using var server = new OpenRgbTestServer { Devices = [Strip], AnswersRescan = false };
        using var provider = new OpenRgbDeviceProvider(server.CreateConnector(), TimeProvider.System, RetryDelay, rescanTimeout: TimeSpan.FromMilliseconds(200));
        await provider.DiscoverAsync(Ct);
        await EventuallyDevices.CountAsync(provider, 1, TestTimeouts.Long);
        server.Devices = [Strip, Headset];

        Assert.True(await provider.RequestRescanAsync(Ct).WaitAsync(TestTimeouts.Long, Ct));

        Assert.Equal(1, server.RescanRequests);
        Assert.Equal(2, (await provider.DiscoverAsync(Ct)).Count);
        Assert.Equal(4, server.ClientNames.Count);
        await Eventually.TrueAsync(() => server.ClientCount == 2, TestTimeouts.Long);
    }
}
