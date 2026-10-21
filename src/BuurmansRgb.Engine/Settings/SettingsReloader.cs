using System.Diagnostics;

namespace BuurmansRgb.Engine.Settings;

// Applies settings.json again when someone else changed it, such as the user in an editor: a
// moment after the last file event, so a file written in steps, or to another file that is then
// renamed, is read once it is whole. The store tells a change by hand from the engine's own save
// and from a file that does not read (yet); those change nothing. The timer only runs while a
// change waits, so a file that does not change costs nothing.
internal sealed class SettingsReloader : IDisposable
{
    private readonly ISettingsFileEvents _fileEvents;
    private readonly ISettingsStore _store;
    private readonly Action<EngineSettings> _apply;
    private readonly TimeSpan _delay;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();
    private bool _disposed;

    // apply gets the settings from the file, on the timer thread. The file is read once at the
    // start too: it may have changed while the engine started, before this listened.
    public SettingsReloader(
        ISettingsFileEvents fileEvents,
        ISettingsStore store,
        Action<EngineSettings> apply,
        TimeSpan delay,
        TimeProvider? timeProvider = null)
    {
        _fileEvents = fileEvents;
        _store = store;
        _apply = apply;
        _delay = delay;
        _timer = (timeProvider ?? TimeProvider.System).CreateTimer(
            _ => Reload(), state: null, delay, Timeout.InfiniteTimeSpan);
        _fileEvents.Changed += OnFileChanged;
    }

    // Long enough for an editor that writes the file in steps, short enough to feel at once.
    public static TimeSpan DefaultDelay { get; } = TimeSpan.FromMilliseconds(400);

    // A reload that is still running holds the lock, so this waits for it: nothing is applied
    // after the engine started to stop.
    public void Dispose()
    {
        _fileEvents.Changed -= OnFileChanged;
        lock (_gate)
        {
            _disposed = true;
        }

        _timer.Dispose();
    }

    private void OnFileChanged(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _timer.Change(_delay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void Reload()
    {
        lock (_gate)
        {
            if (_disposed || _store.LoadIfChanged() is not { } settings)
            {
                return;
            }

            Trace.TraceInformation("The settings file changed outside the engine; applying it.");
            _apply(settings);
        }
    }
}
