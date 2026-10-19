namespace BuurmansRgb.Core;

// A provider whose devices can come and go while the engine runs, for example because a server
// stops. DevicesChanged may be raised on any thread; DiscoverAsync then returns the new list.
// SetLeds ignores a device that is gone, because the caller may not have seen the change yet.
public interface IDynamicDeviceProvider : IDeviceProvider
{
    event EventHandler? DevicesChanged;
}
