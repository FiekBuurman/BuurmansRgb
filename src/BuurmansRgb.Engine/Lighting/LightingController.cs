using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Called from the tray thread, from IPC threads and, when a provider's devices change, from the
// thread pool; the lock guards all state. Events are raised outside the lock, so a listener can
// see a newer output than the render that raised the event: LightingOutput.Sequence orders them.
// An AnimationLoop renders the frames, and only while an animated effect shows and nothing is
// paused; with static lighting no timer runs at all.
internal sealed class LightingController : IDisposable
{
    public const int DefaultFramesPerSecond = 30;
    public const int MaxFramesPerSecond = 60;

    private readonly IReadOnlyList<IDeviceProvider> _providers;
    private readonly Lock _gate = new();

    // Device lists are fetched one provider at a time, so a slow provider cannot overtake a newer list.
    private readonly SemaphoreSlim _discovering = new(1, 1);
    private readonly TimeProvider _time;
    private readonly long _epoch;
    private readonly AnimationLoop _animation;
    private readonly DeviceOutput[][] _outputsByProvider;
    private readonly Dictionary<string, DeviceLighting> _deviceLighting = [];
    private readonly LedColorStore _ledColors = new();
    private DeviceOutput[] _outputs = [];
    private DeviceLighting? _global;
    private bool _isPaused;
    private bool _disposed;
    private long _sequence;
    private int _framesPerSecond = DefaultFramesPerSecond;

    // Effects run on the time of timeProvider, counted from the moment the controller was made.
    public LightingController(IReadOnlyList<IDeviceProvider> providers, TimeProvider? timeProvider = null)
    {
        _providers = providers;
        _time = timeProvider ?? TimeProvider.System;
        _epoch = _time.GetTimestamp();
        _animation = new AnimationLoop(_time, FramePeriodLocked, RenderAnimationFrame);
        _outputsByProvider = [.. providers.Select(_ => Array.Empty<DeviceOutput>())];
        for (var index = 0; index < providers.Count; index++)
        {
            if (providers[index] is IDynamicDeviceProvider dynamicProvider)
            {
                var providerIndex = index;
                // On the thread pool: the provider may raise this while the engine waits for it.
                dynamicProvider.DevicesChanged += (_, _) => _ = Task.Run(() => RefreshAsync(providerIndex));
            }
        }
    }

    // Raised after each render, also while paused, so a preview shows what the lights will show.
    // Listeners read the colors with GetOutput, so nothing is copied while nobody listens.
    public event EventHandler? Rendered;

    // Raised when the lighting is changed through SetLighting, not when it is applied from settings.
    public event EventHandler? LightingChanged;

    // Raised when the device list changed, before the render that gives new devices their lighting.
    public event EventHandler? DevicesChanged;

    public bool IsPaused
    {
        get
        {
            lock (_gate)
            {
                return _isPaused;
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
            lock (_gate)
            {
                _framesPerSecond = Math.Clamp(value, 1, MaxFramesPerSecond);
                _animation.Period = FramePeriodLocked;
            }
        }
    }

    public IReadOnlyList<RgbDevice> Devices
    {
        get
        {
            lock (_gate)
            {
                return [.. _outputs.Select(output => output.Device)];
            }
        }
    }

    public LightingState State
    {
        get
        {
            lock (_gate)
            {
                return new(
                    GlobalLocked,
                    new Dictionary<string, DeviceLighting>(_deviceLighting),
                    _ledColors.Snapshot());
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
            _global = state.Global;
            _deviceLighting.Clear();
            foreach (var (deviceId, lighting) in state.Devices)
            {
                _deviceLighting[deviceId] = lighting;
            }

            _ledColors.Replace(state.LedColors);

            rendered = RenderLocked();
        }

        Publish(rendered);
    }

    // A null deviceId means the global lighting.
    public DeviceLighting GetLighting(string? deviceId)
    {
        lock (_gate)
        {
            if (deviceId is null)
            {
                return GlobalLocked;
            }

            KnownDeviceLocked(deviceId);
            return LightingForLocked(deviceId);
        }
    }

    // A global change also applies to every device with its own lighting, so a global color keeps
    // a device's own brightness and the other way round. Changing one device starts from what
    // that device shows now.
    public void SetLighting(string? deviceId, Func<DeviceLighting, DeviceLighting> change)
    {
        bool rendered;
        lock (_gate)
        {
            if (deviceId is null)
            {
                _global = change(GlobalLocked);
                foreach (var (id, lighting) in _deviceLighting.ToArray())
                {
                    SetDeviceLightingLocked(id, change(lighting));
                }
            }
            else
            {
                KnownDeviceLocked(deviceId);
                var lighting = change(LightingForLocked(deviceId));
                _global = GlobalLocked;
                SetDeviceLightingLocked(deviceId, lighting);
            }

            rendered = RenderLocked();
        }

        Publish(rendered);
        LightingChanged?.Invoke(this, EventArgs.Empty);
    }

    // The colors the per-LED effect shows, one per LED: the stored color, or the device color for
    // an LED without one. That is also where editing them starts.
    public IReadOnlyList<RgbColor> GetLedColors(string deviceId)
    {
        lock (_gate)
        {
            var device = KnownDeviceLocked(deviceId);
            return _ledColors.Resolve(deviceId, device.LedCount, LightingForLocked(deviceId).Color);
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
            var device = KnownDeviceLocked(deviceId);
            ArgumentOutOfRangeException.ThrowIfNegative(firstLed);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(firstLed, device.LedCount);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(colors.Count, device.LedCount - firstLed, nameof(colors));

            _ledColors.Set(deviceId, firstLed, colors);
            _global = GlobalLocked;
            SetDeviceLightingLocked(deviceId, LightingForLocked(deviceId) with { EffectId = EffectIds.PerLed });
            rendered = RenderLocked();
        }

        Publish(rendered);
        LightingChanged?.Invoke(this, EventArgs.Empty);
    }

    // Copies the buffers, so the caller can convert them without holding the lock.
    public LightingOutput GetOutput()
    {
        lock (_gate)
        {
            return new(_sequence, [.. _outputs.Select(output => new DeviceColors(output.Device.Id, [.. output.Buffer]))]);
        }
    }

    // Resuming sends the latest lighting again; pausing stops the animation and leaves the
    // devices as they are.
    public void SetPaused(bool paused)
    {
        bool rendered;
        lock (_gate)
        {
            if (_isPaused == paused)
            {
                return;
            }

            _isPaused = paused;
            if (paused)
            {
                UpdateAnimationLocked();
                return;
            }

            rendered = RenderLocked();
        }

        Publish(rendered);
    }

    // One step of the animation: renders the animated effects and sends only the devices whose
    // colors changed. Allocates nothing, so it can run 60 times a second without feeding the GC.
    // The Rendered listeners run on this thread too; the one of a connected settings app copies
    // the colors for its preview, so that does allocate.
    public void RenderAnimationFrame()
    {
        var rendered = false;
        lock (_gate)
        {
            // A tick that was already on its way when the animation stopped.
            if (!_animation.IsRunning)
            {
                return;
            }

            var time = EffectTime;
            foreach (var output in _outputs)
            {
                if (output.IsAnimated)
                {
                    output.Render(time);
                    rendered |= output.SendIfChanged();
                }
            }

            if (rendered)
            {
                _sequence++;
            }
        }

        Publish(rendered);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _animation.Dispose();
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
                _outputsByProvider[providerIndex] = [.. devices.Select(device => new DeviceOutput(provider, device))];
                _outputs = [.. _outputsByProvider.SelectMany(outputs => outputs)];
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
        if (_global is null)
        {
            return false;
        }

        var time = EffectTime;
        foreach (var output in _outputs)
        {
            var lighting = LightingForLocked(output.Device.Id);
            output.Show(EffectCatalog.Create(lighting.EffectId, lighting.Color, _ledColors.For(output.Device.Id)), lighting.Brightness);
            output.Render(time);
            if (!_isPaused)
            {
                output.Send();
            }
        }

        _sequence++;
        UpdateAnimationLocked();
        return true;
    }

    // Every device runs on the same clock, so the same animated effect stays in step on all of them.
    private TimeSpan EffectTime => _time.GetElapsedTime(_epoch);

    private TimeSpan FramePeriodLocked => TimeSpan.FromSeconds(1.0 / _framesPerSecond);

    private void UpdateAnimationLocked()
    {
        if (!_disposed && !_isPaused && _global is not null && _outputs.Any(output => output.IsAnimated))
        {
            _animation.Start();
        }
        else
        {
            _animation.Stop();
        }
    }

    private void Publish(bool rendered)
    {
        if (rendered)
        {
            Rendered?.Invoke(this, EventArgs.Empty);
        }
    }

    // Until the lighting is applied or first changed, everything counts as the default.
    private DeviceLighting GlobalLocked => _global ?? DeviceLighting.Default;

    private DeviceLighting LightingForLocked(string deviceId) => _deviceLighting.GetValueOrDefault(deviceId, GlobalLocked);

    // A device that ends up equal to the global lighting follows it again, which keeps the
    // settings file to the devices that really differ.
    private void SetDeviceLightingLocked(string deviceId, DeviceLighting lighting)
    {
        if (lighting == GlobalLocked)
        {
            _deviceLighting.Remove(deviceId);
        }
        else
        {
            _deviceLighting[deviceId] = lighting;
        }
    }

    private RgbDevice KnownDeviceLocked(string deviceId) =>
        _outputs.FirstOrDefault(output => output.Device.Id == deviceId)?.Device
            ?? throw new ArgumentException($"Unknown device '{deviceId}'.", nameof(deviceId));
}
