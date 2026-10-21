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

    // The thread that runs HoldSaves, or 0; a save that comes due waits for it. A change on another
    // thread meanwhile, such as one from the settings app, is not in the file. _adopted is what
    // Adopt got within HoldSaves.
    private int _holdingThread;
    private bool _changedElsewhere;
    private (EngineSettings FromFile, LightingState Lighting)? _adopted;

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

    // Runs action, which reads settings.json changed by hand and puts it on the engine
    // (SettingsReloader), while no save starts: one that comes due waits for it. A save that took
    // what the engine held before the file was read would otherwise write that over the change.
    // action runs outside the lock. A change on another thread meanwhile, such as one from the
    // settings app, is mixed with the file in what the engine holds; the file is written then.
    public void HoldSaves(Action action)
    {
        lock (_gate)
        {
            _holdingThread = Environment.CurrentManagedThreadId;
            _changedElsewhere = false;
            _adopted = null;
        }

        try
        {
            action();
        }
        finally
        {
            lock (_gate)
            {
                _holdingThread = 0;
                if (_adopted is (var fromFile, var lighting))
                {
                    _adopted = null;
                    AdoptLocked(fromFile, lighting);
                }
                else if (_pending && !_disposed)
                {
                    ScheduleSaveLocked();
                }
            }
        }
    }

    // Takes what the engine holds, once the settings from a file changed by hand are on it, as what
    // the file holds: what that changed came in through ChangeOptions and LightingChanged as any
    // change does, but is not written back, so the file keeps the user's words and keys the engine
    // does not know. Only what the engine holds otherwise is saved, such as a scene that went off
    // with another lighting (the file still names it in activeScene). Within HoldSaves this counts
    // once action is done. lighting is the lighting the engine holds once the file is on it, as the
    // apply left it (LightingController.Replace returns it), not read here: a change on another
    // thread, such as one from the settings app, may be on the engine already while its
    // LightingChanged did not come yet, and taking it as in the file would never save it.
    public void Adopt(EngineSettings fromFile, LightingState lighting)
    {
        lock (_gate)
        {
            if (_holdingThread == Environment.CurrentManagedThreadId)
            {
                _adopted = (fromFile, lighting);
            }
            else
            {
                AdoptLocked(fromFile, lighting);
            }
        }
    }

    private void AdoptLocked(EngineSettings fromFile, LightingState lighting)
    {
        _waitsForChangeByHand = 0;
        _inFile = _changedElsewhere ? null : (_settings.WithLighting(lighting) with { ActiveScene = fromFile.ActiveScene }).ToJson();
        _changedElsewhere = false;
        if (_disposed)
        {
            return;
        }

        // What the engine holds now, with what came in since the apply.
        var held = _settings.WithLighting(_lighting.State).ToJson();
        if (_inFile is not null && held.AsSpan().SequenceEqual(_inFile))
        {
            _pending = false;
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        else
        {
            ScheduleSaveLocked();
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
        _changedElsewhere |= _holdingThread != 0 && _holdingThread != Environment.CurrentManagedThreadId;
        _pending = true;
        _timer.Change(_delay, Timeout.InfiniteTimeSpan);
    }

    // The listeners hear about the save outside the lock, so a slow one does not hold up the next.
    private void SavePending()
    {
        bool succeeded;
        lock (_gate)
        {
            if (!_pending || _holdingThread != 0)
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
