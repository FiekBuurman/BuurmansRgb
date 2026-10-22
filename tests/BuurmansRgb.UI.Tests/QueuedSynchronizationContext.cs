namespace BuurmansRgb.UI.Tests;

// Stands in for the WPF dispatcher: posted work waits until the test runs it.
internal sealed class QueuedSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _pending = new();
    private readonly SynchronizationContext? _previous;

    private QueuedSynchronizationContext(SynchronizationContext? previous) => _previous = previous;

    // Makes a new one the context of this thread, as on the UI thread; disposing it puts the
    // previous context back.
    public static QueuedSynchronizationContext Install()
    {
        var context = new QueuedSynchronizationContext(Current);
        SetSynchronizationContext(context);
        return context;
    }

    public override void Post(SendOrPostCallback d, object? state) => _pending.Enqueue((d, state));

    public void RunPending()
    {
        while (_pending.TryDequeue(out var work))
        {
            work.Callback(work.State);
        }
    }

    public void Dispose() => SetSynchronizationContext(_previous);
}
