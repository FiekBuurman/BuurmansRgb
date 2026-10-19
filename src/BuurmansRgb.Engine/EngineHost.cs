using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine;

internal sealed class EngineHost(ISettingsStore settingsStore, LightingController lighting, TimeSpan saveDelay)
    : IEngineHost, IDisposable
{
    private LightingSettingsSaver? _saver;

    public EngineHost(ISettingsStore settingsStore, LightingController lighting)
        : this(settingsStore, lighting, LightingSettingsSaver.DefaultDelay)
    {
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var settings = settingsStore.LoadOrCreate();
        await lighting.DiscoverAsync(ct);
        lighting.Apply(settings.ToLightingState());
        _saver = new LightingSettingsSaver(settingsStore, lighting, saveDelay);
    }

    public void SetPaused(bool paused) => lighting.SetPaused(paused);

    public void Dispose() => _saver?.Dispose();
}
