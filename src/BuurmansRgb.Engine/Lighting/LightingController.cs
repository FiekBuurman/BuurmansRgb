using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Effects;

namespace BuurmansRgb.Engine.Lighting;

// Called from the tray thread, from IPC threads and, when a provider's devices change, from the
// thread pool; the lock guards all state. Events are raised outside the lock, so a listener can
// see a newer output than the render that raised the event: LightingOutput.Sequence orders them.
// An AnimationLoop renders the frames, and only while an animated effect shows, a device
// flashes, fades over to a scene (SceneFade) or the LEDs of a zone are counted, and nothing is
// paused; with static lighting no timer runs at all, also not once a fade is over. A pause, and
// stopping the engine, end a fade at once.
//
// A pause has reasons (PauseReasons). Any reason stops the animation and ends a flash and a count
// of LEDs. Only a manual pause also leaves the devices alone; while Windows is locked or asleep,
// the screen is off or the screen saver runs, a change such as a profile that loads on time, or
// the restore after waking up, still reaches the devices. What they show then follows
// PausedLights: the last frame, dimmed or black.
internal sealed class LightingController : ILightingStateSource, IDisposable
{
    public const int DefaultFramesPerSecond = 30;
    public const int MaxFramesPerSecond = 60;

    // The brightness of PausedLightsMode.Dim, over the brightness the lighting has itself.
    public const double DimmedBrightness = 0.2;

    // The reasons PausedLights applies to: all but the manual pause.
    private const PauseReasons AutomaticReasons = PauseReasons.Locked | PauseReasons.Asleep | PauseReasons.DisplayOff | PauseReasons.ScreenSaver;

    // What a device that is switched off shows: black, and static, so it needs no frames.
    private static readonly StaticEffect Off = new(RgbColor.Black);

    private readonly IReadOnlyList<IDeviceProvider> _providers;
    private readonly Lock _gate = new();

    // Held around a whole pause change, outside _gate, so PauseChanged follows the order of the changes.
    private readonly Lock _pausing = new();

    // Device lists are fetched one provider at a time, so a slow provider cannot overtake a newer list.
    // Not disposed: a refresh may still hold it or wait for it when the controller is disposed, and
    // without AvailableWaitHandle it holds no kernel handle.
    private readonly SemaphoreSlim _discovering = new(1, 1);
    private readonly TimeProvider _time;
    private readonly long _epoch;
    private readonly AnimationLoop _animation;
    private readonly DeviceOutputs _outputs;
    private readonly LightingTable _lighting = new();
    private readonly LedColorStore _ledColors = new();
    private readonly LedOverlayStore _overlays = new();
    private readonly HashSet<string> _zonesAlike = new(StringComparer.Ordinal);

    // The own effects by effect id, each with the effect that plays it: one for every device, as
    // it keeps nothing between frames. SetCustomEffects fills the spare one from the current one
    // and swaps them, so an effect with the same steps keeps its StepEffect.
    private Dictionary<string, CustomEffectPlayer> _customEffects = new(StringComparer.Ordinal);
    private Dictionary<string, CustomEffectPlayer> _spareCustomEffects = new(StringComparer.Ordinal);

    // Whether SetCustomEffects gave the own effects there are. Until then (effects.json out of
    // reach, or no library at all) an own effect that is not in _customEffects may still exist, so
    // the lighting keeps its id and only shows the device color for the moment.
    private bool _customEffectsKnown;

    // Removed again on Dispose, so a provider that outlives the controller cannot refresh it.
    private readonly List<(IDynamicDeviceProvider Provider, EventHandler Handler)> _devicesChangedHandlers = [];
    private PauseReasons _pause;

    // The LEDs of the zone that are counted (StartCount); one zone at a time.
    private ZoneCount? _count;

    // Grows with every change of the count, under the lock, so a listener can drop a state that
    // arrives late (ZoneCountInfo.Sequence): CountChanged goes out from the render thread and from
    // IPC threads.
    private long _countSequence;

    // A restore that waits for the manual pause to end.
    private bool _restorePending;
    private bool _disposed;
    private long _sequence;
    private int _framesPerSecond = DefaultFramesPerSecond;
    private PausedLightsMode _pausedLights = PausedLightsMode.Keep;
    private TimeSpan _sceneFade = Contracts.SceneFade.Default;

    // Effects run on the time of timeProvider, counted from the moment the controller was made.
    public LightingController(IReadOnlyList<IDeviceProvider> providers, TimeProvider? timeProvider = null)
    {
        _providers = providers;
        _time = timeProvider ?? TimeProvider.System;
        _epoch = _time.GetTimestamp();
        _animation = new AnimationLoop(_time, FramePeriodLocked, RenderAnimationFrame);
        _outputs = new DeviceOutputs(providers.Count);
        for (var index = 0; index < providers.Count; index++)
        {
            if (providers[index] is IDynamicDeviceProvider dynamicProvider)
            {
                var providerIndex = index;
                // On the thread pool: the provider may raise this while the engine waits for it.
                EventHandler handler = (_, _) => _ = Task.Run(() => RefreshAsync(providerIndex));
                dynamicProvider.DevicesChanged += handler;
                _devicesChangedHandlers.Add((dynamicProvider, handler));
            }
        }
    }

    // Raised after each render, also while paused, so a preview shows what the lights will show.
    // Listeners read the colors with GetOutput, so nothing is copied while nobody listens.
    public event EventHandler? Rendered;

    // Raised when the lighting is changed through SetLighting, SetLedColors, SetOverlay or Replace; not when it
    // is applied from settings, and not when a zone gets another length: the stored LED colors keep
    // the zone lengths they were chosen for and follow the zones by themselves (LedColorStore).
    public event EventHandler? LightingChanged;

    // Raised when the device list changed, before the render that gives new devices their lighting.
    public event EventHandler? DevicesChanged;

    // Raised with the new reasons when the pause changes, in the order of the changes.
    public event EventHandler<PauseReasons>? PauseChanged;

    // Raised when a count of LEDs starts, goes to another LED, changes or ends; read Count for
    // the newest state. Raised from the render loop too, without allocating.
    public event EventHandler? CountChanged;

    // The count of LEDs as it is now, with the sequence of its last change; ZoneCountInfo.None
    // (with that sequence) while no zone is counted.
    public ZoneCountInfo Count
    {
        get
        {
            lock (_gate)
            {
                return (_count?.Info ?? ZoneCountInfo.None) with { Sequence = _countSequence };
            }
        }
    }

    public PauseReasons Pause
    {
        get
        {
            lock (_gate)
            {
                return _pause;
            }
        }
    }

    // True while the render loop runs.
    public bool IsAnimating
    {
        get
        {
            lock (_gate)
            {
                return _animation.IsRunning;
            }
        }
    }

    // Raised when FramesPerSecond got another value; read FramesPerSecond for the newest one.
    public event EventHandler? FramesPerSecondChanged;

    // Kept between 1 and MaxFramesPerSecond. Effects are time-based, so a lower rate only makes
    // the animation less smooth, not slower.
    public int FramesPerSecond
    {
        get
        {
            lock (_gate)
            {
                return _framesPerSecond;
            }
        }
        set
        {
            bool changed;
            lock (_gate)
            {
                var framesPerSecond = Math.Clamp(value, 1, MaxFramesPerSecond);
                changed = framesPerSecond != _framesPerSecond;
                _framesPerSecond = framesPerSecond;
                _animation.Period = FramePeriodLocked;
            }

            if (changed)
            {
                FramesPerSecondChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    // Raised when PausedLights got another value; read PausedLights for the newest one.
    public event EventHandler? PausedLightsChanged;

    // What the devices show while the pause has a reason besides the manual one: the last frame
    // (Keep), that frame at DimmedBrightness (Dim) or black (Off). Only what goes to the devices
    // changes; the buffers, and so the preview, keep the real colors, and when the last of those
    // reasons ends the devices get them again. Another value during such a pause goes out at once;
    // it holds and is reported even when a device fails then.
    public PausedLightsMode PausedLights
    {
        get
        {
            lock (_gate)
            {
                return _pausedLights;
            }
        }
        set
        {
            var changed = false;
            try
            {
                lock (_gate)
                {
                    var oldScale = SendScaleLocked;
                    changed = _pausedLights != value;
                    _pausedLights = value;
                    if (SendScaleLocked != oldScale)
                    {
                        SendAllLocked();
                    }
                }
            }
            finally
            {
                if (changed)
                {
                    PausedLightsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    // Raised when SceneFade got another value; read SceneFade for the newest one.
    public event EventHandler? SceneFadeChanged;

    // How long a change with fade (a scene, "Alles aan" or "Alles uit") takes to go over from the
    // colors the devices show to the new ones, kept between zero and SceneFade.Max; zero is at once.
    // A fade that runs keeps its own length.
    public TimeSpan SceneFade
    {
        get
        {
            lock (_gate)
            {
                return _sceneFade;
            }
        }
        set
        {
            bool changed;
            lock (_gate)
            {
                var fade = Contracts.SceneFade.Clamp(value);
                changed = fade != _sceneFade;
                _sceneFade = fade;
            }

            if (changed)
            {
                SceneFadeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    // True while a device fades over to a new lighting.
    public bool IsFading
    {
        get
        {
            lock (_gate)
            {
                return _outputs.All.Any(output => output.IsFading);
            }
        }
    }

    // Raised when ZonesAlike got other devices; read ZonesAlike for the newest ones.
    public event EventHandler? ZonesAlikeChanged;

    // The ids of the devices whose zones show one image: the effect renders over the longest zone
    // and every other zone gets it stretched (DeviceOutput.Show). It also holds devices that are
    // not there now, so they have it again when they come back. Not with the per-LED effect,
    // where every LED has a color of its own. It is no part of the lighting: a scene leaves it as
    // it is, and LightingChanged is not raised. The devices show another set at once.
    public IReadOnlySet<string> ZonesAlike
    {
        get
        {
            lock (_gate)
            {
                return new HashSet<string>(_zonesAlike, StringComparer.Ordinal);
            }
        }
        set
        {
            bool changed;
            var rendered = false;
            lock (_gate)
            {
                changed = !_zonesAlike.SetEquals(value);
                if (changed)
                {
                    _zonesAlike.Clear();
                    _zonesAlike.UnionWith(value);
                    rendered = RenderLocked();
                }
            }

            Publish(rendered);
            if (changed)
            {
                ZonesAlikeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    // One device, which must be there, such as from the switch in the settings app.
    public void SetZonesAlike(string deviceId, bool alike)
    {
        bool changed;
        var rendered = false;
        lock (_gate)
        {
            KnownDeviceLocked(deviceId);
            changed = alike ? _zonesAlike.Add(deviceId) : _zonesAlike.Remove(deviceId);
            if (changed)
            {
                rendered = RenderLocked();
            }
        }

        Publish(rendered);
        if (changed)
        {
            ZonesAlikeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlyList<RgbDevice> Devices
    {
        get
        {
            lock (_gate)
            {
                return [.. _outputs.All.Select(output => output.Device)];
            }
        }
    }

    public LightingState State
    {
        get
        {
            lock (_gate)
            {
                return StateLocked();
            }
        }
    }

    // Fetches the devices of every provider; the lights follow with Apply.
    public async Task DiscoverAsync(CancellationToken ct)
    {
        for (var index = 0; index < _providers.Count; index++)
        {
            await UpdateDevicesAsync(index, render: false, ct);
        }
    }

    public void Apply(LightingState state)
    {
        bool rendered;
        lock (_gate)
        {
            ApplyLocked(state);
            rendered = RenderLocked();
        }

        Publish(rendered);
    }

    // Apply as a change, so the new lighting is saved: for example a profile that is loaded.
    // Returns the lighting as it is right after, read under the same lock: State may already hold
    // a change from another thread by then. With fade, the devices whose lighting changes go over
    // to it in SceneFade (FadeLocked); without, it shows at once and ends a fade that runs.
    public LightingState Replace(LightingState state, bool fade = false) => Replace(_ => state, fade);

    // Replace with what change makes of the lighting now, read and applied under the same lock,
    // so a change of another device in between is not lost: for example a scene of some devices
    // over the others (Profile.LightingOver). change runs under the lock, so it only computes.
    public LightingState Replace(Func<LightingState, LightingState> change, bool fade = false)
    {
        ArgumentNullException.ThrowIfNull(change);
        bool rendered;
        LightingState applied;
        lock (_gate)
        {
            var wasSet = _lighting.IsSet;
            var before = StateLocked();
            ApplyLocked(change(before));
            if (fade)
            {
                FadeLocked(wasSet ? before : null);
            }
            else
            {
                EndFadesLocked();
            }

            applied = StateLocked();
            rendered = RenderLocked();
        }

        Publish(rendered);
        LightingChanged?.Invoke(this, EventArgs.Empty);
        return applied;
    }

    // A null deviceId means the global lighting.
    public DeviceLighting GetLighting(string? deviceId)
    {
        lock (_gate)
        {
            if (deviceId is null)
            {
                return _lighting.Global;
            }

            KnownDeviceLocked(deviceId);
            return _lighting.Get(deviceId);
        }
    }

    // The lighting of every device, in the order of the devices, under one lock.
    public IReadOnlyList<(string DeviceId, DeviceLighting Lighting)> GetAllLighting()
    {
        lock (_gate)
        {
            return [.. _outputs.All.Select(output => (output.Device.Id, _lighting.Get(output.Device.Id)))];
        }
    }

    // A global change also applies to every device with its own lighting, so a global color keeps
    // a device's own brightness and the other way round. Changing one device starts from what
    // that device shows now. A change to a brightness outside 0 to 1 or to an unknown effect, also
    // an own effect that does not exist (SetCustomEffects), throws ArgumentException and changes
    // nothing. With fade the devices whose lighting changes go over to it in SceneFade, as with
    // Replace: for "Alles aan" and "Alles uit". Without, it shows at once, also on a device that
    // was fading: its fade ends (EndFadesLocked).
    public void SetLighting(string? deviceId, Func<DeviceLighting, DeviceLighting> change, bool fade = false)
    {
        bool rendered;
        lock (_gate)
        {
            var before = fade && _lighting.IsSet ? StateLocked() : null;
            if (deviceId is null)
            {
                _lighting.SetGlobal(lighting => ThrowIfMissingCustomLocked(change(lighting)));
            }
            else
            {
                KnownDeviceLocked(deviceId);
                _lighting.Set(deviceId, ThrowIfMissingCustomLocked(change(_lighting.Get(deviceId))));
            }

            if (fade)
            {
                FadeLocked(before);
            }
            else
            {
                EndFadesLocked(deviceId);
            }

            rendered = RenderLocked();
        }

        Publish(rendered);
        LightingChanged?.Invoke(this, EventArgs.Empty);
    }

    // The own effects there are now (CustomEffectLibrary), which a lighting can show by their id.
    // A device, or the global lighting, that shows an own effect that is gone gets the static
    // effect in its own color, as a change (LightingChanged), so that is saved; the devices that
    // show an effect that is still there show its new steps at once. Before the lighting is
    // applied, they are only kept for it. Before the first call no own effect counts as gone.
    public void SetCustomEffects(IReadOnlyList<CustomEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        var changed = false;
        bool rendered;
        lock (_gate)
        {
            _customEffectsKnown = true;
            (_customEffects, _spareCustomEffects) = (_spareCustomEffects, _customEffects);
            _customEffects.Clear();
            foreach (var effect in effects)
            {
                _customEffects[effect.Id] = _spareCustomEffects.TryGetValue(effect.Id, out var player) && player.Plays(effect)
                    ? player with { Effect = effect }
                    : new CustomEffectPlayer(effect, EffectCatalog.CreateCustom(effect));
            }

            _spareCustomEffects.Clear();

            if (_lighting.IsSet)
            {
                var state = StateLocked();
                var kept = WithoutMissingCustomLocked(state);
                if (!ReferenceEquals(kept, state))
                {
                    _lighting.Replace(kept.Global, kept.Devices);
                    changed = true;
                }
            }

            rendered = RenderLocked();
        }

        Publish(rendered);
        if (changed)
        {
            LightingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // The colors the per-LED effect shows, one per LED: the stored color, or the device color for
    // an LED without one. That is also where editing them starts.
    public IReadOnlyList<RgbColor> GetLedColors(string deviceId)
    {
        lock (_gate)
        {
            var device = KnownDeviceLocked(deviceId);
            return _ledColors.Resolve(device, _lighting.Get(deviceId).Color);
        }
    }

    // Stores colors for the LEDs from firstLed on and switches the device to the per-LED effect,
    // so the change shows at once. Only these LEDs get a color of their own; the others keep
    // following the device color.
    public void SetLedColors(string deviceId, int firstLed, IReadOnlyList<RgbColor> colors)
    {
        bool rendered;
        lock (_gate)
        {
            var device = LedRangeLocked(deviceId, firstLed, colors.Count);

            _ledColors.Set(device, firstLed, colors);
            _lighting.Set(deviceId, _lighting.Get(deviceId) with { EffectId = EffectIds.PerLed });
            EndFadesLocked(deviceId);
            rendered = RenderLocked();
        }

        Publish(rendered);
        LightingChanged?.Invoke(this, EventArgs.Empty);
    }

    // The layer over the effect of the device, one per LED it has now: a fixed color, or null for an
    // LED that shows the effect.
    public IReadOnlyList<RgbColor?> GetOverlay(string deviceId)
    {
        lock (_gate)
        {
            return _overlays.Resolve(KnownDeviceLocked(deviceId));
        }
    }

    // Gives the LEDs from firstLed on a fixed color over the effect, whatever the effect, or takes it
    // away for a null; the other LEDs keep theirs. The effect stays, and the change shows at once.
    // A device that is off stays off, layer and all, until it goes on.
    public void SetOverlay(string deviceId, int firstLed, IReadOnlyList<RgbColor?> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        bool rendered;
        lock (_gate)
        {
            LedRangeLocked(deviceId, firstLed, colors.Count);

            _overlays.Set(deviceId, firstLed, colors);
            EndFadesLocked(deviceId);
            rendered = RenderLocked();
        }

        Publish(rendered);
        LightingChanged?.Invoke(this, EventArgs.Empty);
    }

    // Gives a resizable zone another length. The provider applies it, possibly in the background,
    // and reports the device again through DevicesChanged; the lights follow then.
    public void ResizeZone(string deviceId, int zoneIndex, int ledCount)
    {
        IResizableDeviceProvider provider;
        lock (_gate)
        {
            var output = KnownOutputLocked(deviceId);
            var zone = output.Device.GetZone(zoneIndex);
            if (!zone.Resizable || output.Provider is not IResizableDeviceProvider resizable)
            {
                throw new ArgumentException($"Zone '{zone.Name}' of device '{deviceId}' cannot be resized.", nameof(zoneIndex));
            }

            zone.ThrowIfCannotHold(ledCount);
            provider = resizable;
        }

        // Outside the lock: a provider may report the change right away.
        provider.ResizeZone(deviceId, zoneIndex, ledCount);
    }

    // Lets the device blink for a moment (DeviceFlash), so the user can find it in the pc; then it
    // shows its own lighting again. Nothing is stored and LightingChanged is not raised. The render
    // loop runs only until the flash is over. One device flashes at a time, so the user who clicks
    // through the list sees only the last one; that also holds for FlashZone. While paused the
    // devices stay as they are, so nothing happens then. A flash changes no lighting, so a device
    // that is gone (the list in the settings app may be behind) is skipped instead of reported as
    // an error.
    public void Flash(string deviceId) => StartFlash(deviceId, zoneIndex: null);

    // Flash for the LEDs of one zone (an index into the device's zones) only, so the user sees
    // which header of a motherboard is which fan or strip; the rest of the device keeps its
    // colors. It replaces a flash of any device or zone, as Flash does. The zone counts as the
    // engine knows it: a zone the list in the settings app has but the engine not (any more) is
    // skipped like a device that is gone. A zone without LEDs, like a device without, has nothing
    // to blink, but still ends the flash before.
    public void FlashZone(string deviceId, int zoneIndex) => StartFlash(deviceId, zoneIndex);

    // Counts the LEDs of one zone (ZoneCount) from LED 1 on, up to ledCount, at ledsPerSecond:
    // only the drawing; ZoneCounter gives the zone its longest length for it and its own length
    // back. It replaces the count of any zone and changes no lighting. While paused, or for a
    // device or zone the engine does not know (any more), nothing starts and it returns false. A
    // device list that is made again keeps the count on the zone with the same index and name.
    public bool StartCount(string deviceId, int zoneIndex, int ledCount, int ledsPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ledCount, 1);
        lock (_gate)
        {
            if (_outputs.Find(deviceId) is not { } output || _pause != PauseReasons.None || !_lighting.IsSet
                || zoneIndex < 0 || zoneIndex >= output.Device.Zones.Count)
            {
                return false;
            }

            // ZoneCount checks the pace, before the count that runs stops.
            var time = EffectTime;
            var count = new ZoneCount(deviceId, zoneIndex, output.Device.Zones[zoneIndex].Name, ledCount, ledsPerSecond, time);
            StopCountLocked();
            _count = count;
            _countSequence++;
            ShowCountLocked(output, time);
            UpdateAnimationLocked();
        }

        Publish(true);
        CountChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    // Changes the count that runs, such as a pause or another LED; false when none runs.
    public bool ChangeCount(ZoneCountChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_gate)
        {
            if (_count is not { } count)
            {
                return false;
            }

            var time = EffectTime;
            count.Change(time, change);
            _countSequence++;
            if (_outputs.Find(count.DeviceId) is { } output)
            {
                ShowCountLocked(output, time);
            }

            UpdateAnimationLocked();
        }

        Publish(true);
        CountChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    // Ends the count, so the zone shows the effect again.
    public void StopCount()
    {
        bool stopped;
        lock (_gate)
        {
            stopped = StopCountLocked();
            UpdateAnimationLocked();
        }

        Publish(stopped);
        if (stopped)
        {
            CountChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Copies the buffers, so the caller can convert them without holding the lock.
    public LightingOutput GetOutput()
    {
        lock (_gate)
        {
            return new(_sequence, [.. _outputs.All.Select(output => new DeviceColors(output.Device.Id, [.. output.Buffer]))]);
        }
    }

    // Adds or ends one reason to pause. The first reason stops the animation and leaves the devices
    // as they are, except that a flash and a count end first, so no device stays white or dark;
    // CountChanged follows then, after PauseChanged. When a reason ends and no manual pause is
    // left, the latest lighting goes out again, with a restore that waited for the manual pause:
    // while Windows was locked another user's engine may have changed the devices. When the last
    // reason ends, the animation runs again. Without a manual pause, the devices also get the
    // colors again when what they show changes with PausedLights. The new pause holds and is
    // reported even when a device fails while its colors go out.
    public void SetPaused(PauseReasons reason, bool paused)
    {
        lock (_pausing)
        {
            var pause = PauseReasons.None;
            var changed = false;
            var countStopped = false;
            try
            {
                var rendered = false;
                lock (_gate)
                {
                    var old = _pause;
                    pause = paused ? old | reason : old & ~reason;
                    if (pause == old)
                    {
                        return;
                    }

                    var oldScale = SendScaleLocked;
                    _pause = pause;
                    changed = true;
                    UpdateAnimationLocked();
                    var sent = false;
                    if (old == PauseReasons.None)
                    {
                        rendered = StopFadesLocked();
                        rendered |= StopFlashesLocked();
                        countStopped = StopCountLocked();
                        rendered |= countStopped;
                    }
                    else if (!paused && !IsManual(pause))
                    {
                        if (_restorePending)
                        {
                            _restorePending = false;
                            RestoreProvidersLocked();
                        }

                        rendered = RenderLocked();
                        sent = true;
                    }

                    if (!sent && SendScaleLocked != oldScale)
                    {
                        SendAllLocked();
                    }
                }

                Publish(rendered);
            }
            finally
            {
                if (changed)
                {
                    // In the log before the listeners, so what they do follows the pause there.
                    Trace.TraceInformation(PauseLog.Line(reason, paused, pause));
                    PauseChanged?.Invoke(this, pause);
                }

                if (countStopped)
                {
                    CountChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    // After sleep many devices show an effect of their own again. The providers that can set their
    // devices up again do so, and every device gets its lighting once more, also the ones that
    // did not change. That also happens while Windows is locked, so the lock screen after waking
    // up has the lighting too; during a manual pause the devices stay as they are until it ends.
    public void RestoreDevices()
    {
        bool rendered;
        lock (_gate)
        {
            if (IsManual(_pause))
            {
                _restorePending = true;
                return;
            }

            RestoreProvidersLocked();
            rendered = RenderLocked();
        }

        Publish(rendered);
    }

    // Lets every provider that can search for its devices again do so, all at the same time, and
    // then takes over their lists, so DevicesChanged has gone out and a device that came back has
    // its lighting again once this completes. The providers report their new lists themselves
    // too; taking them over once more costs a refresh, but nobody has to wait for that report.
    // False when a provider could not reach the source of its devices, such as OpenRGB that does
    // not run.
    public async Task<bool> RescanDevicesAsync(CancellationToken ct)
    {
        List<(int Index, Task<bool> Rescan)> rescans = [];
        for (var index = 0; index < _providers.Count; index++)
        {
            if (_providers[index] is IRescannableDeviceProvider provider)
            {
                rescans.Add((index, RescanAsync(provider, ct)));
            }
        }

        var reached = true;
        foreach (var (index, rescan) in rescans)
        {
            reached &= await rescan;
            await UpdateDevicesAsync(index, render: true, ct);
        }

        return reached;
    }

    // One step of the animation: renders the animated effects and sends only the devices whose
    // colors changed. Allocates nothing, so it can run 60 times a second without feeding the GC.
    // The Rendered listeners run on this thread too; the one of a connected settings app copies
    // the colors for its preview, so that does allocate.
    public void RenderAnimationFrame()
    {
        var rendered = false;
        var counted = false;
        lock (_gate)
        {
            // A tick that was already on its way when the animation stopped.
            if (!_animation.IsRunning)
            {
                return;
            }

            var time = EffectTime;
            counted = _count?.Advance(time) ?? false;
            if (counted)
            {
                _countSequence++;
            }

            var ended = false;
            foreach (var output in _outputs.All)
            {
                // The step that ends a count is drawn too, also on a device that needs no more
                // frames then.
                if (output.IsAnimated || (counted && output.IsCounting))
                {
                    var flashing = output.IsFlashing;
                    var fading = output.IsFading;
                    output.Render(time);
                    ended |= (flashing && !output.IsFlashing) || (fading && !output.IsFading);
                    rendered |= output.SendIfChanged();
                }
            }

            // With only static effects left after a flash or a fade, the timer stops again.
            if (ended || (counted && _count is { Running: false }))
            {
                UpdateAnimationLocked();
            }

            if (rendered)
            {
                _sequence++;
            }
        }

        Publish(rendered);
        if (counted)
        {
            CountChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            // The engine leaves the devices as they are when it stops, but not white or dark from
            // a flash or a count that is still running, nor halfway a fade: they get its end at
            // once, without waiting for it.
            StopFadesLocked();
            StopFlashesLocked();
            StopCountLocked();
            _disposed = true;
            _animation.Dispose();
        }

        foreach (var (provider, handler) in _devicesChangedHandlers)
        {
            provider.DevicesChanged -= handler;
        }
    }

    // A device that comes back keeps its own lighting: it stays in the state while the device is gone.
    private async Task RefreshAsync(int providerIndex)
    {
        try
        {
            await UpdateDevicesAsync(providerIndex, render: true, CancellationToken.None);
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }

    // A provider that fails counts as one that could not reach its source; the others go on.
    private static async Task<bool> RescanAsync(IRescannableDeviceProvider provider, CancellationToken ct)
    {
        try
        {
            return await provider.RequestRescanAsync(ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            Trace.TraceError($"Rescanning the devices of {provider.Name} failed: {exception}");
            return false;
        }
    }

    private async Task UpdateDevicesAsync(int providerIndex, bool render, CancellationToken ct)
    {
        await _discovering.WaitAsync(ct);
        try
        {
            var provider = _providers[providerIndex];
            var devices = await provider.DiscoverAsync(ct);
            bool rendered;
            lock (_gate)
            {
                // Disposed while the provider answered: the engine is shutting down, so the
                // devices keep what they show.
                if (_disposed)
                {
                    return;
                }

                _outputs.Replace(providerIndex, provider, devices);
                AttachCountLocked();
                _ledColors.Adopt(devices);
                rendered = render && RenderLocked();
            }

            DevicesChanged?.Invoke(this, EventArgs.Empty);
            Publish(rendered);
        }
        finally
        {
            _discovering.Release();
        }
    }

    // Gives every device the effect of its current lighting and renders it. Returns false when
    // there is no lighting to render yet.
    private bool RenderLocked()
    {
        if (!_lighting.IsSet)
        {
            return false;
        }

        var time = EffectTime;
        foreach (var output in _outputs.All)
        {
            var lighting = _lighting.Get(output.Device.Id);
            var effect = lighting.On ? EffectLocked(lighting, output.Device) : Off;
            var zonesAlike = lighting.EffectId != EffectIds.PerLed && _zonesAlike.Contains(output.Device.Id);
            output.Show(effect, lighting.Brightness, lighting.Speed, zonesAlike, lighting.Direction);

            // Off is off: black without the layer too.
            output.ShowOverlay(lighting.On ? _overlays.For(output.Device.Id) : []);
            output.Render(time);
            if (!IsManual(_pause))
            {
                output.Send(SendScaleLocked);
            }
        }

        _sequence++;
        UpdateAnimationLocked();
        return true;
    }

    // Flash and FlashZone: a null zoneIndex flashes the whole device. Without LEDs to blink, of
    // the zone or the device, no flash starts, but the request still replaces the one before, as
    // it did before zones could flash: that one ends with the own colors.
    private void StartFlash(string deviceId, int? zoneIndex)
    {
        var rendered = false;
        lock (_gate)
        {
            if (_outputs.Find(deviceId) is not { } output || _pause != PauseReasons.None || !_lighting.IsSet)
            {
                return;
            }

            var (first, count) = (0, output.Buffer.Length);
            if (zoneIndex is { } zone)
            {
                if (zone < 0 || zone >= output.Device.Zones.Count)
                {
                    return;
                }

                (first, count) = RgbZone.RangesOf(output.Device.Zones, output.Device.LedCount)[zone];
            }

            if (count == 0)
            {
                rendered = StopFlashesLocked();
            }
            else
            {
                StopFlashesLocked(except: output);
                var time = EffectTime;
                output.StartFlash(time, first, count);
                output.Render(time);
                output.Send();
                _sequence++;
                rendered = true;
            }

            UpdateAnimationLocked();
        }

        Publish(rendered);
    }

    // Call it once the new lighting is in, before the RenderLocked that shows it. Lets every
    // device whose lighting, per-LED colors or layer differ from before go over from the colors
    // it shows now, also in the middle of a fade, to what the next RenderLocked gives it, in
    // SceneFade. A device that stays the same goes on as it was: an effect that moves does not
    // stand still for a moment, and a fade that runs goes on, on time. At once, so a fade that
    // runs ends, with a fade of zero, before there was lighting (a null before), and during a
    // pause: no frames run then, and what goes to the devices, such as a scene on time while
    // Windows is locked, does not wait. Show has not given the new effects yet, so BeginFade still
    // renders the old ones.
    private void FadeLocked(LightingState? before)
    {
        var fade = _disposed || _pause != PauseReasons.None || before is null ? TimeSpan.Zero : _sceneFade;
        var time = EffectTime;
        foreach (var output in _outputs.All)
        {
            if (fade <= TimeSpan.Zero)
            {
                output.EndFade();
            }
            else if (ChangedLocked(output.Device, before!))
            {
                output.BeginFade(time, fade);
            }
        }
    }

    // Whether device shows other lighting, other per-LED colors or another layer now than in
    // before. Only the per-LED effect shows those colors, so they count only for a device that
    // shows it, before and now (with another effect before or now, the lighting differs already);
    // the layer shows with any effect, but not on a device that is off, before and now.
    private bool ChangedLocked(RgbDevice device, LightingState before)
    {
        var lighting = _lighting.Get(device.Id);
        return before.Devices.GetValueOrDefault(device.Id, before.Global) != lighting
            || (lighting.EffectId == EffectIds.PerLed && !_ledColors.SameAs(before.LedColors, device))
            || (lighting.On && !_overlays.SameAs(before.Overlays, device));
    }

    // A change of its own, without fade, shows at once: it ends the fade of the device it changes,
    // or of every device for a null deviceId (a global change, or all lighting at once). The
    // RenderLocked that follows sends the new lighting, and stops the render loop when nothing
    // else moves.
    private void EndFadesLocked(string? deviceId = null)
    {
        if (deviceId is not null)
        {
            _outputs.Find(deviceId)?.EndFade();
            return;
        }

        foreach (var output in _outputs.All)
        {
            output.EndFade();
        }
    }

    // Gives every device that fades the end of its fade at once, as when a pause starts or the
    // engine stops, so no device stays halfway and nothing waits for the fade. Returns true when
    // there was one.
    private bool StopFadesLocked() => StopLocked(static output => output.IsFading, static output => output.EndFade());

    // Gives every device that is flashing, but except, its own colors again. Returns true when
    // there was one.
    private bool StopFlashesLocked(DeviceOutput? except = null) =>
        StopLocked(static output => output.IsFlashing, static output => output.StopFlash(), except);

    // Ends with stop what runs on every device for which runs is true, but except, and sends what
    // the device shows then. Returns true when there was one.
    private bool StopLocked(Func<DeviceOutput, bool> runs, Action<DeviceOutput> stop, DeviceOutput? except = null)
    {
        var time = EffectTime;
        var stopped = false;
        foreach (var output in _outputs.All)
        {
            if (output != except && runs(output))
            {
                stop(output);
                output.Render(time);
                output.Send(SendScaleLocked);
                stopped = true;
            }
        }

        if (stopped)
        {
            _sequence++;
        }

        return stopped;
    }

    // Draws the count over the zone of output as the device has it now, and sends it.
    private void ShowCountLocked(DeviceOutput output, TimeSpan time)
    {
        AttachCountLocked(output);
        output.Render(time);
        output.Send(SendScaleLocked);
        _sequence++;
    }

    // Gives the output of the counted device the count, over the LEDs its zone has now; a device
    // list without the zone (with the same index and name) leaves it without, until ZoneCounter
    // ends the count.
    private void AttachCountLocked(DeviceOutput? output = null)
    {
        if (_count is not { } count || (output ?? _outputs.Find(count.DeviceId)) is not { } counted)
        {
            return;
        }

        var zones = counted.Device.Zones;
        if (count.ZoneIndex < zones.Count && zones[count.ZoneIndex].Name == count.ZoneName)
        {
            var (first, ledCount) = RgbZone.RangesOf(zones, counted.Device.LedCount)[count.ZoneIndex];
            counted.ShowCount(count, first, ledCount);
        }
        else
        {
            counted.ShowCount(null);
        }
    }

    // Gives the counted device its own colors again. Returns true when a zone was counted.
    private bool StopCountLocked()
    {
        if (_count is not { } count)
        {
            return false;
        }

        _count = null;
        _countSequence++;
        if (_outputs.Find(count.DeviceId) is { } output)
        {
            output.ShowCount(null);
            output.Render(EffectTime);
            output.Send(SendScaleLocked);
        }

        _sequence++;
        return true;
    }

    // Sends what every device shows once more, with the scale of the pause; the buffers stay as
    // they are. Nothing before there is lighting, nor during a manual pause.
    private void SendAllLocked()
    {
        if (!_lighting.IsSet || IsManual(_pause))
        {
            return;
        }

        var scale = SendScaleLocked;
        foreach (var output in _outputs.All)
        {
            output.Send(scale);
        }
    }

    // What goes to the devices is scaled by this: 1 while nothing pauses or a manual pause holds
    // (which sends nothing anyway), otherwise as PausedLights says.
    private double SendScaleLocked =>
        IsManual(_pause) || (_pause & AutomaticReasons) == PauseReasons.None
            ? 1.0
            : _pausedLights switch
            {
                PausedLightsMode.Dim => DimmedBrightness,
                PausedLightsMode.Off => 0.0,
                _ => 1.0,
            };

    private void RestoreProvidersLocked()
    {
        foreach (var provider in _providers.OfType<IRestorableDeviceProvider>())
        {
            try
            {
                provider.RestoreDevices();
            }
            catch (Exception exception)
            {
                // The other providers still get their turn, and the colors still go out.
                Trace.TraceError(exception.ToString());
            }
        }
    }

    // Every device runs on the same clock, so the same animated effect stays in step on all of them.
    private TimeSpan EffectTime => _time.GetElapsedTime(_epoch);

    private TimeSpan FramePeriodLocked => TimeSpan.FromSeconds(1.0 / _framesPerSecond);

    private void UpdateAnimationLocked()
    {
        if (!_disposed && _pause == PauseReasons.None && _lighting.IsSet && _outputs.All.Any(output => output.IsAnimated))
        {
            _animation.Start();
        }
        else
        {
            _animation.Stop();
        }
    }

    private static bool IsManual(PauseReasons pause) => pause.HasFlag(PauseReasons.Manual);

    // With read, returns the lighting as it is once state is on.
    private void ApplyLocked(LightingState state)
    {
        state = WithoutMissingCustomLocked(state);
        _lighting.Replace(state.Global, state.Devices);
        _ledColors.Replace(state.LedColors);
        _ledColors.Adopt(_outputs.All.Select(output => output.Device));
        _overlays.Replace(state.Overlays);
    }

    private LightingState StateLocked() =>
        new(_lighting.Global, _lighting.Snapshot(), _ledColors.Snapshot()) { Overlays = _overlays.Snapshot() };

    // The effect a device shows with lighting: an own effect plays its steps, and one that is gone
    // shows the device color: only for a moment once the effects are known (see
    // WithoutMissingCustomLocked), and until they are (_customEffectsKnown).
    private IEffect EffectLocked(DeviceLighting lighting, RgbDevice device)
    {
        if (CustomEffects.IsCustom(lighting.EffectId))
        {
            return _customEffects.TryGetValue(lighting.EffectId, out var custom) ? custom.Steps : new StaticEffect(lighting.Color);
        }

        return EffectCatalog.Create(lighting.EffectId, lighting.Color, _ledColors.For(device), lighting.Loop, EffectCatalog.SeedOf(device.Id), lighting.EffectColors);
    }

    // The state with the static effect in place of an own effect that does not exist (any more),
    // such as from a scene saved before it was deleted; the same state when there is none.
    private LightingState WithoutMissingCustomLocked(LightingState state)
    {
        if (!IsMissingCustomLocked(state.Global) && !state.Devices.Values.Any(IsMissingCustomLocked))
        {
            return state;
        }

        return state with
        {
            Global = WithoutMissingCustomLocked(state.Global),
            Devices = state.Devices.ToDictionary(pair => pair.Key, pair => WithoutMissingCustomLocked(pair.Value)),
        };
    }

    private DeviceLighting WithoutMissingCustomLocked(DeviceLighting lighting) =>
        IsMissingCustomLocked(lighting) ? lighting with { EffectId = EffectIds.Static } : lighting;

    private bool IsMissingCustomLocked(DeviceLighting lighting) =>
        _customEffectsKnown && CustomEffects.IsCustom(lighting.EffectId) && !_customEffects.ContainsKey(lighting.EffectId);

    private DeviceLighting ThrowIfMissingCustomLocked(DeviceLighting lighting) =>
        IsMissingCustomLocked(lighting) ? throw EffectCatalog.UnknownCustom(lighting.EffectId) : lighting;

    private void Publish(bool rendered)
    {
        if (rendered)
        {
            Rendered?.Invoke(this, EventArgs.Empty);
        }
    }

    private RgbDevice KnownDeviceLocked(string deviceId) => KnownOutputLocked(deviceId).Device;

    // The device, when count colors from firstLed on fit on its LEDs. An exception names colors, the
    // list that is too long, as the callers take it.
    private RgbDevice LedRangeLocked(string deviceId, int firstLed, int count)
    {
        var device = KnownDeviceLocked(deviceId);
        ArgumentOutOfRangeException.ThrowIfNegative(firstLed);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(firstLed, device.LedCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, device.LedCount - firstLed, "colors");
        return device;
    }

    private DeviceOutput KnownOutputLocked(string deviceId) =>
        _outputs.Find(deviceId)
            ?? throw DeviceErrors.Unknown(deviceId);

    // An own effect and the StepEffect that plays its steps. Equal steps are compared one color at a
    // time, as an effect from the pipe is a new copy even when only its name changed.
    private readonly record struct CustomEffectPlayer(CustomEffect Effect, StepEffect Steps)
    {
        public bool Plays(CustomEffect effect) =>
            ReferenceEquals(Effect.Steps, effect.Steps) || Effect.Steps.SequenceEqual(effect.Steps);
    }
}
