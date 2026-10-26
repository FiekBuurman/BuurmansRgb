using BuurmansRgb.Core;
using PolyType;
using StreamJsonRpc;

namespace BuurmansRgb.Contracts;

// A null deviceId means all devices: it sets the global lighting and drops per-device choices.
[JsonRpcContract]
[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IEngineApi
{
    // Raised after a change to the lights, also while effects are paused: at most 20 times a second,
    // with the latest frame, and not while the app wants no frames (SetFramesWantedAsync).
    event EventHandler<LightingFrame> FrameRendered;

    // Raised when devices come or go, for example when OpenRGB starts or stops, or when one got
    // another zone length or ZonesAlike. GetDevicesAsync then returns the new list.
    event EventHandler DevicesChanged;

    // Raised when the engine has written its settings file, a moment after the last change.
    event EventHandler<SettingsSaveResult> SettingsSaved;

    // Raised with the new status when the connection to OpenRGB changes, such as when OpenRGB
    // starts or stops, or when it gives another number of devices.
    event EventHandler<ServerStatus> OpenRgbStatusChanged;

    // Raised with the new reasons when the effects pause or run again: from the tray menu, while
    // Windows is locked or asleep, while the screen is off or the screen saver runs, or after
    // ResumeEffectsAsync.
    event EventHandler<PauseReasons> PauseChanged;

    // Raised when the lighting changed: from this or another settings app, or from the engine
    // itself, such as a profile that loads on time or from the tray menu. GetLightingAsync then
    // gives the new lighting.
    event EventHandler LightingChanged;

    // Raised when a profile was saved or deleted, or got another automation: from this or another
    // settings app. GetProfilesAsync and GetProfileAutomationAsync then give the new state.
    event EventHandler ProfilesChanged;

    // Raised when an option of the engine changed: the simulated devices, the frame rate, the
    // pause on lock and sleep and what the lamps do during that pause, the catch-up of scene
    // times, the hotkeys or the own colors of the toolbox, from this or another settings app.
    event EventHandler OptionsChanged;

    // Raised with the new state when a count of LEDs (StartZoneCountAsync) starts, goes to another
    // LED, pauses, goes on, gets another pace or ends: ZoneCountInfo.None then. Raised at most once
    // per LED, so at the pace of the count. A state can arrive after a newer one; drop it by its
    // Sequence.
    event EventHandler<ZoneCountInfo> ZoneCountChanged;

    // Raised when an own effect was saved or deleted, from this or another settings app.
    // GetCustomEffectsAsync then gives the new list.
    event EventHandler CustomEffectsChanged;

    Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default);

    // The effects of the engine itself; the own effects of the user come from GetCustomEffectsAsync.
    Task<IReadOnlyList<string>> GetEffectsAsync(CancellationToken ct = default);

    // The own effects of the user (CustomEffect), in alphabetical order of their names. The Id of
    // each is its effect id, for SetEffectAsync and in the lighting and the scenes.
    Task<IReadOnlyList<CustomEffect>> GetCustomEffectsAsync(CancellationToken ct = default);

    // Saves an own effect, which the engine keeps in effects.json: with an empty Id as a new effect,
    // which gets an id of its own, or else in place of the effect with that Id. Returns it as the
    // engine keeps it (CustomEffects.Normalize). The devices that show it show its new steps at
    // once. Throws for an invalid effect, a name another effect has, more than
    // CustomEffects.MaxEffects or an Id the engine does not know (any more).
    Task<CustomEffect> SaveCustomEffectAsync(CustomEffect effect, CancellationToken ct = default);

    // Deletes an own effect. Every device that shows it, and the lighting of all devices, gets the
    // static effect in its own color, as a change (LightingChanged); a scene that names it keeps the
    // id and shows static when it loads. Throws for an id the engine does not know (any more).
    Task DeleteCustomEffectAsync(string id, CancellationToken ct = default);

    Task<LightingInfo> GetLightingAsync(string? deviceId, CancellationToken ct = default);

    // The lighting of every device the engine knows now, taken at one moment: one call for a list of
    // lamps instead of one per device, and no device that went away halfway.
    Task<IReadOnlyList<DeviceLightingInfo>> GetAllLightingAsync(CancellationToken ct = default);

    Task<LightingFrame> GetFrameAsync(CancellationToken ct = default);

    // False while the app shows no preview (minimized, or a page without one): FrameRendered stops,
    // and nothing is copied for it. True again sends the latest frame right away. True until said.
    Task SetFramesWantedAsync(bool wanted, CancellationToken ct = default);

    // Only these devices go into the frames of FrameRendered, such as the one lamp that Kleur
    // shows; null for all of them, which it is until said. GetFrameAsync always gives all.
    Task SetFrameDevicesAsync(IReadOnlyList<string>? deviceIds, CancellationToken ct = default);

    Task SetColorAsync(string? deviceId, RgbColor color, CancellationToken ct = default);

    // Brightness runs from 0 to 1.
    Task SetBrightnessAsync(string? deviceId, double brightness, CancellationToken ct = default);

    Task SetEffectAsync(string? deviceId, string effectId, CancellationToken ct = default);

    // Switched off, a device shows black and keeps its lighting for when it goes on again. For all
    // devices it switches every device.
    Task SetOnAsync(string? deviceId, bool on, CancellationToken ct = default);

    // Speed runs from EffectSpeed.Min to EffectSpeed.Max; it only shows on animated effects.
    Task SetSpeedAsync(string? deviceId, double speed, CancellationToken ct = default);

    // The direction of the effects that run over the LEDs (EffectDirection.Applies), in degrees
    // from EffectDirection.Min to EffectDirection.Max; null runs them along the LEDs in their
    // order again. The other effects keep it for when they switch to such an effect.
    Task SetDirectionAsync(string? deviceId, int? degrees, CancellationToken ct = default);

    // The colors of the color-loop effect, ColorLoop.MinColors to ColorLoop.MaxColors of them,
    // and whether they spread over the LEDs. Does not switch the effect.
    Task SetColorLoopAsync(string? deviceId, IReadOnlyList<RgbColor> colors, bool spread, CancellationToken ct = default);

    // The own colors of an effect that takes a list of them (EffectColors.Takes: twinkle, comet,
    // scanner and fire), EffectColors.MinColors to EffectColors.MaxColors of them; the lists of the
    // other effects stay. Does not switch the effect.
    Task SetEffectColorsAsync(string? deviceId, string effectId, IReadOnlyList<RgbColor> colors, CancellationToken ct = default);

    // Removes the own colors of an effect that takes a list of them, so it has its default again
    // (EffectColors.Default): the lamp color, or the warm colors of the fire. The lists of the
    // other effects stay. Does not switch the effect.
    Task ClearEffectColorsAsync(string? deviceId, string effectId, CancellationToken ct = default);

    // The colors of the per-LED effect, one per LED: the stored color, or the device color for an
    // LED without one.
    Task<IReadOnlyList<RgbColor>> GetLedColorsAsync(string deviceId, CancellationToken ct = default);

    // Stores colors for the LEDs from firstLed on and switches the device to the per-LED effect.
    // The other LEDs keep following the device color.
    Task SetLedColorsAsync(string deviceId, int firstLed, IReadOnlyList<RgbColor> colors, CancellationToken ct = default);

    // Gives a resizable zone (an index into the device's zones) another number of LEDs, within its
    // range. The device may get its new length a little later; DevicesChanged then follows. The
    // engine saves the length and gives it back when the device reports another one, for example
    // after OpenRGB restarted. A count of the zone's LEDs ends with this length.
    Task ResizeZoneAsync(string deviceId, int zoneIndex, int ledCount, CancellationToken ct = default);

    // Whether every zone of the device shows the image the effect renders over its longest zone,
    // a shorter zone stretched (DeviceInfo.ZonesAlike); not with the per-LED effect. Saved like
    // the lighting, but no part of it: a scene leaves it as it is. DevicesChanged follows.
    Task SetZonesAlikeAsync(string deviceId, bool alike, CancellationToken ct = default);

    // Lets the device blink white a few times, so the user can see which one it is; then it shows
    // its own lighting again. Changes no lighting. One device flashes at a time: a new flash
    // replaces the one before, which ends with its own lighting; a device without LEDs has nothing
    // to blink, but still ends it. Best effort: does nothing while effects are paused or for a
    // device the engine does not know (any more).
    Task FlashDeviceAsync(string deviceId, CancellationToken ct = default);

    // The same for the LEDs of one zone (an index into the device's zones) only, such as one
    // header of a motherboard; the rest of the device keeps its colors. A new flash of a device or
    // zone replaces the one before, also when the zone has no LEDs. Also does nothing for a zone
    // the engine does not know (any more).
    Task FlashZoneAsync(string deviceId, int zoneIndex, CancellationToken ct = default);

    // Counts the LEDs of a resizable zone (an index into the device's zones), so the user can see
    // how many a strip or fan on an addressable header has: the zone gets its longest length for a
    // while and its LEDs burn one more at a time, from LED 1 on, ledsPerSecond a second (1 to
    // ZoneCountInfo.MaxLedsPerSecond); the newest is green, the ones before it white, and the rest
    // of the device keeps its lighting. One zone counts at a time: another zone stops the count
    // before, which gets its own length back. The zone gets its own length back with
    // StopZoneCountAsync and when this settings app goes, the effects pause, the devices are searched
    // again (RescanDevicesAsync) or the engine stops; ResizeZoneAsync of the zone ends the count
    // with that length instead. A new device list that has another length for the zone, or not the
    // device, ends it without a length. Nothing starts while the effects pause. ZoneCountChanged
    // tells how it goes. Changes no lighting; a zone whose length cannot be set throws.
    Task StartZoneCountAsync(string deviceId, int zoneIndex, int ledsPerSecond, CancellationToken ct = default);

    // Pauses the count, goes on, goes to another LED or takes another pace; nothing for a zone
    // that is not counted (any more).
    Task SetZoneCountAsync(string deviceId, int zoneIndex, ZoneCountChange change, CancellationToken ct = default);

    // Ends the count of the zone and gives it its own length back; nothing for another zone.
    Task StopZoneCountAsync(string deviceId, int zoneIndex, CancellationToken ct = default);

    // The count as it is now; ZoneCountInfo.None while no zone is counted.
    Task<ZoneCountInfo> GetZoneCountAsync(CancellationToken ct = default);

    // Lets OpenRGB, and the simulated devices, search for devices again, for example when a device
    // that was switched off and on does not come back by itself. Completes after the first new
    // list, so DevicesChanged has gone out by then, though a client may handle it just after the
    // answer; OpenRGB may fill its list step by step while it searches, and the devices it finds
    // later follow through DevicesChanged. That can take some seconds, as the engine connects to
    // OpenRGB anew when OpenRGB does not answer the request in time. A device that came back gets
    // its own lighting. False when OpenRGB could not be reached.
    Task<bool> RescanDevicesAsync(CancellationToken ct = default);

    Task<ServerStatus> GetOpenRgbStatusAsync(CancellationToken ct = default);

    // Why the effects are paused; PauseReasons.None while they run.
    Task<PauseReasons> GetPauseAsync(CancellationToken ct = default);

    // Ends the pause from the tray menu, like unchecking "Effecten pauzeren" there. A pause because
    // Windows is locked or asleep, the screen is off or the screen saver runs, stays until that ends.
    Task ResumeEffectsAsync(CancellationToken ct = default);

    Task<SimulatedDevicesMode> GetSimulatedDevicesAsync(CancellationToken ct = default);

    // Saved like the lighting. When the new mode shows or hides the simulated devices,
    // DevicesChanged follows.
    Task SetSimulatedDevicesAsync(SimulatedDevicesMode mode, CancellationToken ct = default);

    // How many frames per second a moving effect gets, from 1 to 60; saved like the lighting.
    Task<int> GetFramesPerSecondAsync(CancellationToken ct = default);

    // A value outside 1 to 60 is kept within it.
    Task SetFramesPerSecondAsync(int framesPerSecond, CancellationToken ct = default);

    // Whether the effects pause while Windows is locked or asleep, the screen is off or the screen
    // saver runs; saved like the lighting. Turning it off ends such a pause at once, turning it on
    // starts one when Windows is locked, the screen is off or the screen saver runs.
    Task<bool> GetPauseOnLockAndSleepAsync(CancellationToken ct = default);

    Task SetPauseOnLockAndSleepAsync(bool enabled, CancellationToken ct = default);

    // What the devices show during that pause: the last frame (the default), that frame dimmed, or
    // black; not during the pause from the tray menu. Saved like the lighting; another value during
    // such a pause goes to the devices at once.
    Task<PausedLightsMode> GetPausedLightsAsync(CancellationToken ct = default);

    Task SetPausedLightsAsync(PausedLightsMode mode, CancellationToken ct = default);

    // Whether a scene time that passed while the pc slept or the engine did not run is caught up
    // on: after waking up and at the start the scene of the last time that passed comes on. Off,
    // such a time is skipped. Saved like the lighting; it counts from the next wake-up or start.
    Task<bool> GetCatchUpSceneTimesAsync(CancellationToken ct = default);

    Task SetCatchUpSceneTimesAsync(bool enabled, CancellationToken ct = default);

    // Whether Ctrl+Alt+Shift+A and Ctrl+Alt+Shift+U switch everything on and off, as "Alles aan"
    // and "Alles uit" in the tray menu; saved like the lighting. Turning them off gives the keys
    // back to Windows at once.
    Task<bool> GetHotkeysAsync(CancellationToken ct = default);

    Task SetHotkeysAsync(bool enabled, CancellationToken ct = default);

    // The own colors of the toolbox on Kleur ("Eigen kleuren"), which the settings app keeps here
    // as it stops when it closes; saved like the lighting. Empty until the app gives some.
    Task<IReadOnlyList<RgbColor>> GetCustomColorsAsync(CancellationToken ct = default);

    // Replaces them all. A color twice is kept once; more than CustomColors.MaxColors throws.
    Task SetCustomColorsAsync(IReadOnlyList<RgbColor> colors, CancellationToken ct = default);

    // The names of the saved profiles, in alphabetical order.
    Task<IReadOnlyList<string>> GetProfilesAsync(CancellationToken ct = default);

    // Saves all the lighting as it is now under name, which follows ProfileNames. A profile with
    // that name is replaced.
    Task SaveProfileAsync(string name, CancellationToken ct = default);

    // Shows the lighting of the profile on every device; it becomes the current lighting and is
    // saved as such.
    Task LoadProfileAsync(string name, CancellationToken ct = default);

    Task DeleteProfileAsync(string name, CancellationToken ct = default);

    // The profile that is on: the one loaded or saved last, until the lighting changed in another
    // way (LightingChanged). Null when none is.
    Task<string?> GetActiveProfileAsync(CancellationToken ct = default);

    // The lighting the profile gives all devices, without the devices that differ from it, such as
    // to show what the profile looks like.
    Task<LightingInfo> GetProfileLightingAsync(string name, CancellationToken ct = default);

    // When the engine loads the profile by itself; ProfileAutomation.None when it never does.
    Task<ProfileAutomation> GetProfileAutomationAsync(string name, CancellationToken ct = default);

    // Saved with the profile as ProfileAutomation.Normalize gives it: the times count in whole
    // minutes and the program follows ProgramNames. More than ProfileAutomation.MaxTimes times or
    // an invalid program throws.
    Task SetProfileAutomationAsync(string name, ProfileAutomation automation, CancellationToken ct = default);
}
