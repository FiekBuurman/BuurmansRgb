using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine;

internal sealed class EngineHost(
    ISettingsStore settingsStore,
    LightingController lighting,
    EngineIpcServer? ipcServer = null,
    TimeSpan? saveDelay = null) : IEngineHost, IDisposable
{
    private LightingSettingsSaver? _saver;

    // The IPC server starts once the lights are on and keeps running until ct is cancelled.
    public async Task StartAsync(CancellationToken ct)
    {
        var settings = settingsStore.LoadOrCreate();
        await lighting.DiscoverAsync(ct);
        lighting.Apply(settings.ToLightingState());
        _saver = new LightingSettingsSaver(settingsStore, lighting, saveDelay ?? LightingSettingsSaver.DefaultDelay);
        _ = ipcServer?.RunAsync(ct);
    }

    public void SetPaused(bool paused) => lighting.SetPaused(paused);

    public void Dispose() => _saver?.Dispose();
}
