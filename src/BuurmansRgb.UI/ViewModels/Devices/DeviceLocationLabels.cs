using System.Text.RegularExpressions;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

// A short label for devices that look the same in the list, such as the RAM sticks of a kit or two
// graphics cards of one model: only the part of their location that differs, such as "Adres 0x52".
// It tells them apart; which one is which in the pc shows with "Knipperen". A device that has
// no twin gets no label.
internal static partial class DeviceLocationLabels
{
    // Device ids with their label.
    public static IReadOnlyDictionary<string, string> For(IReadOnlyList<DeviceInfo> devices)
    {
        Dictionary<string, string> labels = [];
        var twins = devices
            .Where(device => device.Location is not null)
            .GroupBy(device => (device.Type, BaseName(device.Name)))
            .Where(group => group.Count() > 1);
        foreach (var group in twins)
        {
            var label = LabelFor([.. group]);
            if (label is null)
            {
                continue;
            }

            foreach (var device in group)
            {
                labels[device.Id] = label(device);
            }
        }

        return labels;
    }

    // The address when that differs between the twins, else the port (the bus of each graphics
    // card); null when both are the same.
    private static Func<DeviceInfo, string>? LabelFor(IReadOnlyList<DeviceInfo> twins)
    {
        if (AllDiffer(twins, device => device.Location!.Address))
        {
            return device => UiText.Format(Strings.DeviceLocationAddress, device.Location!.Address);
        }

        if (AllDiffer(twins, device => device.Location!.Port))
        {
            return device => UiText.Format(Strings.DeviceLocationPort, device.Location!.Port);
        }

        return null;
    }

    private static bool AllDiffer(IReadOnlyList<DeviceInfo> twins, Func<DeviceInfo, string?> part) =>
        twins.All(device => part(device) is not null) && twins.Select(part).Distinct().Count() == twins.Count;

    // OpenRGB names the devices of one model the same; the provider then numbers them ("ENE DRAM 2").
    private static string BaseName(string name) => TrailingNumber().Replace(name, string.Empty);

    [GeneratedRegex(@" \d+$")]
    private static partial Regex TrailingNumber();
}
