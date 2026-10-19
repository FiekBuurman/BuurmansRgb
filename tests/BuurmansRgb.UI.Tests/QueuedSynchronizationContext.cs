namespace BuurmansRgb.UI.Tests;

// Stands in for the WPF dispatcher: posted work waits until the test runs it.
internal sealed class QueuedSynchronizationContext : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _pending = new();

    public override void Post(SendOrPostCallback d, object? state) => _pending.Enqueue((d, state));

    public void RunPending()
    {
        while (_pending.TryDequeue(out var work))
        {
            work.Callback(work.State);
        }
    }
}
