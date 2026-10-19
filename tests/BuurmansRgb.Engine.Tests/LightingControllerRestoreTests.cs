using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerRestoreTests : IDisposable
{
    private static readonly RgbColor Orange = new(255, 136, 0);

    private readonly RestorableDeviceProvider _restorable = new();
    private readonly RecordingDeviceProvider _recording = new();
    private readonly LightingController _controller;

    public LightingControllerRestoreTests() => _controller = new LightingController([_restorable, _recording]);

    public void Dispose() => _controller.Dispose();

    [Fact]
    public async Task RestoreDevices_RestoresTheProviderThenSendsTheColorsAgain()
    {
        await StartAsync();

        _controller.RestoreDevices();

        Assert.Equal(["leds", "restore", "leds"], _restorable.Calls);
    }

    [Fact]
    public async Task RestoreDevices_SendsUnchangedColorsToEveryProvider()
    {
        await StartAsync();

        _controller.RestoreDevices();

        Assert.Equal(2, _recording.SetLedsCount);
        Assert.Equal([Orange, Orange, Orange], _recording.LastColors);
    }

    [Fact]
    public async Task RestoreDevices_RaisesRendered()
    {
        await StartAsync();
        var rendered = 0;
        _controller.Rendered += (_, _) => rendered++;

        _controller.RestoreDevices();

        Assert.Equal(1, rendered);
    }

    [Fact]
    public async Task RestoreDevices_WhilePaused_WaitsForThePauseToEnd()
    {
        await StartAsync();
        _controller.SetPaused(true);

        _controller.RestoreDevices();

        Assert.Equal(["leds"], _restorable.Calls);
        _controller.SetPaused(false);
        Assert.Equal(["leds", "restore", "leds"], _restorable.Calls);
    }

    [Fact]
    public async Task SetPausedFalse_WithoutRestoreWhilePaused_DoesNotRestore()
    {
        await StartAsync();
        _controller.SetPaused(true);

        _controller.SetPaused(false);

        Assert.Equal(["leds", "leds"], _restorable.Calls);
    }

    [Fact]
    public async Task RestoreDevices_ProviderThrows_StillSendsTheColors()
    {
        await StartAsync();
        _restorable.ThrowOnRestore = true;

        _controller.RestoreDevices();

        Assert.Equal(["leds", "restore", "leds"], _restorable.Calls);
        Assert.Equal(2, _recording.SetLedsCount);
    }

    [Fact]
    public async Task RestoreDevices_BeforeAnyLighting_SendsNothing()
    {
        await _controller.DiscoverAsync(TestContext.Current.CancellationToken);

        _controller.RestoreDevices();

        Assert.Equal(["restore"], _restorable.Calls);
        Assert.Equal(0, _recording.SetLedsCount);
    }

    private async Task StartAsync()
    {
        await _controller.DiscoverAsync(TestContext.Current.CancellationToken);
        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Static)));
    }
}
