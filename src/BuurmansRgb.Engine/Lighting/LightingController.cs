using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Called from the tray thread and from IPC threads; the lock guards all state. Events are
// raised outside the lock, so a listener can see a newer output than the render that raised
// the event: LightingOutput.Sequence orders them.
internal sealed class LightingController(IReadOnlyList<IDeviceProvider> providers)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, DeviceLighting> _deviceLighting = [];
    private IReadOnlyList<DeviceOutput> _outputs = [];
    private DeviceLighting? _global;
    private bool _isPaused;
    private long _sequence;

    // Raised after each render, also while paused, so a preview shows what the lights will show.
    // Listeners read the colors with GetOutput, so nothing is copied while nobody listens.
    public event EventHandler? Rendered;

    // Raised when the lighting is changed through SetLighting, not when it is applied from settings.
    public event EventHandler? LightingChanged;

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
                return new(GlobalLocked, new Dictionary<string, DeviceLighting>(_deviceLighting));
            }
        }
    }

    public async Task DiscoverAsync(CancellationToken ct)
    {
        List<DeviceOutput> outputs = [];
        foreach (var provider in providers)
        {
            foreach (var device in await provider.DiscoverAsync(ct))
            {
                outputs.Add(new(provider, device, new RgbColor[device.LedCount]));
            }
        }

        lock (_gate)
        {
            _outputs = outputs;
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

            EnsureKnownDevice(deviceId);
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
                EnsureKnownDevice(deviceId);
                var lighting = change(LightingForLocked(deviceId));
                _global = GlobalLocked;
                SetDeviceLightingLocked(deviceId, lighting);
            }

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

    // Resuming sends the latest lighting again; pausing leaves the devices as they are.
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
                return;
            }

            rendered = RenderLocked();
        }

        Publish(rendered);
    }

    // Returns false when there is no lighting to render yet.
    private bool RenderLocked()
    {
        if (_global is null)
        {
            return false;
        }

        foreach (var (provider, device, buffer) in _outputs)
        {
            buffer.AsSpan().Fill(LightingForLocked(device.Id).OutputColor);
            if (!_isPaused)
            {
                provider.SetLeds(device.Id, buffer);
            }
        }

        _sequence++;
        return true;
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

    private void EnsureKnownDevice(string deviceId)
    {
        if (!_outputs.Any(output => output.Device.Id == deviceId))
        {
            throw new ArgumentException($"Unknown device '{deviceId}'.", nameof(deviceId));
        }
    }

    private sealed record DeviceOutput(IDeviceProvider Provider, RgbDevice Device, RgbColor[] Buffer);
}
