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
internal sealed class SimulatedDevicesSwitch : IDisposable
{
    private readonly SwitchableDeviceProvider _simulated;
    private readonly LightingController _lighting;
    private readonly Lock _gate = new();
    private SimulatedDevicesMode _mode = SimulatedDevicesMode.Automatic;

    public SimulatedDevicesSwitch(SwitchableDeviceProvider simulated, LightingController lighting)
    {
        _simulated = simulated;
        _lighting = lighting;
        _lighting.DevicesChanged += OnDevicesChanged;
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

    public void Dispose() => _lighting.DevicesChanged -= OnDevicesChanged;

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
        _ => !_lighting.Devices.Any(device => device.Source == DeviceSource.Hardware),
    };
}
