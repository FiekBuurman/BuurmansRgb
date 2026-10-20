using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine;

internal sealed class EngineHost(
    ISettingsStore settingsStore,
    LightingController lighting,
    EngineIpcServer? ipcServer = null,
    TimeSpan? saveDelay = null,
    SwitchableDeviceProvider? simulatedDevices = null,
    Action? trimMemory = null,
    SettingsSaveEvents? saveEvents = null) : IEngineHost, IDisposable
{
    private LightingSettingsSaver? _saver;

    // The IPC server starts once the lights are on and keeps running until ct is cancelled.
    // trimMemory then runs once, so the memory that only starting up needed goes back to Windows.
    public async Task StartAsync(CancellationToken ct)
    {
        var settings = settingsStore.LoadOrCreate();
        simulatedDevices?.IsEnabled = settings.SimulatedDevices;
        lighting.FramesPerSecond = settings.FramesPerSecond;
        await lighting.DiscoverAsync(ct);
        lighting.Apply(settings.ToLightingState());
        _saver = new LightingSettingsSaver(settingsStore, lighting, settings, saveDelay ?? LightingSettingsSaver.DefaultDelay, saveEvents: saveEvents);
        _ = ipcServer?.RunAsync(ct);
        trimMemory?.Invoke();
    }

    public void SetPaused(bool paused) => lighting.SetPaused(paused);

    public void Dispose() => _saver?.Dispose();
}
