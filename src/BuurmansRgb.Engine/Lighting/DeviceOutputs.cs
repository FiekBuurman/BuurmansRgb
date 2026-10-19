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

    // Gives the devices of one provider new outputs. A zone that got another length, here or in
    // the provider's own app, shifts the LEDs after it: the stored per-LED colors move along, so
    // every zone keeps its own colors. Returns true when any colors moved.
    public bool Replace(int providerIndex, IDeviceProvider provider, IReadOnlyList<RgbDevice> devices, LedColorStore ledColors)
    {
        var previous = _byProvider[providerIndex];
        var moved = false;
        foreach (var device in devices)
        {
            if (previous.FirstOrDefault(output => output.Device.Id == device.Id) is { } old)
            {
                moved |= ledColors.MoveWithZones(device.Id, old.Device.Zones, device.Zones);
            }
        }

        _byProvider[providerIndex] = [.. devices.Select(device => new DeviceOutput(provider, device))];
        All = [.. _byProvider.SelectMany(outputs => outputs)];
        return moved;
    }
}
