using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Called from the tray thread and from IPC threads; the lock guards all state. Events are
// raised outside the lock, so a frame can arrive late: LightingFrame.Sequence orders them.
internal sealed class LightingController(IReadOnlyList<IDeviceProvider> providers)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, DeviceLighting> _deviceLighting = [];
    private IReadOnlyList<DeviceOutput> _outputs = [];
    private DeviceLighting? _global;
    private bool _isPaused;
    private long _frameSequence;

    // Raised after each render, also while paused, so a preview shows what the lights will show.
    public event EventHandler<LightingFrame>? FrameRendered;

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
        LightingFrame? frame;
        lock (_gate)
        {
            _global = state.Global;
            _deviceLighting.Clear();
            foreach (var (deviceId, lighting) in state.Devices)
            {
                _deviceLighting[deviceId] = lighting;
            }

            frame = RenderLocked();
        }

        Publish(frame);
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
        LightingFrame? frame;
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

            frame = RenderLocked();
        }

        Publish(frame);
        LightingChanged?.Invoke(this, EventArgs.Empty);
    }

    public LightingFrame GetFrame()
    {
        lock (_gate)
        {
            return CreateFrameLocked();
        }
    }

    // Resuming sends the latest lighting again; pausing leaves the devices as they are.
    public void SetPaused(bool paused)
    {
        LightingFrame? frame;
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

            frame = RenderLocked();
        }

        Publish(frame);
    }

    // Returns the frame to publish, or null when nothing was rendered or nobody listens.
    private LightingFrame? RenderLocked()
    {
        if (_global is null)
        {
            return null;
        }

        foreach (var (provider, device, buffer) in _outputs)
        {
            buffer.AsSpan().Fill(LightingForLocked(device.Id).OutputColor);
            if (!_isPaused)
            {
                provider.SetLeds(device.Id, buffer);
            }
        }

        _frameSequence++;
        return FrameRendered is null ? null : CreateFrameLocked();
    }

    private LightingFrame CreateFrameLocked() =>
        new(_frameSequence, [.. _outputs.Select(output => DeviceFrame.FromColors(output.Device.Id, output.Buffer))]);

    private void Publish(LightingFrame? frame)
    {
        if (frame is not null)
        {
            FrameRendered?.Invoke(this, frame);
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
