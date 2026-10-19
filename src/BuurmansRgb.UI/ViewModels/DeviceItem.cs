using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

// A null DeviceId and Type stand for "all devices". TypeName is null for a type without a Dutch
// name in Strings.resx ("DeviceType_<type>") yet.
public sealed record DeviceItem(string? DeviceId, string Name, string Details, DeviceType? Type = null, string? TypeName = null)
{
    public static DeviceItem AllDevices(int deviceCount) =>
        new(null, Strings.AllDevices, Format(Strings.AllDevicesDetails, deviceCount));

    public static DeviceItem For(DeviceInfo device)
    {
        var typeName = TypeNameOf(device.Type);
        var leds = device.Zones.Count > 1
            ? Format(Strings.DeviceDetailsZones, device.LedCount, device.Zones.Count)
            : Format(Strings.DeviceDetails, device.LedCount);
        var details = typeName is null ? leds : Format(Strings.DeviceDetailsWithType, typeName, leds);
        return new(device.Id, device.Name, details, device.Type, typeName);
    }

    public static string? TypeNameOf(DeviceType type) =>
        Strings.ResourceManager.GetString($"DeviceType_{type}", Strings.Culture);

    private static string Format(string format, params ReadOnlySpan<object?> values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
