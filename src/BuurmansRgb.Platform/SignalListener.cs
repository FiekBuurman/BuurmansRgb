namespace BuurmansRgb.Platform;

// Runs a callback each time a named signal of SingleInstance or CommandMailbox is set, until it is
// disposed. Dispose stops listening and waits for a callback that is still running.
internal sealed class SignalListener : IDisposable
{
    private readonly RegisteredWaitHandle _registration;
    private int _callbackThreadId;
    private int _disposed;

    private SignalListener(WaitHandle signal, Action onSignal)
    {
        // A registered wait uses no thread and no timer while idle.
        _registration = ThreadPool.RegisterWaitForSingleObject(
            signal,
            (_, _) => Invoke(onSignal),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    // One listener per signal at a time: listener holds the one of before, if any.
    public static SignalListener Start(WaitHandle signal, ref SignalListener? listener, Action onSignal)
    {
        if (listener is { IsDisposed: false })
        {
            throw new InvalidOperationException("This instance is already listening for that signal.");
        }

        listener = new SignalListener(signal, onSignal);
        return listener;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Disposing from inside the callback must not wait for that same callback.
        if (Volatile.Read(ref _callbackThreadId) == Environment.CurrentManagedThreadId)
        {
            _registration.Unregister(null);
            return;
        }

        using var callbacksDone = new ManualResetEvent(initialState: false);
        if (_registration.Unregister(callbacksDone))
        {
            callbacksDone.WaitOne();
        }
    }

    private void Invoke(Action onSignal)
    {
        Volatile.Write(ref _callbackThreadId, Environment.CurrentManagedThreadId);
        try
        {
            onSignal();
        }
        finally
        {
            Volatile.Write(ref _callbackThreadId, 0);
        }
    }
}
