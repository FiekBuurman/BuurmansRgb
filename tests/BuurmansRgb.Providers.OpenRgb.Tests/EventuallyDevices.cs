using System.Diagnostics;
using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// Polls a provider until it reports a number of devices, for example after the server started.
internal static class EventuallyDevices
{
    public static async Task<IReadOnlyList<RgbDevice>> CountAsync(IDeviceProvider provider, int count, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var devices = await provider.DiscoverAsync(Ct);
            if (devices.Count == count)
            {
                return devices;
            }

            Assert.True(stopwatch.Elapsed < timeout, $"Still {devices.Count} devices instead of {count}.");
            await Task.Delay(10, Ct);
        }
    }
}