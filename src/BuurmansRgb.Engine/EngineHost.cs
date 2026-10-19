using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine;

internal sealed class EngineHost(ISettingsStore settingsStore, LightingController lighting) : IEngineHost
{
    public async Task StartAsync(CancellationToken ct)
    {
        var settings = settingsStore.LoadOrCreate();
        await lighting.DiscoverAsync(ct);
        lighting.Apply(LightingState.Uniform(new DeviceLighting(settings.Color, settings.Brightness, EffectIds.Static)));
    }

    public void SetPaused(bool paused) => lighting.SetPaused(paused);
}
