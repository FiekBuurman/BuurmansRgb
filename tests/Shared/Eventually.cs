using System.Diagnostics;

namespace BuurmansRgb.Tests;

// Polls for things that happen on background threads, such as a frame of the render loop or a
// socket that closes.
internal static class Eventually
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public static async Task TrueAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultTimeout;
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(stopwatch.Elapsed < limit, "The condition did not come true in time.");
            await Task.Delay(10, Ct);
        }
    }
}