using System.ComponentModel;

namespace BuurmansRgb.Engine.Restart;

// The state of a service in the service control manager (SERVICE_STATUS.dwCurrentState).
internal enum ServiceState : uint
{
    Stopped = 1,
    StartPending = 2,
    StopPending = 3,
    Running = 4,
    ContinuePending = 5,
    PausePending = 6,
    Paused = 7,
}

// One opened service: the calls to the service control manager that stopping and starting need.
internal interface IServiceHandle
{
    // Throws ServiceControlException when the state cannot be read.
    ServiceState QueryState();

    // 0, or the Win32 error of ControlService with SERVICE_CONTROL_STOP.
    int SendStop();

    // 0, or the Win32 error of StartService.
    int SendStart();
}

// Stopping and starting a service and waiting for it, apart from advapi32 so the tests can give
// any state, such as a service that is still stopping. Waits by polling the state, as `sc` and
// ServiceController do: sleep waits one poll interval, time tells how long it took.
internal sealed class ServiceControl(TimeProvider time, Action<TimeSpan> sleep)
{
    public static TimeSpan PollInterval { get; } = TimeSpan.FromMilliseconds(250);

    public static ServiceControl System { get; } = new(TimeProvider.System, Thread.Sleep);

    public void Stop(IServiceHandle service, string name, TimeSpan timeout)
    {
        var begin = time.GetTimestamp();
        var state = service.QueryState();
        if (state == ServiceState.Stopped)
        {
            return;
        }

        if (state != ServiceState.StopPending)
        {
            var error = service.SendStop();
            if (error is not (0 or ErrorServiceNotActive))
            {
                throw Failure(name, error);
            }
        }

        WaitFor(service, name, ServiceState.Stopped, begin, timeout);
    }

    public void Start(IServiceHandle service, string name, TimeSpan timeout)
    {
        var begin = time.GetTimestamp();
        var state = service.QueryState();
        if (state == ServiceState.Running)
        {
            return;
        }

        // Still stopping, such as OpenRGB that did not stop within its time: StartService fails
        // with "already running" then, and the service would stop after all. So it first waits,
        // within the same timeout, until it stopped.
        if (state == ServiceState.StopPending)
        {
            WaitFor(service, name, ServiceState.Stopped, begin, timeout);
            state = ServiceState.Stopped;
        }

        if (state != ServiceState.StartPending)
        {
            var error = service.SendStart();

            // "Already running" also comes for a service that is on its way out; it only runs
            // already when it says so itself.
            if (error == ErrorServiceAlreadyRunning
                && service.QueryState() is not (ServiceState.Running or ServiceState.StartPending))
            {
                throw Failure(name, error);
            }

            if (error is not (0 or ErrorServiceAlreadyRunning))
            {
                throw Failure(name, error);
            }
        }

        WaitFor(service, name, ServiceState.Running, begin, timeout);
    }

    public static ServiceControlException Failure(string name, int error)
    {
        var failure = error switch
        {
            ErrorInvalidServiceControl or ErrorServiceCannotAcceptControl => ServiceControlFailure.NotStoppable,
            ErrorDependentServicesRunning => ServiceControlFailure.DependentsRunning,
            _ => ServiceControlFailure.Other,
        };
        var inner = new Win32Exception(error);
        return new ServiceControlException(name, failure, inner.Message, inner);
    }

    // The Win32 error behind a failure, or 0 when there is none, such as for a timeout.
    public static int Win32Error(Exception exception) => exception switch
    {
        Win32Exception win32 => win32.NativeErrorCode,
        { InnerException: Win32Exception inner } => inner.NativeErrorCode,
        _ => 0,
    };

    private void WaitFor(IServiceHandle service, string name, ServiceState wanted, long begin, TimeSpan timeout)
    {
        while (service.QueryState() != wanted)
        {
            if (time.GetElapsedTime(begin) >= timeout)
            {
                throw new ServiceControlException(name, ServiceControlFailure.TimedOut, $"Not {(wanted == ServiceState.Running ? "running" : "stopped")} within {timeout.TotalSeconds:0} s.");
            }

            sleep(PollInterval);
        }
    }

    private const int ErrorDependentServicesRunning = 1051;
    private const int ErrorInvalidServiceControl = 1052;
    private const int ErrorServiceAlreadyRunning = 1056;
    private const int ErrorServiceCannotAcceptControl = 1061;
    private const int ErrorServiceNotActive = 1062;
}
