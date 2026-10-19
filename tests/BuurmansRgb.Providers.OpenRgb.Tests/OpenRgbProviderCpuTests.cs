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

        server.CloseClients();
        await Eventually.DeviceCountAsync(provider, 0, Timeout);

        // OpenRGB.NET's read loop spins at full CPU on a closed socket until the client is disposed.
        // Measured after the drop, so the work of dropping itself and of tests that ran before
        // (sockets that still close, JIT) does not count; a spinning loop would use about a second.
        // On a busy pc the rest of the process already uses about 300 ms, so the limit is halfway.
        var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        var cpuUsed = Process.GetCurrentProcess().TotalProcessorTime - cpuBefore;
        Assert.True(cpuUsed < TimeSpan.FromMilliseconds(500), $"Used {cpuUsed.TotalMilliseconds} ms CPU.");
    }
}
