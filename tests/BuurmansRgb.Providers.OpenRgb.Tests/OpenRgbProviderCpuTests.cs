using System.Diagnostics;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// Measures the CPU time of the whole process, so no other test may run at the same time.
[CollectionDefinition(nameof(OpenRgbProviderCpuTests), DisableParallelization = true)]
public sealed class OpenRgbProviderCpuTestsCollection;

[Collection(nameof(OpenRgbProviderCpuTests))]
public sealed class OpenRgbProviderCpuTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ServerClosesTheConnection_ProviderLetsGoOfIt()
    {
        await using var server = new OpenRgbTestServer();
        using var provider = new OpenRgbDeviceProvider(server.CreateConnector(), TimeProvider.System, TimeSpan.FromHours(1));
        await provider.DiscoverAsync(TestContext.Current.CancellationToken);
        await Eventually.DeviceCountAsync(provider, 1, Timeout);
        var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;

        server.CloseClients();
        await Eventually.DeviceCountAsync(provider, 0, Timeout);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        // OpenRGB.NET's read loop spins at full CPU on a closed socket until the client is disposed.
        var cpuUsed = Process.GetCurrentProcess().TotalProcessorTime - cpuBefore;
        Assert.True(cpuUsed < TimeSpan.FromMilliseconds(300), $"Used {cpuUsed.TotalMilliseconds} ms CPU.");
    }
}
