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
        await EventuallyDevices.CountAsync(provider, 1, Timeout);

        // OpenRGB.NET's read loop spins at full CPU on a closed socket until the client is disposed,
        // which adds about a second of CPU per second. Whatever else the process does (tests that
        // ran before, the test host, GC) counts in a window before the drop as well, so only the
        // increase over that baseline tells. The window after the drop starts once the device is
        // gone, so the work of dropping itself does not count.
        var baseline = await CpuInOneSecondAsync();
        server.CloseClients();
        await EventuallyDevices.CountAsync(provider, 0, Timeout);
        var afterDrop = await CpuInOneSecondAsync();

        var increase = afterDrop - baseline;
        Assert.True(
            increase < TimeSpan.FromMilliseconds(500),
            $"Used {afterDrop.TotalMilliseconds} ms CPU after the drop, against {baseline.TotalMilliseconds} ms before.");
    }

    private static async Task<TimeSpan> CpuInOneSecondAsync()
    {
        var before = Process.GetCurrentProcess().TotalProcessorTime;
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        return Process.GetCurrentProcess().TotalProcessorTime - before;
    }
}
