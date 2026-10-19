using System.Diagnostics;
using BuurmansRgb.Engine.Lighting;
using Timer = System.Threading.Timer;

namespace BuurmansRgb.Engine.Settings;

// Saves the lighting a moment after the last change, so dragging a slider in the UI writes the
// file once instead of on every step. The timer only runs while a save is pending.
internal sealed class LightingSettingsSaver : IDisposable
{
    private readonly ISettingsStore _store;
    private readonly LightingController _lighting;
    private readonly TimeSpan _delay;
    private readonly Timer _timer;
    private readonly Lock _gate = new();
    private bool _pending;
    private bool _disposed;

    public LightingSettingsSaver(ISettingsStore store, LightingController lighting, TimeSpan delay)
    {
        _store = store;
        _lighting = lighting;
        _delay = delay;
        _timer = new Timer(_ => SavePending(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _lighting.LightingChanged += OnLightingChanged;
    }

    public static TimeSpan DefaultDelay { get; } = TimeSpan.FromSeconds(1);

    // Writes a pending change right away, so nothing is lost when the engine exits.
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

        using (var callbacksDone = new ManualResetEvent(initialState: false))
        {
            if (_timer.Dispose(callbacksDone))
            {
                callbacksDone.WaitOne();
            }
        }

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
