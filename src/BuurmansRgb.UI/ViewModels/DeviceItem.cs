using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

// A null DeviceId and Type stand for "all devices". TypeName is null for a type without a Dutch
// name in Strings.resx ("DeviceType_<type>") yet. Group is the heading the list shows the device
// under, such as the simulated devices; "all devices" has none. LocationLabel tells twins apart
// (DeviceLocationLabels) and shows under the device; LocationToolTip holds the whole location.
public sealed record DeviceItem(
    string? DeviceId,
    string Name,
    string Details,
    DeviceType? Type = null,
    string? TypeName = null,
    string? Group = null,
    string? LocationLabel = null,
    string? LocationToolTip = null)
{
    public bool ShowsLocationLabel => LocationLabel is not null;

    public static DeviceItem AllDevices(int deviceCount) =>
        new(null, Strings.AllDevices, UiText.Format(Strings.AllDevicesDetails, deviceCount));

    public static DeviceItem For(DeviceInfo device, string? locationLabel = null)
    {
        var typeName = TypeNameOf(device.Type);
        var leds = device.Zones.Count > 1
            ? UiText.Format(Strings.LedCountInZones, device.LedCount, device.Zones.Count)
            : UiText.LedCount(device.LedCount);
        var details = typeName is null ? leds : UiText.Format(Strings.DeviceDetailsWithType, typeName, leds);
        return new(device.Id, device.Name, details, device.Type, typeName, UiText.Lookup("DeviceSource", device.Source),
            locationLabel, ToolTipOf(device.Location));
    }

    private static string? TypeNameOf(DeviceType type) => UiText.Lookup("DeviceType", type);

    private static string? ToolTipOf(DeviceLocation? location) => location switch
    {
        null => null,
        { Address: { } address } => UiText.Format(Strings.DeviceLocationToolTipWithAddress, location.Port, address),
        _ => UiText.Format(Strings.DeviceLocationToolTip, location.Port),
    };
}
