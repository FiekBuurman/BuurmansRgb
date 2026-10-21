using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Ipc;

// One instance per connected UI, so disposing it stops the frames for that UI only.
// LightingController checks the input; its ArgumentException reaches the UI as an RPC error.
internal sealed class EngineApi : IEngineApi, IDisposable
{
    private readonly LightingController _lighting;
    private readonly ProfileLibrary _profiles;
    private readonly SettingsSaveEvents? _saveEvents;

    public EngineApi(LightingController lighting, ProfileLibrary profiles, SettingsSaveEvents? saveEvents = null)
    {
        _lighting = lighting;
        _profiles = profiles;
        _saveEvents = saveEvents;
        _lighting.Rendered += OnRendered;
        _lighting.DevicesChanged += OnDevicesChanged;
        _saveEvents?.Saved += OnSaved;
    }

    public event EventHandler<LightingFrame>? FrameRendered;

    public event EventHandler? DevicesChanged;

    public event EventHandler<SettingsSaveResult>? SettingsSaved;

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

    public Task SetSpeedAsync(string? deviceId, double speed, CancellationToken ct = default)
    {
        _lighting.SetLighting(deviceId, lighting => lighting with { Speed = speed });
        return Task.CompletedTask;
    }

    // A copy of the colors, so the caller cannot change the loop afterwards.
    public Task SetColorLoopAsync(string? deviceId, IReadOnlyList<RgbColor> colors, bool spread, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var loop = new ColorLoop([.. colors], spread);
        _lighting.SetLighting(deviceId, lighting => lighting with { Loop = loop });
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

    public Task FlashDeviceAsync(string deviceId, CancellationToken ct = default)
    {
        _lighting.Flash(deviceId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetProfilesAsync(CancellationToken ct = default) => Task.FromResult(_profiles.Names);

    public Task SaveProfileAsync(string name, CancellationToken ct = default)
    {
        _profiles.Save(name);
        return Task.CompletedTask;
    }

    public Task LoadProfileAsync(string name, CancellationToken ct = default)
    {
        _profiles.Load(name);
        return Task.CompletedTask;
    }

    public Task DeleteProfileAsync(string name, CancellationToken ct = default)
    {
        _profiles.Delete(name);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _lighting.Rendered -= OnRendered;
        _lighting.DevicesChanged -= OnDevicesChanged;
        _saveEvents?.Saved -= OnSaved;
    }

    // The wire formats are made here, so the lighting model does not know them, and outside the
    // controller's lock.
    private static LightingInfo ToInfo(DeviceLighting lighting) => new(lighting.Color, lighting.Brightness, lighting.EffectId, lighting.Speed, lighting.Loop);

    private static LightingFrame ToFrame(LightingOutput output) =>
        new(output.Sequence, [.. output.Devices.Select(device => DeviceFrame.FromColors(device.DeviceId, device.Colors))]);

    private void OnDevicesChanged(object? sender, EventArgs e) => DevicesChanged?.Invoke(this, EventArgs.Empty);

    private void OnSaved(object? sender, bool succeeded) => SettingsSaved?.Invoke(this, new SettingsSaveResult(succeeded));

    private void OnRendered(object? sender, EventArgs e)
    {
        if (FrameRendered is { } frameRendered)
        {
            frameRendered(this, ToFrame(_lighting.GetOutput()));
        }
    }
}
