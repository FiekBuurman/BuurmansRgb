using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine;

internal sealed class EngineHost(ISettingsStore settingsStore, LightingController lighting)
{
    public bool IsPaused => lighting.IsPaused;

    public async Task StartAsync(CancellationToken ct)
    {
        var settings = settingsStore.LoadOrCreate();
        await lighting.DiscoverAsync(ct);
        lighting.ApplyStaticColor(settings.Color, settings.Brightness);
    }

    public void SetPaused(bool paused)
    {
        if (paused)
        {
            lighting.Pause();
        }
        else
        {
            lighting.Resume();
        }
    }
}
