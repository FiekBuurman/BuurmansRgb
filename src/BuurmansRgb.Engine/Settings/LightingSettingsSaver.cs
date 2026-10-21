using System.Diagnostics;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Settings;

// Saves the lighting, and the options that can change while the engine runs, a moment after the
// last change, so dragging a slider in the UI writes the file once instead of on every step. The
// timer only runs while a save is pending. A save that would write what the file holds already
// writes nothing, and one that finds the file changed by hand waits for SettingsReloader to apply
// that change first: the newest change wins, also when it was made in an editor.
internal sealed class LightingSettingsSaver : IDisposable
{
    // Saves in a row that found the file changed by hand before this saves anyway: without a
    // watcher (one that did not start) nobody reads such a change, and the engine must still save.
    // With one, the reloader reads it well before.
    private const int MaxWaitsForChangeByHand = 5;

    private readonly ISettingsStore _store;
    private readonly ILightingStateSource _lighting;
    private readonly TimeSpan _delay;
    private readonly SettingsSaveEvents? _saveEvents;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();

    // The options besides the lighting that the next save writes.
    private EngineSettings _settings;
    private bool _pending;
    private bool _disposed;

    // What the file holds as far as this saver knows, as JSON: its last save, or a change by hand
    // it adopted. Null when it does not know.
    private byte[]? _inFile;

    // The thread on which Adopt applies a change by hand, or 0; a save that comes due waits for it.
    // A change on another thread meanwhile, such as one from the settings app, is not in the file.
    private int _adoptingThread;
    private bool _changedElsewhere;

    // How many saves in a row found the file changed by hand.
    private int _waitsForChangeByHand;

    // startupSettings are the settings the engine started with: the saver keeps their other
    // options, unless ChangeOptions changes them, and replaces the lighting. saveEvents hears about
    // every save, also a failed one.
    public LightingSettingsSaver(
        ISettingsStore store,
        ILightingStateSource lighting,
        EngineSettings startupSettings,
        TimeSpan delay,
        TimeProvider? timeProvider = null,
        SettingsSaveEvents? saveEvents = null)
    {
        _store = store;
        _lighting = lighting;
        _settings = startupSettings;
        _delay = delay;
        _saveEvents = saveEvents;
        _timer = (timeProvider ?? TimeProvider.System).CreateTimer(
            _ => SavePending(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _lighting.LightingChanged += OnLightingChanged;
    }

    public static TimeSpan DefaultDelay { get; } = TimeSpan.FromSeconds(1);

    // Writes a pending change right away, so nothing is lost when the engine exits. A timer
    // callback that is still running holds the lock, so this waits for it and does not save twice.
    public void Dispose()
    {
        _lighting.LightingChanged -= OnLightingChanged;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _timer.Dispose();
        SavePending();
    }

    // Changes an option besides the lighting, such as the simulated devices, and saves it the same
    // way as a change of the lighting.
    public void ChangeOptions(Func<EngineSettings, EngineSettings> change)
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _settings = change(_settings);
                ScheduleSaveLocked();
            }
        }
    }

    // Runs apply, which puts settings from a file changed by hand on the engine, and takes what the
    // engine holds then as what the file holds: what apply changes comes in through ChangeOptions
    // and LightingChanged as any change does, but is not written back, so the file keeps the user's
    // words and keys the engine does not know. Only what the engine holds otherwise is saved, such
    // as a scene that went off with another lighting (the file still names it in activeScene).
    // apply runs outside the lock; a save that comes due meanwhile waits for it. A change on another
    // thread meanwhile is mixed with the file in what the engine holds, so then the file is written.
    public void Adopt(EngineSettings fromFile, Action apply)
    {
        lock (_gate)
        {
            _adoptingThread = Environment.CurrentManagedThreadId;
            _changedElsewhere = false;
        }

        try
        {
            apply();
        }
        finally
        {
            lock (_gate)
            {
                _adoptingThread = 0;
                _waitsForChangeByHand = 0;
                var held = _settings.WithLighting(_lighting.State);
                _inFile = _changedElsewhere ? null : (held with { ActiveScene = fromFile.ActiveScene }).ToJson();
                if (!_disposed)
                {
                    if (_inFile is not null && held.ToJson().AsSpan().SequenceEqual(_inFile))
                    {
                        _pending = false;
                        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    }
                    else
                    {
                        ScheduleSaveLocked();
                    }
                }
            }
        }
    }

    private void OnLightingChanged(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                ScheduleSaveLocked();
            }
        }
    }

    private void ScheduleSaveLocked()
    {
        _changedElsewhere |= _adoptingThread != 0 && _adoptingThread != Environment.CurrentManagedThreadId;
        _pending = true;
        _timer.Change(_delay, Timeout.InfiniteTimeSpan);
    }

    // The listeners hear about the save outside the lock, so a slow one does not hold up the next.
    private void SavePending()
    {
        bool succeeded;
        lock (_gate)
        {
            if (!_pending || _adoptingThread != 0)
            {
                return;
            }

            _pending = false;
            var settings = _settings.WithLighting(_lighting.State);
            var json = settings.ToJson();
            if (_inFile is not null && json.AsSpan().SequenceEqual(_inFile))
            {
                return;
            }

            try
            {
                if (!_store.SaveIfUnchanged(settings))
                {
                    // At the exit the change by hand goes before this one. Otherwise this tries
                    // again once the reloader applied it, which usually leaves nothing to save.
                    if (_disposed)
                    {
                        return;
                    }

                    if (++_waitsForChangeByHand < MaxWaitsForChangeByHand)
                    {
                        ScheduleSaveLocked();
                        return;
                    }

                    Trace.TraceWarning("Nobody read the settings file changed by hand; the engine saves over it.");
                    _store.Save(settings);
                }

                _waitsForChangeByHand = 0;
                _inFile = json;
                succeeded = true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The lights keep working; the change is only lost after a restart.
                Trace.TraceError(exception.ToString());
                _inFile = null;
                succeeded = false;
            }
        }

        _saveEvents?.Raise(succeeded);
    }
}
