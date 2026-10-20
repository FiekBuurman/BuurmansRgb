using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

internal sealed class FakeEngineConnector : IEngineConnector
{
    public Exception? ConnectException { get; set; }

    public FakeEngineConnection Connection { get; private set; } = new();

    public int ConnectCount { get; private set; }

    public Task<IEngineConnection> ConnectAsync(CancellationToken ct)
    {
        ConnectCount++;
        if (ConnectException is not null)
        {
            return Task.FromException<IEngineConnection>(ConnectException);
        }

        Connection = new FakeEngineConnection();
        return Task.FromResult<IEngineConnection>(Connection);
    }
}

internal sealed class FakeEngineConnection : IEngineConnection
{
    public FakeEngineApi FakeApi { get; } = new();

    public IEngineApi Api => FakeApi;

    public bool IsDisposed { get; private set; }

    public event EventHandler? Disconnected;

    public void Disconnect() => Disconnected?.Invoke(this, EventArgs.Empty);

    public void Dispose() => IsDisposed = true;
}

internal sealed class FakeEngineApi : IEngineApi
{
    public event EventHandler<LightingFrame>? FrameRendered;

    public event EventHandler? DevicesChanged;

    public event EventHandler<SettingsSaveResult>? SettingsSaved;

    public IReadOnlyList<DeviceInfo> Devices { get; set; } =
    [
        TestDevices.Create("strip", "LED strip", DeviceType.LedStrip, 3),
        TestDevices.Create("fan", "Fan", DeviceType.Fan, 2),
    ];

    public Dictionary<string, LightingInfo> Lighting { get; } = new()
    {
        [""] = new(new RgbColor(255, 0, 0), 0.8, EffectIds.Static),
        ["fan"] = new(new RgbColor(0, 0, 255), 0.5, EffectIds.Static),
    };

    public Dictionary<string, RgbColor[]> LedColors { get; } = [];

    public LightingFrame Frame { get; set; } = LightingFrame.Empty;

    public List<string> Calls { get; } = [];

    public Exception? SetException { get; set; }

    public bool HasFrameSubscribers => FrameRendered is not null;

    public void RaiseFrame(LightingFrame frame) => FrameRendered?.Invoke(this, frame);

    public void RaiseDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

    public void RaiseSaved(bool succeeded) => SettingsSaved?.Invoke(this, new SettingsSaveResult(succeeded));

    public Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default) => Task.FromResult(Devices);

    public IReadOnlyList<string> Effects { get; set; } = [EffectIds.Static, EffectIds.PerLed];

    public Task<IReadOnlyList<string>> GetEffectsAsync(CancellationToken ct = default) => Task.FromResult(Effects);

    // When set, GetLightingAsync answers once this task completes, like a slow engine.
    public Task? LightingGate { get; set; }

    public async Task<LightingInfo> GetLightingAsync(string? deviceId, CancellationToken ct = default)
    {
        if (LightingGate is { } gate)
        {
            await gate;
        }

        return Lighting.GetValueOrDefault(deviceId ?? "", Lighting[""]);
    }

    public Task<LightingFrame> GetFrameAsync(CancellationToken ct = default) => Task.FromResult(Frame);

    public Task SetColorAsync(string? deviceId, RgbColor color, CancellationToken ct = default) =>
        Record($"color {deviceId ?? "all"} {color.ToHex()}");

    public Task SetBrightnessAsync(string? deviceId, double brightness, CancellationToken ct = default) =>
        Record(string.Create(CultureInfo.InvariantCulture, $"brightness {deviceId ?? "all"} {brightness:0.00}"));

    public Task SetEffectAsync(string? deviceId, string effectId, CancellationToken ct = default) =>
        Record($"effect {deviceId ?? "all"} {effectId}");

    public Task SetSpeedAsync(string? deviceId, double speed, CancellationToken ct = default) =>
        Record(string.Create(CultureInfo.InvariantCulture, $"speed {deviceId ?? "all"} {speed:0.00}"));

    public Task SetColorLoopAsync(string? deviceId, IReadOnlyList<RgbColor> colors, bool spread, CancellationToken ct = default) =>
        Record($"loop {deviceId ?? "all"} {string.Join(",", colors.Select(color => color.ToHex()))} {(spread ? "spread" : "together")}");

    // Like the engine: a device without stored colors has its own color on every LED.
    public Task<IReadOnlyList<RgbColor>> GetLedColorsAsync(string deviceId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RgbColor>>(
            LedColors.GetValueOrDefault(deviceId)
            ?? [.. Enumerable.Repeat(Lighting.GetValueOrDefault(deviceId, Lighting[""]).Color, Devices.Single(device => device.Id == deviceId).LedCount)]);

    public Task SetLedColorsAsync(string deviceId, int firstLed, IReadOnlyList<RgbColor> colors, CancellationToken ct = default) =>
        Record($"leds {deviceId} {firstLed} {string.Join(",", colors.Select(color => color.ToHex()))}");

    public Task ResizeZoneAsync(string deviceId, int zoneIndex, int ledCount, CancellationToken ct = default) =>
        Record($"resize {deviceId} {zoneIndex} {ledCount}");

    public Task FlashDeviceAsync(string deviceId, CancellationToken ct = default) => Record($"flash {deviceId}");

    public SimulatedDevicesMode SimulatedDevices { get; set; } = SimulatedDevicesMode.Automatic;

    public Task<SimulatedDevicesMode> GetSimulatedDevicesAsync(CancellationToken ct = default) => Task.FromResult(SimulatedDevices);

    public async Task SetSimulatedDevicesAsync(SimulatedDevicesMode mode, CancellationToken ct = default)
    {
        await Record($"simulated {mode}");
        SimulatedDevices = mode;
    }

    // Like the engine: saving adds the name, deleting removes it, in alphabetical order.
    public List<string> Profiles { get; } = [];

    // The lighting a loaded profile shows, by profile name.
    public Dictionary<string, LightingInfo> ProfileLighting { get; } = [];

    public Task<IReadOnlyList<string>> GetProfilesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>([.. Profiles]);

    public async Task SaveProfileAsync(string name, CancellationToken ct = default)
    {
        await Record($"profile save {name}");
        Profiles.Add(name);
        Profiles.Sort(StringComparer.CurrentCultureIgnoreCase);
    }

    public async Task LoadProfileAsync(string name, CancellationToken ct = default)
    {
        await Record($"profile load {name}");
        if (ProfileLighting.TryGetValue(name, out var lighting))
        {
            Lighting.Clear();
            Lighting[""] = lighting;
        }
    }

    public async Task DeleteProfileAsync(string name, CancellationToken ct = default)
    {
        await Record($"profile delete {name}");
        Profiles.Remove(name);
    }

    private Task Record(string call)
    {
        if (SetException is not null)
        {
            return Task.FromException(SetException);
        }

        Calls.Add(call);
        return Task.CompletedTask;
    }
}
