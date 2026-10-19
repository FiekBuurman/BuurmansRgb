namespace BuurmansRgb.Core;

// A provider whose resizable zones can get another length, such as an addressable header that
// cannot tell how many LEDs the strip on it has. The engine checks the zone and the length
// against the zone's range first. The new length may take effect in the background; the provider
// then raises DevicesChanged and DiscoverAsync returns the device with its new zones. Like SetLeds
// it must not wait.
public interface IResizableDeviceProvider : IDynamicDeviceProvider
{
    void ResizeZone(string deviceId, int zoneIndex, int ledCount);
}
