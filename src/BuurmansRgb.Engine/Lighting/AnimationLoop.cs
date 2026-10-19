using System.Diagnostics;

namespace BuurmansRgb.Engine.Lighting;

// Calls renderFrame once per period on a PeriodicTimer, between Start and Stop. Start, Stop and
// Period are not thread-safe: LightingController calls them under its lock. renderFrame runs on
// the thread pool and takes that lock itself.
//
// A frame that throws, for example in a provider or a Rendered listener, does not stop the loop:
// the next frame tries again. Only the first error of a series is traced, so a device that keeps
// failing does not fill the log 30 times a second.
internal sealed class AnimationLoop : IDisposable
{
    private readonly TimeProvider _time;
    private readonly Action _renderFrame;
    private TimeSpan _period;

    // Disposing it ends the running loop.
    private PeriodicTimer? _timer;

    public AnimationLoop(TimeProvider time, TimeSpan period, Action renderFrame)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, TimeSpan.Zero);
        _time = time;
        _period = period;
        _renderFrame = renderFrame;
    }

    public bool IsRunning => _timer is not null;

    // A change also applies to a loop that runs.
    public TimeSpan Period
    {
        get => _period;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _period = value;
            _timer?.Period = value;
        }
    }

    public void Start()
    {
        if (_timer is not null)
        {
            return;
        }

        var timer = new PeriodicTimer(_period, _time);
        _timer = timer;
        _ = RunAsync(timer);
    }

    // A tick that is already on its way may still call renderFrame once.
    public void Stop()
    {
        // Disposing makes the waiting WaitForNextTickAsync return false, which ends the loop.
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();

    private async Task RunAsync(PeriodicTimer timer)
    {
        var failing = false;
        while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            try
            {
                _renderFrame();
                failing = false;
            }
            catch (Exception exception)
            {
                if (!failing)
                {
                    Trace.TraceError(exception.ToString());
                }

                failing = true;
            }
        }
    }
}
