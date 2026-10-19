using System.Diagnostics;
using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// The provider with the real OpenRGB.NET connection, against the test server: the acceptance
// "stop OpenRGB and start it again" without OpenRGB itself.
public sealed class OpenRgbProviderServerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);
    private static readonly RgbColor Orange = new(255, 136, 0);

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
            var device = Assert.Single(await WaitForDevicesAsync(provider, count: 1));
            provider.SetLeds(device.Id, [Orange, Orange, Orange]);
            Assert.True(server.Updates.TryTake(out var update, Timeout));
            Assert.Equal([Orange, Orange, Orange], update.Colors);

            await server.DisposeAsync();
            await WaitForDevicesAsync(provider, count: 0);

            server = new OpenRgbTestServer(port);
            var restarted = Assert.Single(await WaitForDevicesAsync(provider, count: 1));
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

    [Fact]
    public async Task ServerClosesTheConnection_ProviderLetsGoOfIt()
    {
        await using var server = new OpenRgbTestServer();
        using var provider = new OpenRgbDeviceProvider(server.CreateConnector(), TimeProvider.System, TimeSpan.FromHours(1));
        await provider.DiscoverAsync(Ct);
        await WaitForDevicesAsync(provider, count: 1);
        var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;

        server.CloseClients();
        await WaitForDevicesAsync(provider, count: 0);
        await Task.Delay(500, Ct);

        // OpenRGB.NET's read loop spins at full CPU on a closed socket until the client is disposed.
        var cpuUsed = Process.GetCurrentProcess().TotalProcessorTime - cpuBefore;
        Assert.True(cpuUsed < TimeSpan.FromMilliseconds(300), $"Used {cpuUsed.TotalMilliseconds} ms CPU.");
    }

    private static async Task<IReadOnlyList<RgbDevice>> WaitForDevicesAsync(OpenRgbDeviceProvider provider, int count)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var devices = await provider.DiscoverAsync(Ct);
            if (devices.Count == count)
            {
                return devices;
            }

            Assert.True(stopwatch.Elapsed < Timeout, $"Still {devices.Count} devices instead of {count}.");
            await Task.Delay(10, Ct);
        }
    }
}
