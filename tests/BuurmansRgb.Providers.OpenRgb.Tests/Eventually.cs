using System.Diagnostics;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// Polls for things that happen on background threads, such as a socket that closes.
internal static class Eventually
{
    public static async Task TrueAsync(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(stopwatch.Elapsed < timeout, "The condition did not come true in time.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
