using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// The provider with the real OpenRGB.NET connection, against the test server: the acceptance
// "stop OpenRGB and start it again" without OpenRGB itself.
public sealed class OpenRgbProviderServerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ServerRestarts_DevicesComeBackAndTakeColorsAgain()
    {
        var server = new OpenRgbTestServer();
        var port = server.Port;
        using var provider = new OpenRgbDeviceProvider(server.CreateConnector(), TimeProvider.System, RetryDelay);
        try
        {
            await provider.DiscoverAsync(Ct);
            var device = Assert.Single(await EventuallyDevices.CountAsync(provider, 1, Timeout));
            provider.SetLeds(device.Id, [Orange, Orange, Orange]);
            Assert.True(server.Updates.TryTake(out var update, Timeout));
            Assert.Equal([Orange, Orange, Orange], update.Colors);

            await server.DisposeAsync();
            await EventuallyDevices.CountAsync(provider, 0, Timeout);

            server = new OpenRgbTestServer(port);
            var restarted = Assert.Single(await EventuallyDevices.CountAsync(provider, 1, Timeout));
            Assert.Equal(device.Id, restarted.Id);
            await Eventually.TrueAsync(() => !server.CustomModeIndexes.IsEmpty, Timeout);
            provider.SetLeds(restarted.Id, [Orange, Orange, Orange]);
            Assert.True(server.Updates.TryTake(out _, Timeout));
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
