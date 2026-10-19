using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Ipc;

// One instance per connected UI, so disposing it stops the frames for that UI only.
// Invalid input throws ArgumentException, which reaches the UI as an RPC error.
internal sealed class EngineApi : IEngineApi, IDisposable
{
    private readonly LightingController _lighting;

    public EngineApi(LightingController lighting)
    {
        _lighting = lighting;
        _lighting.Rendered += OnRendered;
    }

    public event EventHandler<LightingFrame>? FrameRendered;

    public Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DeviceInfo>>(
            [.. _lighting.Devices.Select(device => new DeviceInfo(device.Id, device.Name, device.Type, device.LedCount))]);

    public Task<IReadOnlyList<string>> GetEffectsAsync(CancellationToken ct = default) => Task.FromResult(EffectCatalog.Ids);

    public Task<LightingInfo> GetLightingAsync(string? deviceId, CancellationToken ct = default)
    {
        var lighting = _lighting.GetLighting(deviceId);
        return Task.FromResult(new LightingInfo(lighting.Color, lighting.Brightness, lighting.EffectId));
    }

    public Task<LightingFrame> GetFrameAsync(CancellationToken ct = default) => Task.FromResult(ToFrame(_lighting.GetOutput()));

    public Task SetColorAsync(string? deviceId, RgbColor color, CancellationToken ct = default)
    {
        _lighting.SetLighting(deviceId, lighting => lighting with { Color = color });
        return Task.CompletedTask;
    }

    public Task SetBrightnessAsync(string? deviceId, double brightness, CancellationToken ct = default)
    {
        if (!(brightness is >= 0.0 and <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(brightness), brightness, "Brightness runs from 0 to 1.");
        }

        _lighting.SetLighting(deviceId, lighting => lighting with { Brightness = brightness });
        return Task.CompletedTask;
    }

    public Task SetEffectAsync(string? deviceId, string effectId, CancellationToken ct = default)
    {
        if (!EffectCatalog.IsKnown(effectId))
        {
            throw new ArgumentException($"Unknown effect '{effectId}'.", nameof(effectId));
        }

        _lighting.SetLighting(deviceId, lighting => lighting with { EffectId = effectId });
        return Task.CompletedTask;
    }

    public void Dispose() => _lighting.Rendered -= OnRendered;

    // The wire format is made here, outside the controller's lock.
    private static LightingFrame ToFrame(LightingOutput output) =>
        new(output.Sequence, [.. output.Devices.Select(device => DeviceFrame.FromColors(device.DeviceId, device.Colors))]);

    private void OnRendered(object? sender, EventArgs e)
    {
        if (FrameRendered is { } frameRendered)
        {
            frameRendered(this, ToFrame(_lighting.GetOutput()));
        }
    }
}
