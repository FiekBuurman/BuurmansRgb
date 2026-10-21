namespace BuurmansRgb.Contracts;

// One instance per Windows session, for the engine and for the settings app: a second start finds
// the first by name, signals it and stops.
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private ActivationListener? _listener;

    private SingleInstance(Mutex mutex, EventWaitHandle activation, bool isPrimary)
    {
        _mutex = mutex;
        _activation = activation;
        IsPrimary = isPrimary;
    }

    public bool IsPrimary { get; }

    public static SingleInstance Acquire(string name)
    {
        var mutex = new Mutex(initiallyOwned: false, $@"Local\{name}", out var createdNew);
        var activation = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, $@"Local\{name}.Activate");
        return new(mutex, activation, createdNew);
    }

    public void SignalPrimary() => _activation.Set();

    // Dispose the result before whatever the callback uses: it stops listening and waits
    // for a callback that is still running.
    public IDisposable ListenForActivation(Action onActivated)
    {
        if (!IsPrimary)
        {
            throw new InvalidOperationException("Only the primary instance can listen for activation.");
        }

        if (_listener is { IsDisposed: false })
        {
            throw new InvalidOperationException("This instance is already listening for activation.");
        }

        _listener = new ActivationListener(_activation, onActivated);
        return _listener;
    }

    public void Dispose()
    {
        _listener?.Dispose();
        _activation.Dispose();
        _mutex.Dispose();
    }

    private sealed class ActivationListener : IDisposable
    {
        private readonly RegisteredWaitHandle _registration;
        private int _callbackThreadId;
        private int _disposed;

        public ActivationListener(WaitHandle activation, Action onActivated)
        {
            // A registered wait uses no thread and no timer while idle.
            _registration = ThreadPool.RegisterWaitForSingleObject(
                activation,
                (_, _) => Invoke(onActivated),
                state: null,
                Timeout.Infinite,
                executeOnlyOnce: false);
        }

        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

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

        private void Invoke(Action onActivated)
        {
            Volatile.Write(ref _callbackThreadId, Environment.CurrentManagedThreadId);
            try
            {
                onActivated();
            }
            finally
            {
                Volatile.Write(ref _callbackThreadId, 0);
            }
        }
    }
}
