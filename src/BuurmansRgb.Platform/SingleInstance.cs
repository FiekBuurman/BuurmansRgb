using System.Diagnostics;

namespace BuurmansRgb.Platform;

// One instance per Windows session, for the engine and for the settings app: a second start finds
// the first by name, signals it and stops. The signal asks the first one to come to the front, or
// to exit (the engine's --exit). A start can also hand the first a command and wait for its answer
// (the engine's --scene, --all-on and --all-off, see CommandMailbox).
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private readonly EventWaitHandle _exit;
    private readonly CommandMailbox? _commands;
    private SignalListener? _activationListener;
    private SignalListener? _exitListener;

    private SingleInstance(string name, Mutex mutex, EventWaitHandle activation, EventWaitHandle exit, bool isPrimary, bool acceptsCommands)
    {
        _mutex = mutex;
        _activation = activation;
        _exit = exit;
        IsPrimary = isPrimary;

        // Only the first takes commands, and only one that asks for them (the engine, not the
        // settings app), so a sender finds the signal for them only while an instance runs that
        // knows commands.
        if (isPrimary && acceptsCommands)
        {
            _commands = new CommandMailbox(name);
        }
    }

    public bool IsPrimary { get; }

    // acceptsCommands: whether the first one takes commands of CommandMailbox.Send (ListenForCommand).
    public static SingleInstance Acquire(string name, bool acceptsCommands = false)
    {
        var mutex = new Mutex(initiallyOwned: false, MutexName(name), out var createdNew);
        var activation = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, $@"Local\{name}.Activate");
        var exit = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, ExitName(name));
        return new(name, mutex, activation, exit, createdNew, acceptsCommands);
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
    public static SingleInstance AcquireWhenFree(string name, TimeSpan timeout, TimeSpan pollInterval, bool acceptsCommands = false)
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            var instance = Acquire(name, acceptsCommands);
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

    // The same for a command of CommandMailbox.Send: onCommand gets it, on a thread of the pool,
    // and gives the answer. A command that came before this gets its answer as soon as it
    // listens. Only for an instance acquired with acceptsCommands.
    public IDisposable ListenForCommand(Func<string, int> onCommand)
    {
        ArgumentNullException.ThrowIfNull(onCommand);
        if (IsPrimary && _commands is null)
        {
            throw new InvalidOperationException("This instance was not acquired to accept commands.");
        }

        ThrowIfNotPrimary();
        return _commands!.Listen(onCommand);
    }

    public void Dispose()
    {
        _activationListener?.Dispose();
        _exitListener?.Dispose();
        _commands?.Dispose();
        _activation.Dispose();
        _exit.Dispose();
        _mutex.Dispose();
    }

    private static string MutexName(string name) => $@"Local\{name}";

    private static string ExitName(string name) => $@"Local\{name}.Exit";

    private SignalListener Listen(EventWaitHandle signal, ref SignalListener? listener, Action onSignal)
    {
        ThrowIfNotPrimary();
        return SignalListener.Start(signal, ref listener, onSignal);
    }

    private void ThrowIfNotPrimary()
    {
        if (!IsPrimary)
        {
            throw new InvalidOperationException("Only the primary instance can listen for a signal.");
        }
    }
}
