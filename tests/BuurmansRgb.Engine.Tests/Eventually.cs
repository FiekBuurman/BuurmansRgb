using System.Diagnostics;

namespace BuurmansRgb.Engine.Tests;

// Polls for things that happen on background threads, such as a frame of the render loop.
internal static class Eventually
{
    public static async Task TrueAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(5);
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(stopwatch.Elapsed < limit, "The condition did not come true in time.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }
}
