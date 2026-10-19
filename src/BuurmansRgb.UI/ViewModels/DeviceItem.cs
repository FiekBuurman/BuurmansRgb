using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

// A null DeviceId and Type stand for "all devices". TypeName is null for a type without a Dutch
// name in Strings.resx ("DeviceType_<type>") yet.
public sealed record DeviceItem(string? DeviceId, string Name, string Details, DeviceType? Type = null, string? TypeName = null)
{
    public static DeviceItem AllDevices(int deviceCount) =>
        new(null, Strings.AllDevices, UiText.Format(Strings.AllDevicesDetails, deviceCount));

    public static DeviceItem For(DeviceInfo device)
    {
        var typeName = TypeNameOf(device.Type);
        var leds = device.Zones.Count > 1
            ? UiText.Format(Strings.LedCountInZones, device.LedCount, device.Zones.Count)
            : UiText.Format(Strings.LedCount, device.LedCount);
        var details = typeName is null ? leds : UiText.Format(Strings.DeviceDetailsWithType, typeName, leds);
        return new(device.Id, device.Name, details, device.Type, typeName);
    }

    public static string? TypeNameOf(DeviceType type) =>
        Strings.ResourceManager.GetString($"DeviceType_{type}", Strings.Culture);
}
