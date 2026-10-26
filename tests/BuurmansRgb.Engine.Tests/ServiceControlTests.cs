using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine.Tests;

// Stopping and starting with waiting, against a service in memory whose states the test gives;
// the clock only moves when the control waits a poll interval.
public sealed class ServiceControlTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private readonly ManualClock _clock = new();
    private readonly ServiceControl _control;

    public ServiceControlTests() => _control = new ServiceControl(_clock, _clock.Advance);

    [Fact]
    public void Start_Stopped_StartsAndWaitsUntilItRuns()
    {
        var service = new FakeServiceHandle(ServiceState.Stopped) { AfterStart = [ServiceState.StartPending, ServiceState.Running] };

        _control.Start(service, "OpenRGB", Timeout);

        Assert.Equal(["query", "start", "query", "query"], service.Calls);
    }

    [Fact]
    public void Start_Running_DoesNothing()
    {
        var service = new FakeServiceHandle(ServiceState.Running);

        _control.Start(service, "OpenRGB", Timeout);

        Assert.Equal(["query"], service.Calls);
    }

    // OpenRGB that did not stop within its time is started anyway: it first stops, then starts,
    // so it runs afterwards.
    [Fact]
    public void Start_StillStopping_WaitsUntilStoppedThenStarts()
    {
        var service = new FakeServiceHandle(ServiceState.StopPending, ServiceState.StopPending, ServiceState.StopPending, ServiceState.Stopped)
        {
            AfterStart = [ServiceState.Running],
        };

        _control.Start(service, "OpenRGB", Timeout);

        Assert.Equal(["query", "query", "query", "query", "start", "query"], service.Calls);
    }

    [Fact]
    public void Start_StopsNotWithinTheTimeout_TimesOutWithoutStarting()
    {
        var service = new FakeServiceHandle(ServiceState.StopPending);

        var exception = Assert.Throws<ServiceControlException>(() => _control.Start(service, "OpenRGB", Timeout));

        Assert.Equal(ServiceControlFailure.TimedOut, exception.Failure);
        Assert.DoesNotContain("start", service.Calls);
        Assert.True(_clock.GetElapsedTime(0) >= Timeout);
    }

    // ERROR_SERVICE_ALREADY_RUNNING from a service that is on its way out is no success.
    [Fact]
    public void Start_AlreadyRunningErrorWhileItStops_Fails()
    {
        var service = new FakeServiceHandle(ServiceState.Stopped) { StartError = 1056, AfterStart = [ServiceState.StopPending] };

        var exception = Assert.Throws<ServiceControlException>(() => _control.Start(service, "OpenRGB", Timeout));

        Assert.Equal(ServiceControlFailure.Other, exception.Failure);
        Assert.Equal(1056, ServiceControl.Win32Error(exception));
    }

    [Theory]
    [InlineData((uint)ServiceState.Running)]
    [InlineData((uint)ServiceState.StartPending)]
    public void Start_AlreadyRunningErrorWhileItRuns_IsFine(uint state)
    {
        var service = new FakeServiceHandle(ServiceState.Stopped) { StartError = 1056, AfterStart = [(ServiceState)state, ServiceState.Running] };

        _control.Start(service, "OpenRGB", Timeout);

        Assert.Equal(ServiceState.Running, service.QueryState());
    }

    [Fact]
    public void Start_Fails_ThrowsWithTheError()
    {
        var service = new FakeServiceHandle(ServiceState.Stopped) { StartError = 1058 };

        var exception = Assert.Throws<ServiceControlException>(() => _control.Start(service, "OpenRGB", Timeout));

        Assert.Equal(ServiceControlFailure.Other, exception.Failure);
        Assert.Equal(1058, ServiceControl.Win32Error(exception));
    }

    [Fact]
    public void Stop_Running_StopsAndWaitsUntilStopped()
    {
        var service = new FakeServiceHandle(ServiceState.Running) { AfterStop = [ServiceState.StopPending, ServiceState.Stopped] };

        _control.Stop(service, "PawnIO", Timeout);

        Assert.Equal(["query", "stop", "query", "query"], service.Calls);
    }

    [Fact]
    public void Stop_StillStopping_OnlyWaits()
    {
        var service = new FakeServiceHandle(ServiceState.StopPending, ServiceState.Stopped);

        _control.Stop(service, "PawnIO", Timeout);

        Assert.Equal(["query", "query"], service.Calls);
    }

    [Theory]
    [InlineData(1052, (int)ServiceControlFailure.NotStoppable)]
    [InlineData(1061, (int)ServiceControlFailure.NotStoppable)]
    [InlineData(1051, (int)ServiceControlFailure.DependentsRunning)]
    [InlineData(5, (int)ServiceControlFailure.Other)]
    public void Stop_Refused_ThrowsTheFailure(int error, int failure)
    {
        var service = new FakeServiceHandle(ServiceState.Running) { StopError = error };

        var exception = Assert.Throws<ServiceControlException>(() => _control.Stop(service, "PawnIO", Timeout));

        Assert.Equal((ServiceControlFailure)failure, exception.Failure);
    }

    [Fact]
    public void Stop_NotWithinTheTimeout_TimesOut()
    {
        var service = new FakeServiceHandle(ServiceState.Running) { AfterStop = [ServiceState.StopPending] };

        var exception = Assert.Throws<ServiceControlException>(() => _control.Stop(service, "PawnIO", Timeout));

        Assert.Equal(ServiceControlFailure.TimedOut, exception.Failure);
    }

    // Gives the states in order to each query, and keeps giving the last one.
    private sealed class FakeServiceHandle(params ServiceState[] states) : IServiceHandle
    {
        private readonly Queue<ServiceState> _states = new(states);
        private ServiceState _state = states[0];

        public List<string> Calls { get; } = [];

        public int StopError { get; init; }

        public int StartError { get; init; }

        public ServiceState[] AfterStop { get; init; } = [];

        public ServiceState[] AfterStart { get; init; } = [];

        public ServiceState QueryState()
        {
            Calls.Add("query");
            if (_states.TryDequeue(out var state))
            {
                _state = state;
            }

            return _state;
        }

        public int SendStop()
        {
            Calls.Add("stop");
            Then(StopError == 0 ? AfterStop : []);
            return StopError;
        }

        public int SendStart()
        {
            Calls.Add("start");
            Then(AfterStart);
            return StartError;
        }

        private void Then(ServiceState[] next)
        {
            _states.Clear();
            foreach (var state in next)
            {
                _states.Enqueue(state);
            }
        }
    }
}
