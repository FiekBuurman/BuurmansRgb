using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// The provider with the real OpenRGB.NET connection, against the test server: the acceptance
// "stop OpenRGB and start it again" without OpenRGB itself.
public sealed class OpenRgbProviderServerTests
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

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
}
