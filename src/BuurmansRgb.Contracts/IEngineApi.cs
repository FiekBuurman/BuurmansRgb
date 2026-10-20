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

    // Raised when devices come or go, for example when OpenRGB starts or stops. GetDevicesAsync
    // then returns the new list.
    event EventHandler DevicesChanged;

    // Raised when the engine has written its settings file, a moment after the last change.
    event EventHandler<SettingsSaveResult> SettingsSaved;

    Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetEffectsAsync(CancellationToken ct = default);

    Task<LightingInfo> GetLightingAsync(string? deviceId, CancellationToken ct = default);

    Task<LightingFrame> GetFrameAsync(CancellationToken ct = default);

    Task SetColorAsync(string? deviceId, RgbColor color, CancellationToken ct = default);

    // Brightness runs from 0 to 1.
    Task SetBrightnessAsync(string? deviceId, double brightness, CancellationToken ct = default);

    Task SetEffectAsync(string? deviceId, string effectId, CancellationToken ct = default);

    // Speed runs from EffectSpeed.Min to EffectSpeed.Max; it only shows on animated effects.
    Task SetSpeedAsync(string? deviceId, double speed, CancellationToken ct = default);

    // The colors of the color-loop effect, ColorLoop.MinColors to ColorLoop.MaxColors of them,
    // and whether they spread over the LEDs. Does not switch the effect.
    Task SetColorLoopAsync(string? deviceId, IReadOnlyList<RgbColor> colors, bool spread, CancellationToken ct = default);

    // The colors of the per-LED effect, one per LED: the stored color, or the device color for an
    // LED without one.
    Task<IReadOnlyList<RgbColor>> GetLedColorsAsync(string deviceId, CancellationToken ct = default);

    // Stores colors for the LEDs from firstLed on and switches the device to the per-LED effect.
    // The other LEDs keep following the device color.
    Task SetLedColorsAsync(string deviceId, int firstLed, IReadOnlyList<RgbColor> colors, CancellationToken ct = default);

    // Gives a resizable zone (an index into the device's zones) another number of LEDs, within its
    // range. The device may get its new length a little later; DevicesChanged then follows.
    Task ResizeZoneAsync(string deviceId, int zoneIndex, int ledCount, CancellationToken ct = default);
}
