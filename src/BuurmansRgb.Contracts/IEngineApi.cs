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

    // Raised with the new status when the connection to OpenRGB changes, such as when OpenRGB
    // starts or stops, or when it gives another number of devices.
    event EventHandler<ServerStatus> OpenRgbStatusChanged;

    // Raised with the new reasons when the effects pause or run again: from the tray menu, while
    // Windows is locked or asleep, or after ResumeEffectsAsync.
    event EventHandler<PauseReasons> PauseChanged;

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
    // range. The device may get its new length a little later; DevicesChanged then follows. The
    // engine saves the length and gives it back when the device reports another one, for example
    // after OpenRGB restarted.
    Task ResizeZoneAsync(string deviceId, int zoneIndex, int ledCount, CancellationToken ct = default);

    // Lets the device blink white a few times, so the user can see which one it is; then it shows
    // its own lighting again. Changes no lighting. Best effort: does nothing while effects are
    // paused or for a device the engine does not know (any more).
    Task FlashDeviceAsync(string deviceId, CancellationToken ct = default);

    Task<ServerStatus> GetOpenRgbStatusAsync(CancellationToken ct = default);

    // Why the effects are paused; PauseReasons.None while they run.
    Task<PauseReasons> GetPauseAsync(CancellationToken ct = default);

    // Ends the pause from the tray menu, like unchecking "Effecten pauzeren" there. A pause because
    // Windows is locked or asleep stays until that ends.
    Task ResumeEffectsAsync(CancellationToken ct = default);

    Task<SimulatedDevicesMode> GetSimulatedDevicesAsync(CancellationToken ct = default);

    // Saved like the lighting. When the new mode shows or hides the simulated devices,
    // DevicesChanged follows.
    Task SetSimulatedDevicesAsync(SimulatedDevicesMode mode, CancellationToken ct = default);

    // The names of the saved profiles, in alphabetical order.
    Task<IReadOnlyList<string>> GetProfilesAsync(CancellationToken ct = default);

    // Saves all the lighting as it is now under name, which follows ProfileNames. A profile with
    // that name is replaced.
    Task SaveProfileAsync(string name, CancellationToken ct = default);

    // Shows the lighting of the profile on every device; it becomes the current lighting and is
    // saved as such.
    Task LoadProfileAsync(string name, CancellationToken ct = default);

    Task DeleteProfileAsync(string name, CancellationToken ct = default);

    // When the engine loads the profile by itself; ProfileAutomation.None when it never does.
    Task<ProfileAutomation> GetProfileAutomationAsync(string name, CancellationToken ct = default);

    // Saved with the profile. The time counts in whole minutes and the program follows
    // ProgramNames; an invalid program throws.
    Task SetProfileAutomationAsync(string name, ProfileAutomation automation, CancellationToken ct = default);
}
