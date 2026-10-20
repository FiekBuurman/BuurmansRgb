using BuurmansRgb.Core;
using PolyType;
using StreamJsonRpc;

namespace BuurmansRgb.Contracts;

// A null deviceId means all devices: it sets the global lighting and drops per-device choices.
[JsonRpcContract]
[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IEngineApi
{
    // Raised after every change to the lights, also while effects are paused.
    event EventHandler<LightingFrame> FrameRendered;

    Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetEffectsAsync(CancellationToken ct = default);

    Task<LightingInfo> GetLightingAsync(string? deviceId, CancellationToken ct = default);

    Task<LightingFrame> GetFrameAsync(CancellationToken ct = default);

    Task SetColorAsync(string? deviceId, RgbColor color, CancellationToken ct = default);

    // Brightness runs from 0 to 1.
    Task SetBrightnessAsync(string? deviceId, double brightness, CancellationToken ct = default);

    Task SetEffectAsync(string? deviceId, string effectId, CancellationToken ct = default);
}
