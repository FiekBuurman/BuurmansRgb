using System.Security.Cryptography;
using System.Text;
using BuurmansRgb.Core;
using OpenRgbDeviceType = OpenRGB.NET.DeviceType;

namespace BuurmansRgb.Providers.OpenRgb;

internal static class OpenRgbDeviceMapper
{
    private const string IdPrefix = "openrgb-";

    private const int MaxSlugLength = 40;

    // OpenRGB 0.9 added storage (14) and case (15) before its unknown type (now 19), but
    // OpenRGB.NET 3.1.1 still knows 14 as Unknown and has no name for case; it passes the
    // number on as it is.
    public const OpenRgbDeviceType CaseType = (OpenRgbDeviceType)15;

    // The modes in which OpenRGB takes a color per LED from the SDK, in order of preference.
    private static readonly string[] DirectModeNames = ["Direct", "Custom"];

    public static IReadOnlyList<MappedDevice> Map(IEnumerable<OpenRgbDeviceData> devices)
    {
        var list = devices.ToArray();
        var nameCounts = list.CountBy(device => device.Name).ToDictionary();
        Dictionary<string, int> nameNumbers = [];
        HashSet<string> usedIds = [];
        List<MappedDevice> mapped = [];
        foreach (var device in list)
        {
            var id = UniqueId(CreateId(device), usedIds);
            var name = nameCounts[device.Name] > 1 ? NumberedName(device.Name, nameNumbers) : device.Name;
            var zones = device.Zones.Select(ToZone).ToArray();
            var mappedDevice = new RgbDevice(id, name, ToDeviceType(device.Type), zones, device.LedCount) { Source = DeviceSource.OpenRgb };
            mapped.Add(new(mappedDevice, device.Index));
        }

        return mapped;
    }

    public static bool HasDirectMode(OpenRgbDeviceData device) =>
        device.ModeNames.Any(mode => DirectModeNames.Contains(mode, StringComparer.OrdinalIgnoreCase));

    public static DeviceType ToDeviceType(OpenRgbDeviceType type) => type switch
    {
        OpenRgbDeviceType.Motherboard => DeviceType.Motherboard,
        OpenRgbDeviceType.Dram => DeviceType.Ram,
        OpenRgbDeviceType.Gpu => DeviceType.Gpu,
        OpenRgbDeviceType.Cooler => DeviceType.Fan,
        OpenRgbDeviceType.Ledstrip => DeviceType.LedStrip,
        CaseType => DeviceType.Case,
        OpenRgbDeviceType.Keyboard => DeviceType.Keyboard,
        OpenRgbDeviceType.Mouse => DeviceType.Mouse,
        OpenRgbDeviceType.Headset => DeviceType.Headset,
        _ => DeviceType.Other,
    };

    // The id ends up in settings.json, so it must survive a restart of OpenRGB or the pc. The
    // index does not, but name, location (USB path or SMBus address) and serial do. The readable
    // part helps when editing the file; the hash tells identical models apart.
    public static string CreateId(OpenRgbDeviceData device)
    {
        var key = string.Join('\n', device.Name, device.Location, device.Serial);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)), 0, 4);
        var slug = Slug(device.Name);
        return slug.Length == 0 ? $"{IdPrefix}{hash}" : $"{IdPrefix}{slug}-{hash}";
    }

    private static RgbZone ToZone(OpenRgbZoneData zone) => new(zone.Name, zone.LedCount, zone.MinLedCount, zone.MaxLedCount)
    {
        Matrix = zone.Matrix is { Cells.Count: > 0 } matrix ? matrix : null,
    };

    // OpenRGB names every RAM stick of a kit the same. A number in server order tells them apart
    // in the device list; the id does not use it.
    private static string NumberedName(string name, Dictionary<string, int> numbers)
    {
        var number = numbers.GetValueOrDefault(name) + 1;
        numbers[name] = number;
        return $"{name} {number}";
    }

    // Two devices with the same name, location and serial still get their own id, in server order.
    private static string UniqueId(string id, HashSet<string> usedIds)
    {
        var unique = id;
        for (var number = 2; !usedIds.Add(unique); number++)
        {
            unique = $"{id}-{number}";
        }

        return unique;
    }

    private static string Slug(string name)
    {
        var slug = new StringBuilder(MaxSlugLength);
        foreach (var character in name)
        {
            if (slug.Length == MaxSlugLength)
            {
                break;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                slug.Append(char.ToLowerInvariant(character));
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        return slug.ToString().TrimEnd('-');
    }
}
