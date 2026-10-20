using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// The outputs of every device, kept per provider so one provider's list can be replaced on its
// own. Not thread-safe: LightingController uses it under its lock.
internal sealed class DeviceOutputs
{
    private readonly DeviceOutput[][] _byProvider;

    public DeviceOutputs(int providerCount) =>
        _byProvider = [.. Enumerable.Range(0, providerCount).Select(_ => Array.Empty<DeviceOutput>())];

    // An array, so the render loop can walk it without allocating an enumerator.
    public DeviceOutput[] All { get; private set; } = [];

    public DeviceOutput? Find(string deviceId) => All.FirstOrDefault(output => output.Device.Id == deviceId);

    // Gives the devices of one provider new outputs; a device that was there already keeps its
    // effect clock. Returns the devices it had before, so the caller can compare their zones with
    // the new ones.
    public IReadOnlyList<RgbDevice> Replace(int providerIndex, IDeviceProvider provider, IReadOnlyList<RgbDevice> devices)
    {
        var previous = _byProvider[providerIndex];
        var clocks = previous.ToDictionary(output => output.Device.Id, output => output.Clock);
        _byProvider[providerIndex] = [.. devices.Select(device => new DeviceOutput(provider, device, clocks.GetValueOrDefault(device.Id)))];
        All = [.. _byProvider.SelectMany(outputs => outputs)];
        return [.. previous.Select(output => output.Device)];
    }
}
