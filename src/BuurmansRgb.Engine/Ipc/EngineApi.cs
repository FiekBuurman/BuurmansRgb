using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Ipc;

// One instance per connected UI, so disposing it stops the frames for that UI only.
// LightingController checks the input; its ArgumentException reaches the UI as an RPC error.
internal sealed class EngineApi : IEngineApi, IDisposable
{
    private readonly LightingController _lighting;

    public EngineApi(LightingController lighting)
    {
        _lighting = lighting;
        _lighting.Rendered += OnRendered;
        _lighting.DevicesChanged += OnDevicesChanged;
    }

    public event EventHandler<LightingFrame>? FrameRendered;

    public event EventHandler? DevicesChanged;

    public Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DeviceInfo>>([.. _lighting.Devices.Select(DeviceInfo.From)]);

    public Task<IReadOnlyList<string>> GetEffectsAsync(CancellationToken ct = default) => Task.FromResult(EffectCatalog.Ids);

    public Task<LightingInfo> GetLightingAsync(string? deviceId, CancellationToken ct = default) =>
        Task.FromResult(ToInfo(_lighting.GetLighting(deviceId)));

    public Task<LightingFrame> GetFrameAsync(CancellationToken ct = default) => Task.FromResult(ToFrame(_lighting.GetOutput()));

    public Task SetColorAsync(string? deviceId, RgbColor color, CancellationToken ct = default)
    {
        _lighting.SetLighting(deviceId, lighting => lighting with { Color = color });
        return Task.CompletedTask;
    }

    public Task SetBrightnessAsync(string? deviceId, double brightness, CancellationToken ct = default)
    {
        _lighting.SetLighting(deviceId, lighting => lighting with { Brightness = brightness });
        return Task.CompletedTask;
    }

    public Task SetEffectAsync(string? deviceId, string effectId, CancellationToken ct = default)
    {
        _lighting.SetLighting(deviceId, lighting => lighting with { EffectId = effectId });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RgbColor>> GetLedColorsAsync(string deviceId, CancellationToken ct = default) =>
        Task.FromResult(_lighting.GetLedColors(deviceId));

    public Task SetLedColorsAsync(string deviceId, int firstLed, IReadOnlyList<RgbColor> colors, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(colors);
        _lighting.SetLedColors(deviceId, firstLed, colors);
        return Task.CompletedTask;
    }

    public Task ResizeZoneAsync(string deviceId, int zoneIndex, int ledCount, CancellationToken ct = default)
    {
        _lighting.ResizeZone(deviceId, zoneIndex, ledCount);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _lighting.Rendered -= OnRendered;
        _lighting.DevicesChanged -= OnDevicesChanged;
    }

    // The wire formats are made here, so the lighting model does not know them, and outside the
    // controller's lock.
    private static LightingInfo ToInfo(DeviceLighting lighting) => new(lighting.Color, lighting.Brightness, lighting.EffectId);

    private static LightingFrame ToFrame(LightingOutput output) =>
        new(output.Sequence, [.. output.Devices.Select(device => DeviceFrame.FromColors(device.DeviceId, device.Colors))]);

    private void OnDevicesChanged(object? sender, EventArgs e) => DevicesChanged?.Invoke(this, EventArgs.Empty);

    private void OnRendered(object? sender, EventArgs e)
    {
        if (FrameRendered is { } frameRendered)
        {
            frameRendered(this, ToFrame(_lighting.GetOutput()));
        }
    }
}
