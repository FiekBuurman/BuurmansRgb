using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

public sealed class EngineHostTests : IDisposable
{
    private readonly TempDirectory _directory = new();
    private readonly SimulatedDeviceProvider _simulated = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task StartAsync_AppliesSavedStaticColorToSimulatedDevices()
    {
        var store = new JsonSettingsStore(_directory.File("settings.json"));
        store.Save(new EngineSettings { Color = new RgbColor(0, 200, 100), Brightness = 0.5 });
        var host = new EngineHost(store, new LightingController([_simulated]));

        await host.StartAsync(TestContext.Current.CancellationToken);

        await AssertAllSimulatedLedsAsync(new RgbColor(0, 100, 50));
    }

    [Fact]
    public async Task StartAsync_WithoutSettingsFile_AppliesDefaultColor()
    {
        var store = new JsonSettingsStore(_directory.File("settings.json"));
        var host = new EngineHost(store, new LightingController([_simulated]));

        await host.StartAsync(TestContext.Current.CancellationToken);

        var defaults = EngineSettings.CreateDefault();
        await AssertAllSimulatedLedsAsync(defaults.Color.Scale(defaults.Brightness));
    }

    [Fact]
    public void SetPaused_TogglesPauseStateOfLighting()
    {
        var lighting = new LightingController([_simulated]);
        var host = new EngineHost(new JsonSettingsStore(_directory.File("settings.json")), lighting);

        host.SetPaused(true);
        Assert.True(lighting.IsPaused);

        host.SetPaused(false);
        Assert.False(lighting.IsPaused);
    }

    private async Task AssertAllSimulatedLedsAsync(RgbColor expected)
    {
        var devices = await _simulated.DiscoverAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(devices);
        foreach (var device in devices)
        {
            Assert.All(_simulated.GetLeds(device.Id).ToArray(), led => Assert.Equal(expected, led));
        }
    }
}
