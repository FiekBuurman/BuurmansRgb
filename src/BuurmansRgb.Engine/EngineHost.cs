using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine;

internal sealed class EngineHost(
    ISettingsStore settingsStore,
    LightingController lighting,
    EngineIpcServer? ipcServer = null,
    TimeSpan? saveDelay = null,
    SimulatedDevicesSwitch? simulatedDevices = null,
    Action? trimMemory = null,
    SettingsSaveEvents? saveEvents = null,
    IReadOnlyList<IProfileTrigger>? profileTriggers = null) : IEngineHost, IDisposable
{
    private LightingSettingsSaver? _saver;

    // The IPC server starts once the lights are on and keeps running until ct is cancelled.
    // trimMemory then runs once, so the memory that only starting up needed goes back to Windows.
    // From then on a new mode for the simulated devices is saved like the lighting, and the profile
    // triggers may load profiles: after the saved lighting, so they are not overwritten, and with
    // the saver, so what they load is saved.
    public async Task StartAsync(CancellationToken ct)
    {
        var settings = settingsStore.LoadOrCreate();
        simulatedDevices?.Mode = settings.SimulatedDevices;
        lighting.FramesPerSecond = settings.FramesPerSecond;
        await lighting.DiscoverAsync(ct);
        lighting.Apply(settings.ToLightingState());
        _saver = new LightingSettingsSaver(settingsStore, lighting, settings, saveDelay ?? LightingSettingsSaver.DefaultDelay, saveEvents: saveEvents);
        simulatedDevices?.ModeChanged += OnSimulatedDevicesModeChanged;
        foreach (var trigger in profileTriggers ?? [])
        {
            trigger.Start();
        }

        _ = ipcServer?.RunAsync(ct);
        trimMemory?.Invoke();
    }

    public void SetPaused(bool paused) => lighting.SetPaused(paused);

    public void Dispose()
    {
        simulatedDevices?.ModeChanged -= OnSimulatedDevicesModeChanged;
        foreach (var trigger in profileTriggers ?? [])
        {
            trigger.Dispose();
        }

        _saver?.Dispose();
    }

    // Reads the newest mode, so two changes that cross each other still save the last one.
    private void OnSimulatedDevicesModeChanged(object? sender, EventArgs e) =>
        _saver?.ChangeOptions(settings => settings with { SimulatedDevices = simulatedDevices!.Mode });
}
