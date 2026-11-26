using System.Diagnostics;

namespace BuurmansRgb.Tests;

// Polls for things that happen on background threads, such as a frame of the render loop or a
// socket that closes.
internal static class Eventually
{
    public static async Task TrueAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TestTimeouts.Short;
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(stopwatch.Elapsed < limit, "The condition did not come true in time.");
            await Task.Delay(10, Ct);
        }
    }

    // Waits until the value has not changed for the quiet time, and returns it: for events that
    // come in a burst of unknown length, such as the file events of one write. A fixed pause would
    // be too short on a slow runner and too long on a fast one.
    public static async Task<int> StableAsync(Func<int> read, TimeSpan? quiet = null, TimeSpan? timeout = null)
    {
        var quietTime = quiet ?? TimeSpan.FromMilliseconds(100);
        var limit = timeout ?? TestTimeouts.Long;
        var stopwatch = Stopwatch.StartNew();
        var value = read();
        var since = Stopwatch.StartNew();
        while (since.Elapsed < quietTime)
        {
            Assert.True(stopwatch.Elapsed < limit, "The value did not stop changing in time.");
            await Task.Delay(10, Ct);
            var now = read();
            if (now != value)
            {
                value = now;
                since.Restart();
            }
        }

        return value;
    }
}