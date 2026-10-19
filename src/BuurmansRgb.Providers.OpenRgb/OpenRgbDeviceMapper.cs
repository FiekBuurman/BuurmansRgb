using System.Security.Cryptography;
using System.Text;
using BuurmansRgb.Core;
using OpenRgbDeviceType = OpenRGB.NET.DeviceType;

namespace BuurmansRgb.Providers.OpenRgb;

// A device as the engine sees it, plus where to send its colors on the server.
internal sealed record MappedDevice(RgbDevice Device, int Index);

internal static class OpenRgbDeviceMapper
{
    public const string IdPrefix = "openrgb-";

    private const int MaxSlugLength = 40;

    // The modes in which OpenRGB takes a color per LED from the SDK, in order of preference.
    private static readonly string[] DirectModeNames = ["Direct", "Custom"];

    public static IReadOnlyList<MappedDevice> Map(IEnumerable<OpenRgbDeviceData> devices)
    {
        HashSet<string> usedIds = [];
        List<MappedDevice> mapped = [];
        foreach (var device in devices)
        {
            var id = UniqueId(CreateId(device), usedIds);
            var zones = device.Zones.Select(zone => new RgbZone(zone.Name, zone.LedCount, IsResizable(zone))).ToArray();
            mapped.Add(new(new RgbDevice(id, device.Name, ToDeviceType(device.Type), zones, device.LedCount), device.Index));
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

    private static bool IsResizable(OpenRgbZoneData zone) => zone.MinLedCount != zone.MaxLedCount;

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
