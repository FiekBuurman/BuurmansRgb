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
// The parts live in partial files: the devices (.Devices), the pause (.Pause), the options
// (.Options), a flash (.Flash), a count of LEDs (.Count) and a fade (.Fade); this one has the
// state, the lighting and the render. All of them share the one lock.
internal sealed partial class LightingController : ILightingStateSource, IDisposable
{
    // What a device that is switched off shows: black, and static, so it needs no frames.
    private static readonly StaticEffect Off = new(RgbColor.Black);

    private readonly IReadOnlyList<IDeviceProvider> _providers;
    private readonly Lock _gate = new();
    private readonly TimeProvider _time;
    private readonly long _epoch;
    private readonly AnimationLoop _animation;
    private readonly DeviceOutputs _outputs;
    private readonly LightingTable _lighting = new();
    private readonly LedColorStore _ledColors = new();
    private readonly LedOverlayStore _overlays = new();
    private readonly CustomEffectPlayers _customEffects = new();
    private bool _disposed;
    private long _sequence;

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
        LightingState applied = null!;
        Mutate(() =>
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
            return true;
        });
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

    // "Alles aan" and "Alles uit": every device, also the ones with lighting of their own, goes on
    // or off with a fade.
    public void SetAllOn(bool on) => SetLighting(null, lighting => lighting with { On = on }, fade: true);

    // A global change also applies to every device with its own lighting, so a global color keeps
    // a device's own brightness and the other way round. Changing one device starts from what
    // that device shows now. A change to a brightness outside 0 to 1 or to an unknown effect, also
    // an own effect that does not exist (SetCustomEffects), throws ArgumentException and changes
    // nothing. With fade the devices whose lighting changes go over to it in SceneFade, as with
    // Replace: for "Alles aan" and "Alles uit". Without, it shows at once, also on a device that
    // was fading: its fade ends (EndFadesLocked).
    public void SetLighting(string? deviceId, Func<DeviceLighting, DeviceLighting> change, bool fade = false)
    {
        Mutate(() =>
        {
            var before = fade && _lighting.IsSet ? StateLocked() : null;
            if (deviceId is null)
            {
                _lighting.SetGlobal(lighting => _customEffects.ThrowIfMissing(change(lighting)));
            }
            else
            {
                KnownDeviceLocked(deviceId);
                _lighting.Set(deviceId, _customEffects.ThrowIfMissing(change(_lighting.Get(deviceId))));
            }

            if (fade)
            {
                FadeLocked(before);
            }
            else
            {
                EndFadesLocked(deviceId);
            }

            return true;
        });
    }

    // The own effects there are now (CustomEffectLibrary), which a lighting can show by their id.
    // A device, or the global lighting, that shows an own effect that is gone gets the static
    // effect in its own color, as a change (LightingChanged), so that is saved; the devices that
    // show an effect that is still there show its new steps at once. Before the lighting is
    // applied, they are only kept for it. Before the first call no own effect counts as gone.
    public void SetCustomEffects(IReadOnlyList<CustomEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        Mutate(
            () =>
            {
                _customEffects.Set(effects);
                if (!_lighting.IsSet)
                {
                    return false;
                }

                var state = StateLocked();
                var kept = _customEffects.WithoutMissing(state);
                if (ReferenceEquals(kept, state))
                {
                    return false;
                }

                _lighting.Replace(kept.Global, kept.Devices);
                return true;
            },
            alwaysRender: true);
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
        ArgumentNullException.ThrowIfNull(colors);
        Mutate(() =>
        {
            var device = LedRangeLocked(deviceId, firstLed, colors.Count);

            _ledColors.Set(device, firstLed, colors);
            _lighting.Set(deviceId, _lighting.Get(deviceId) with { EffectId = EffectIds.PerLed });
            EndFadesLocked(deviceId);
            return true;
        });
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
        Mutate(() =>
        {
            LedRangeLocked(deviceId, firstLed, colors.Count);

            _overlays.Set(deviceId, firstLed, colors);
            EndFadesLocked(deviceId);
            return true;
        });
    }

    // Copies the buffers, so the caller can convert them without holding the lock.
    public LightingOutput GetOutput()
    {
        lock (_gate)
        {
            return new(_sequence, [.. _outputs.All.Select(output => new DeviceColors(output.Device.Id, [.. output.Buffer]))]);
        }
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

    // Every device runs on the same clock, so the same animated effect stays in step on all of them.
    private TimeSpan EffectTime => _time.GetElapsedTime(_epoch);

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

    // With read, returns the lighting as it is once state is on.
    private void ApplyLocked(LightingState state)
    {
        state = _customEffects.WithoutMissing(state);
        _lighting.Replace(state.Global, state.Devices);
        _ledColors.Replace(state.LedColors);
        _ledColors.Adopt(_outputs.All.Select(output => output.Device));
        _overlays.Replace(state.Overlays);
    }

    private LightingState StateLocked() =>
        new(_lighting.Global, _lighting.Snapshot(), _ledColors.Snapshot()) { Overlays = _overlays.Snapshot() };

    // The effect a device shows with lighting; an own effect comes from CustomEffectPlayers.
    private IEffect EffectLocked(DeviceLighting lighting, RgbDevice device)
    {
        if (CustomEffects.IsCustom(lighting.EffectId))
        {
            return _customEffects.EffectOf(lighting);
        }

        return EffectCatalog.Create(lighting.EffectId, lighting.Color, _ledColors.For(device), lighting.Loop, EffectCatalog.SeedOf(device.Id), lighting.EffectColors);
    }

    // A change of the lighting: changeLocked runs under the lock and returns whether it changed the
    // lighting. It renders then, also without a change when alwaysRender is set, and outside the
    // lock Rendered and LightingChanged go out. A changeLocked that throws has changed nothing, so
    // nothing renders and no event goes out.
    private void Mutate(Func<bool> changeLocked, bool alwaysRender = false)
    {
        bool changed;
        var rendered = false;
        lock (_gate)
        {
            changed = changeLocked();
            if (changed || alwaysRender)
            {
                rendered = RenderLocked();
            }
        }

        Publish(rendered);
        if (changed)
        {
            LightingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Publish(bool rendered)
    {
        if (rendered)
        {
            Rendered?.Invoke(this, EventArgs.Empty);
        }
    }
}
