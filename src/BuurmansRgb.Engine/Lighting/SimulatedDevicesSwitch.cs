using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Shows or hides the simulated devices by their mode. In Automatic mode it follows the device list
// of the lighting: once a real device is in it, the simulated devices go, and they only come back
// while this pc never had a real device (HadRealDevices). Every change of the list or the mode
// decides again, from the list and the mode of that moment. Switching makes the lighting fetch the
// simulated devices again, which raises DevicesChanged and so decides once more: the end state
// always follows the last change. The lock keeps the reading and the switching of one decision
// together, so two decisions that cross each other do not make the devices come and go an extra time.
//
// With the status of OpenRGB, Automatic also waits for its first try to connect, and keeps them
// hidden while OpenRGB says it has real devices that are not in the list yet: without that, the
// simulated devices showed for about a second at every start of the engine on a pc with lamps.
//
// HadRealDevices is a fact the switch records, not a choice: it goes to true as soon as a real
// device is in the list, in any mode, and the host keeps it in settings.json so it also counts
// after a restart while OpenRGB is not running yet. Someone with real devices whose OpenRGB went
// away then sees no simulated devices in place of their own. Only the file sets it back.
internal sealed class SimulatedDevicesSwitch : IDisposable
{
    private readonly SwitchableDeviceProvider _simulated;
    private readonly LightingController _lighting;
    private readonly IServerStatusSource? _openRgb;
    private readonly Lock _gate = new();
    private SimulatedDevicesMode _mode = SimulatedDevicesMode.Automatic;
    private ServerStatus _openRgbStatus = ServerStatus.Connecting;
    private bool _openRgbTried;
    private bool _hadRealDevices;

    public SimulatedDevicesSwitch(SwitchableDeviceProvider simulated, LightingController lighting, IServerStatusSource? openRgb = null)
    {
        _simulated = simulated;
        _lighting = lighting;
        _openRgb = openRgb;
        _lighting.DevicesChanged += OnDevicesChanged;
        if (_openRgb is not null)
        {
            // Listening first, so no change gets lost between reading the status and listening.
            _openRgb.StatusChanged += OnOpenRgbStatusChanged;
            lock (_gate)
            {
                ShowOpenRgbStatusLocked(_openRgb.Status);
                UpdateLocked();
            }
        }
    }

    // Raised when Mode got another value; read Mode for the newest one.
    public event EventHandler? ModeChanged;

    // Raised when HadRealDevices got another value; read HadRealDevices for the newest one.
    public event EventHandler? HadRealDevicesChanged;

    public SimulatedDevicesMode Mode
    {
        get
        {
            lock (_gate)
            {
                return _mode;
            }
        }
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown mode for the simulated devices.");
            }

            bool changed;
            bool sawRealDevices;
            lock (_gate)
            {
                changed = _mode != value;
                _mode = value;
                sawRealDevices = UpdateLocked();
            }

            if (changed)
            {
                ModeChanged?.Invoke(this, EventArgs.Empty);
            }

            RaiseIfSawRealDevices(sawRealDevices);
        }
    }

    // Whether a real device was ever in the list on this pc. Set from settings.json at the start
    // and after a change by hand; the switch itself only sets it to true, so false by hand only
    // holds while no real device is in the list: with one, it is true again at once, and
    // HadRealDevicesChanged says so, so the host writes it back.
    public bool HadRealDevices
    {
        get
        {
            lock (_gate)
            {
                return _hadRealDevices;
            }
        }
        set
        {
            bool changed;
            lock (_gate)
            {
                changed = _hadRealDevices != value;
                _hadRealDevices = value;
                changed |= UpdateLocked();
            }

            if (changed)
            {
                HadRealDevicesChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Dispose()
    {
        _lighting.DevicesChanged -= OnDevicesChanged;
        _openRgb?.StatusChanged -= OnOpenRgbStatusChanged;
    }

    private void OnOpenRgbStatusChanged(object? sender, ServerStatus status)
    {
        bool sawRealDevices;
        lock (_gate)
        {
            ShowOpenRgbStatusLocked(status);
            sawRealDevices = UpdateLocked();
        }

        RaiseIfSawRealDevices(sawRealDevices);
    }

    private void ShowOpenRgbStatusLocked(ServerStatus status)
    {
        _openRgbStatus = status;
        _openRgbTried |= status.State != ServerState.Connecting;
    }

    // Real devices may come: the first try is on its way, or OpenRGB has devices that the lighting
    // fetches next (and then decides again).
    private bool ExpectsRealDevicesLocked() =>
        _openRgb is not null
        && (!_openRgbTried || _openRgbStatus is { State: ServerState.Connected, DeviceCount: > 0 });

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        bool sawRealDevices;
        lock (_gate)
        {
            sawRealDevices = UpdateLocked();
        }

        RaiseIfSawRealDevices(sawRealDevices);
    }

    // Decides from the list of now, and records a real device in it. Returns whether that made
    // HadRealDevices true just now, so the caller raises HadRealDevicesChanged outside the lock.
    // Neither the lighting nor the switchable provider calls back into this while holding its own
    // lock, and the lighting fetches the devices again on the thread pool, so this cannot deadlock.
    private bool UpdateLocked()
    {
        var hasRealDevices = _lighting.Devices.Any(device => device.Source == DeviceSource.Hardware);
        var sawRealDevices = hasRealDevices && !_hadRealDevices;
        _hadRealDevices |= hasRealDevices;
        _simulated.IsEnabled = _mode switch
        {
            SimulatedDevicesMode.Always => true,
            SimulatedDevicesMode.Never => false,
            _ => !hasRealDevices && !_hadRealDevices && !ExpectsRealDevicesLocked(),
        };
        return sawRealDevices;
    }

    private void RaiseIfSawRealDevices(bool sawRealDevices)
    {
        if (sawRealDevices)
        {
            HadRealDevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
