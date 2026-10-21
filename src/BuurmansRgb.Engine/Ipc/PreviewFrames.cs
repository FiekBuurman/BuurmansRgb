using System.Diagnostics;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Ipc;

// The frames for the preview of one connected settings app, apart from the render loop. A render
// only notes that there is something new (Rendered); a timer on the thread pool then copies the
// latest output and sends it, at most once per MinInterval, so the animation never waits for the
// app, and frames the app could not show anyway are never copied. While the app wants no frames
// (its window is minimized, or its page has no preview), nothing is copied at all; once it wants
// them again, the latest output goes right away.
internal sealed class PreviewFrames : IDisposable
{
    // 20 frames a second: smooth enough for a preview, a third less than the default 30 of the
    // animation.
    public static TimeSpan MinInterval { get; } = TimeSpan.FromMilliseconds(50);

    private readonly Func<LightingOutput> _getOutput;
    private readonly Action<LightingOutput> _send;
    private readonly TimeProvider _time;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();
    private bool _wanted = true;
    private bool _pending;
    private bool _disposed;
    private long? _lastSent;

    public PreviewFrames(Func<LightingOutput> getOutput, Action<LightingOutput> send, TimeProvider time)
    {
        _getOutput = getOutput;
        _send = send;
        _time = time;
        _timer = time.CreateTimer(_ => Send(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    // True until the app says otherwise, so an app that never says still gets its frames.
    public bool Wanted
    {
        get
        {
            lock (_gate)
            {
                return _wanted;
            }
        }

        set
        {
            TimeSpan? due = null;
            lock (_gate)
            {
                if (_wanted == value || _disposed)
                {
                    return;
                }

                _wanted = value;
                if (value)
                {
                    due = ScheduleLocked();
                }
                else
                {
                    // A frame that still waits finds _pending false and sends nothing.
                    _pending = false;
                }
            }

            Start(due);
        }
    }

    // On the thread that rendered, often the timer of the animation: only takes the lock.
    public void Rendered()
    {
        TimeSpan? due = null;
        lock (_gate)
        {
            if (_wanted && !_disposed)
            {
                due = ScheduleLocked();
            }
        }

        Start(due);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _pending = false;
        }

        _timer.Dispose();
    }

    // When to send the next frame, or null when one is on its way already.
    private TimeSpan? ScheduleLocked()
    {
        if (_pending)
        {
            return null;
        }

        _pending = true;
        var wait = _lastSent is { } last ? MinInterval - _time.GetElapsedTime(last) : TimeSpan.Zero;
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    // Outside the lock: a timer that is due now may run Send before Change returns. Send checks
    // again whether the frame is still wanted, so a change in between is harmless.
    private void Start(TimeSpan? due)
    {
        if (due is not { } wait)
        {
            return;
        }

        try
        {
            _timer.Change(wait, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // Disposed meanwhile: no more frames.
        }
    }

    private void Send()
    {
        lock (_gate)
        {
            if (!_pending || !_wanted || _disposed)
            {
                return;
            }

            _pending = false;
            _lastSent = _time.GetTimestamp();
        }

        try
        {
            _send(_getOutput());
        }
        catch (Exception exception)
        {
            // A connection that closes meanwhile; the next frame tries again.
            Trace.TraceError(exception.ToString());
        }
    }
}
