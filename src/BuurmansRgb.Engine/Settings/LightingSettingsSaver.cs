using System.Diagnostics;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Settings;

// Saves the lighting a moment after the last change, so dragging a slider in the UI writes the
// file once instead of on every step. The timer only runs while a save is pending.
internal sealed class LightingSettingsSaver : IDisposable
{
    private readonly ISettingsStore _store;
    private readonly LightingController _lighting;
    private readonly TimeSpan _delay;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();
    private bool _pending;
    private bool _disposed;

    public LightingSettingsSaver(ISettingsStore store, LightingController lighting, TimeSpan delay, TimeProvider? timeProvider = null)
    {
        _store = store;
        _lighting = lighting;
        _delay = delay;
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

    private void OnLightingChanged(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _pending = true;
            _timer.Change(_delay, Timeout.InfiniteTimeSpan);
        }
    }

    private void SavePending()
    {
        lock (_gate)
        {
            if (!_pending)
            {
                return;
            }

            _pending = false;
            try
            {
                _store.Save(EngineSettings.From(_lighting.State));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The lights keep working; the change is only lost after a restart.
                Trace.TraceError(exception.ToString());
            }
        }
    }
}
