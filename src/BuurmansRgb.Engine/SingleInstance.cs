namespace BuurmansRgb.Engine;

internal sealed class SingleInstance : IDisposable
{
    public const string EngineName = "BuurmansRgb.Engine";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private RegisteredWaitHandle? _registration;

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

    public void ListenForActivation(Action onActivated)
    {
        if (!IsPrimary)
        {
            throw new InvalidOperationException("Only the primary instance can listen for activation.");
        }

        // A registered wait uses no thread and no timer while idle.
        _registration ??= ThreadPool.RegisterWaitForSingleObject(
            _activation,
            (_, _) => onActivated(),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activation.Dispose();
        _mutex.Dispose();
    }
}
