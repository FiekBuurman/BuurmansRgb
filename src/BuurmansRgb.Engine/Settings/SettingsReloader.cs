using System.Diagnostics;

namespace BuurmansRgb.Engine.Settings;

// Applies settings.json again when someone else changed it, such as the user in an editor: a
// moment after the last file event, so a file written in steps, or to another file that is then
// renamed, is read once it is whole. The store tells a change by hand from the engine's own save
// and from a file that does not read (yet); those change nothing. A file that cannot be read just
// then (held by another program) is read again after the delay, a few times. The timer only runs
// while a change waits, so a file that does not change costs nothing.
internal sealed class SettingsReloader : IDisposable
{
    // Reads again of a file that could not be read, after the first one: about two seconds with
    // the default delay, enough for an editor or virus scanner that holds it a moment.
    private const int MaxReadsAgain = 5;

    private readonly ISettingsFileEvents _fileEvents;
    private readonly ISettingsStore _store;
    private readonly Action<EngineSettings> _apply;
    private readonly Action<Action>? _holdSaves;
    private readonly TimeSpan _delay;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();
    private bool _disposed;
    private int _readsAgain;

    // apply gets the settings from the file, on the timer thread. The file is read once at the
    // start too: it may have changed while the engine started, before this listened. holdSaves
    // (LightingSettingsSaver.HoldSaves) runs each read with its apply, so no save of what the
    // engine held before comes between them.
    public SettingsReloader(
        ISettingsFileEvents fileEvents,
        ISettingsStore store,
        Action<EngineSettings> apply,
        TimeSpan delay,
        TimeProvider? timeProvider = null,
        Action<Action>? holdSaves = null)
    {
        _fileEvents = fileEvents;
        _store = store;
        _apply = apply;
        _holdSaves = holdSaves;
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
                _readsAgain = 0;
                _timer.Change(_delay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void Reload()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_holdSaves is null)
            {
                ReloadLocked();
            }
            else
            {
                _holdSaves(ReloadLocked);
            }
        }
    }

    private void ReloadLocked()
    {
        var change = _store.LoadIfChanged();
        if (change.ReadLater && _readsAgain < MaxReadsAgain)
        {
            _readsAgain++;
            _timer.Change(_delay, Timeout.InfiniteTimeSpan);
            return;
        }

        _readsAgain = 0;
        if (change.Settings is not { } settings)
        {
            return;
        }

        Trace.TraceInformation("The settings file changed outside the engine; applying it.");
        try
        {
            _apply(settings);
        }
        catch (Exception exception)
        {
            // A device that failed, say. This runs on a timer thread, where an exception would
            // end the engine; the next change by hand tries again.
            Trace.TraceError($"Applying the settings file changed by hand: {exception}");
        }
    }
}
