using System.Diagnostics;

namespace BuurmansRgb.Contracts;

// One instance per Windows session, for the engine and for the settings app: a second start finds
// the first by name, signals it and stops. The signal asks the first one to come to the front, or
// to exit (the engine's --exit).
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private readonly EventWaitHandle _exit;
    private SignalListener? _activationListener;
    private SignalListener? _exitListener;

    private SingleInstance(Mutex mutex, EventWaitHandle activation, EventWaitHandle exit, bool isPrimary)
    {
        _mutex = mutex;
        _activation = activation;
        _exit = exit;
        IsPrimary = isPrimary;
    }

    public bool IsPrimary { get; }

    public static SingleInstance Acquire(string name)
    {
        var mutex = new Mutex(initiallyOwned: false, MutexName(name), out var createdNew);
        var activation = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, $@"Local\{name}.Activate");
        var exit = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, ExitName(name));
        return new(mutex, activation, exit, createdNew);
    }

    // Asks a running instance to exit, from outside (the engine's "Herstarten" closing the
    // settings app). Only opens what is there: unlike Acquire it never creates the mutex, so an
    // instance that starts meanwhile does not take it for a second start. False when none runs.
    public static bool SignalExitIfRunning(string name)
    {
        if (!Mutex.TryOpenExisting(MutexName(name), out var mutex))
        {
            return false;
        }

        using (mutex)
        {
            // Named events are Windows only, as the apps are.
            if (!OperatingSystem.IsWindows() || !EventWaitHandle.TryOpenExisting(ExitName(name), out var exit))
            {
                return false;
            }

            using (exit)
            {
                exit.Set();
                return true;
            }
        }
    }

    // For an instance that starts in place of one that is on its way out (the engine's
    // "Herstarten"): tries again until the other one is gone, at most for timeout. Not primary
    // when the other one is still there then. The mutex is gone once no process holds it, so a
    // second start meanwhile (or a --exit) only makes this wait a moment longer.
    public static SingleInstance AcquireWhenFree(string name, TimeSpan timeout, TimeSpan pollInterval)
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            var instance = Acquire(name);
            if (instance.IsPrimary || waited.Elapsed >= timeout)
            {
                return instance;
            }

            instance.Dispose();
            Thread.Sleep(pollInterval);
        }
    }

    public void SignalPrimary() => _activation.Set();

    public void SignalPrimaryToExit() => _exit.Set();

    // Dispose the result before whatever the callback uses: it stops listening and waits
    // for a callback that is still running.
    public IDisposable ListenForActivation(Action onActivated) => Listen(_activation, ref _activationListener, onActivated);

    // The same for the request to exit.
    public IDisposable ListenForExit(Action onExit) => Listen(_exit, ref _exitListener, onExit);

    public void Dispose()
    {
        _activationListener?.Dispose();
        _exitListener?.Dispose();
        _activation.Dispose();
        _exit.Dispose();
        _mutex.Dispose();
    }

    private static string MutexName(string name) => $@"Local\{name}";

    private static string ExitName(string name) => $@"Local\{name}.Exit";

    private SignalListener Listen(EventWaitHandle signal, ref SignalListener? listener, Action onSignal)
    {
        if (!IsPrimary)
        {
            throw new InvalidOperationException("Only the primary instance can listen for a signal.");
        }

        if (listener is { IsDisposed: false })
        {
            throw new InvalidOperationException("This instance is already listening for that signal.");
        }

        listener = new SignalListener(signal, onSignal);
        return listener;
    }

    private sealed class SignalListener : IDisposable
    {
        private readonly RegisteredWaitHandle _registration;
        private int _callbackThreadId;
        private int _disposed;

        public SignalListener(WaitHandle signal, Action onSignal)
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
}
