using System.Diagnostics;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Settings;

// Saves the lighting, and the options that can change while the engine runs, a moment after the
// last change, so dragging a slider in the UI writes the file once instead of on every step. The
// timer only runs while a save is pending.
internal sealed class LightingSettingsSaver : IDisposable
{
    private readonly ISettingsStore _store;
    private readonly ILightingStateSource _lighting;
    private EngineSettings _settings;
    private readonly TimeSpan _delay;
    private readonly SettingsSaveEvents? _saveEvents;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();
    private bool _pending;
    private bool _disposed;

    // startupSettings are the settings the engine started with: the saver keeps their other options,
    // unless ChangeOptions changes them, and replaces the lighting. saveEvents hears about every
    // save, also a failed one.
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
        _pending = true;
        _timer.Change(_delay, Timeout.InfiniteTimeSpan);
    }

    // The listeners hear about the save outside the lock, so a slow one does not hold up the next.
    private void SavePending()
    {
        bool succeeded;
        lock (_gate)
        {
            if (!_pending)
            {
                return;
            }

            _pending = false;
            try
            {
                _store.Save(_settings.WithLighting(_lighting.State));
                succeeded = true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The lights keep working; the change is only lost after a restart.
                Trace.TraceError(exception.ToString());
                succeeded = false;
            }
        }

        _saveEvents?.Raise(succeeded);
    }
}
