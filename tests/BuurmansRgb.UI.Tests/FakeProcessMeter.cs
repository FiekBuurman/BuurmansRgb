using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

// A process meter that a test feeds, and that remembers what the app read and released. Never a
// process of this pc.
internal sealed class FakeProcessMeter : IProcessMeter, IDisposable
{
    private const long Megabyte = 1024 * 1024;

    public ProcessReading? Next { get; set; } = new(TimeSpan.FromSeconds(1), 26 * Megabyte);

    public List<int> Reads { get; } = [];

    public bool IsDisposed { get; private set; }

    public int Releases { get; private set; }

    public ProcessReading? Read(int processId)
    {
        lock (Reads)
        {
            Reads.Add(processId);
        }

        return Next;
    }

    public void Release() => Releases++;

    public void Dispose() => IsDisposed = true;
}
