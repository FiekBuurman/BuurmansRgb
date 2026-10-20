using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// Line icons on an 18 by 18 grid, drawn with a 1 pixel stroke (the .5 coordinates keep the lines
// sharp). A type without an icon of its own gets the general device icon.
public static class DeviceIcons
{
    public const double Size = 18.0;

    public static Geometry AllDevices { get; } = Create(
        "M2.5,2.5 H7.5 V7.5 H2.5 Z M10.5,2.5 H15.5 V7.5 H10.5 Z M2.5,10.5 H7.5 V15.5 H2.5 Z M10.5,10.5 H15.5 V15.5 H10.5 Z");

    // A board with the CPU socket, a RAM slot and a PCIe slot.
    public static Geometry Motherboard { get; } = Create(
        "M2.5,2.5 H15.5 V15.5 H2.5 Z M5.5,5.5 H10.5 V10.5 H5.5 Z M13.5,4.5 V10.5 M4.5,13.5 H11.5");

    // A stick with three chips and the notch between its contacts.
    public static Geometry Ram { get; } = Create(
        "M1.5,5.5 H16.5 V12.5 H1.5 Z M3.5,7.5 H5.5 V10.5 H3.5 Z M7.5,7.5 H10.5 V10.5 H7.5 Z M12.5,7.5 H14.5 V10.5 H12.5 Z M2.5,14.5 H8 M10,14.5 H15.5");

    // A card with two fans, its slot bracket on the left and the PCIe contacts below.
    public static Geometry Gpu { get; } = Create(
        "M1.5,2.5 V15.5 M1.5,4.5 H16.5 V12.5 H1.5 M4.5,8.5 A2,2 0 1 0 8.5,8.5 A2,2 0 1 0 4.5,8.5 Z M9.5,8.5 A2,2 0 1 0 13.5,8.5 A2,2 0 1 0 9.5,8.5 Z M5.5,12.5 V14.5 H12.5 V12.5");

    // A housing with one LED.
    public static Geometry Device { get; } = Create(
        "M3.5,3.5 H14.5 V14.5 H3.5 Z M7,9 A2,2 0 1 0 11,9 A2,2 0 1 0 7,9 Z");

    // A null type stands for "all devices".
    public static Geometry For(DeviceType? type) => type switch
    {
        null => AllDevices,
        DeviceType.Motherboard => Motherboard,
        DeviceType.Ram => Ram,
        DeviceType.Gpu => Gpu,
        _ => Device,
    };

    private static Geometry Create(string path)
    {
        var geometry = Geometry.Parse(path);
        geometry.Freeze();
        return geometry;
    }
}
