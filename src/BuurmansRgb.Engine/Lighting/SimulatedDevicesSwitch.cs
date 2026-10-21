using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Shows or hides the simulated devices by their mode. In Automatic mode it follows the device list
// of the lighting: once a real device is in it, the simulated devices go, and when the last real
// device goes they come back. Every change of the list or the mode decides again, from the list and
// the mode of that moment. Switching makes the lighting fetch the simulated devices again, which
// raises DevicesChanged and so decides once more: the end state always follows the last change.
// The lock keeps the reading and the switching of one decision together, so two decisions that
// cross each other do not make the devices come and go an extra time.
//
// With the status of OpenRGB, Automatic also waits for its first try to connect, and keeps them
// hidden while OpenRGB says it has real devices that are not in the list yet: without that, the
// simulated devices showed for about a second at every start of the engine on a pc with lamps.
internal sealed class SimulatedDevicesSwitch : IDisposable
{
    private readonly SwitchableDeviceProvider _simulated;
    private readonly LightingController _lighting;
    private readonly IServerStatusSource? _openRgb;
    private readonly Lock _gate = new();
    private SimulatedDevicesMode _mode = SimulatedDevicesMode.Automatic;
    private ServerStatus _openRgbStatus = ServerStatus.Connecting;
    private bool _openRgbTried;

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
            lock (_gate)
            {
                changed = _mode != value;
                _mode = value;
                UpdateLocked();
            }

            if (changed)
            {
                ModeChanged?.Invoke(this, EventArgs.Empty);
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
        lock (_gate)
        {
            ShowOpenRgbStatusLocked(status);
            UpdateLocked();
        }
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
        lock (_gate)
        {
            UpdateLocked();
        }
    }

    // Neither the lighting nor the switchable provider calls back into this while holding its own
    // lock, and the lighting fetches the devices again on the thread pool, so this cannot deadlock.
    private void UpdateLocked() => _simulated.IsEnabled = _mode switch
    {
        SimulatedDevicesMode.Always => true,
        SimulatedDevicesMode.Never => false,
        _ => !_lighting.Devices.Any(device => device.Source == DeviceSource.Hardware) && !ExpectsRealDevicesLocked(),
    };
}
