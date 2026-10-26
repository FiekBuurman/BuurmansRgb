namespace BuurmansRgb.Engine.Restart;

// The Windows services and drivers "Herstarten" needs, behind an interface so the tests never touch
// a real one. Looking up works as a normal user; stopping and starting needs admin rights.
internal interface IServiceManager
{
    bool Exists(string name);

    // The name of the first kernel driver whose name or display name contains the text (without
    // regard to case), or null.
    string? FindDriver(string text);

    // Returns once it stopped; throws ServiceControlException when it cannot stop, or not within the
    // timeout. A service that is stopped already is fine.
    void Stop(string name, TimeSpan timeout);

    // Returns once it runs; throws ServiceControlException when it cannot start, or not within the
    // timeout. A service that runs already is fine.
    void Start(string name, TimeSpan timeout);
}

internal enum ServiceControlFailure
{
    // The service does not accept a stop now: a driver that cannot unload, or one in use.
    NotStoppable,

    // Other services that depend on it run.
    DependentsRunning,

    TimedOut,

    Other,
}

internal sealed class ServiceControlException(string service, ServiceControlFailure failure, string message, Exception? innerException = null)
    : Exception($"{service}: {message}", innerException)
{
    public string Service { get; } = service;

    public ServiceControlFailure Failure { get; } = failure;
}
