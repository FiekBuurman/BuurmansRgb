using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

// A null DeviceId and Type stand for "all devices". TypeName is null for a type without a Dutch
// name in Strings.resx ("DeviceType_<type>") yet. Group is the heading the list shows the device
// under, such as the simulated devices; "all devices" has none. Location says where the device is
// connected, null when unknown; the list shows it under the device when it holds an address, such
// as the slot of a RAM stick, and otherwise only as a tooltip, since a USB path means little.
public sealed record DeviceItem(
    string? DeviceId,
    string Name,
    string Details,
    DeviceType? Type = null,
    string? TypeName = null,
    string? Group = null,
    string? Location = null,
    bool ShowsLocation = false)
{
    public string? LocationToolTip => Location is null ? null : UiText.Format(Strings.DeviceLocationToolTip, Location);

    public static DeviceItem AllDevices(int deviceCount) =>
        new(null, Strings.AllDevices, UiText.Format(Strings.AllDevicesDetails, deviceCount));

    public static DeviceItem For(DeviceInfo device)
    {
        var typeName = TypeNameOf(device.Type);
        var leds = device.Zones.Count > 1
            ? UiText.Format(Strings.LedCountInZones, device.LedCount, device.Zones.Count)
            : UiText.LedCount(device.LedCount);
        var details = typeName is null ? leds : UiText.Format(Strings.DeviceDetailsWithType, typeName, leds);
        return new(device.Id, device.Name, details, device.Type, typeName, UiText.Lookup("DeviceSource", device.Source),
            LocationOf(device.Location), device.Location?.Address is not null);
    }

    private static string? TypeNameOf(DeviceType type) => UiText.Lookup("DeviceType", type);

    private static string? LocationOf(DeviceLocation? location) => location switch
    {
        null => null,
        { Address: { } address } => UiText.Format(Strings.DeviceLocationAddress, address, location.Port),
        _ => location.Port,
    };
}
