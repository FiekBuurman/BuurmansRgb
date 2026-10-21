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

    // A fan in its square frame: the LED ring, the hub and four blades.
    public static Geometry Fan { get; } = Create(
        "M1.5,1.5 H16.5 V16.5 H1.5 Z M3,9 A6,6 0 1 0 15,9 A6,6 0 1 0 3,9 Z M7.5,9 A1.5,1.5 0 1 0 10.5,9 A1.5,1.5 0 1 0 7.5,9 Z M9,7.5 V4.5 M10.5,9 H13.5 M9,10.5 V13.5 M7.5,9 H4.5");

    // A strip with three LEDs on it.
    public static Geometry LedStrip { get; } = Create(
        "M0.5,6.5 H17.5 V11.5 H0.5 Z M2.5,9 A1,1 0 1 0 4.5,9 A1,1 0 1 0 2.5,9 Z M8,9 A1,1 0 1 0 10,9 A1,1 0 1 0 8,9 Z M13.5,9 A1,1 0 1 0 15.5,9 A1,1 0 1 0 13.5,9 Z");

    // A tower with a light bar along its front and a fan.
    public static Geometry Case { get; } = Create(
        "M4.5,0.5 H13.5 V17.5 H4.5 Z M6.5,2.5 V15.5 M8.5,11 A2,2 0 1 0 12.5,11 A2,2 0 1 0 8.5,11 Z M9.5,3.5 H11.5");

    // A keyboard with two rows of keys and a space bar.
    public static Geometry Keyboard { get; } = Create(
        "M0.5,4.5 H17.5 V13.5 H0.5 Z M2,6.5 H4 M5,6.5 H7 M8,6.5 H10 M11,6.5 H13 M14,6.5 H16 M3,8.5 H5 M6,8.5 H8 M9,8.5 H11 M12,8.5 H14 M5,11.5 H13");

    // A mouse with its two buttons.
    public static Geometry Mouse { get; } = Create(
        "M9.5,1.5 A5,5 0 0 0 4.5,6.5 V11.5 A5,5 0 0 0 14.5,11.5 V6.5 A5,5 0 0 0 9.5,1.5 Z M9.5,1.5 V7.5 M4.5,7.5 H14.5");

    // A headband with two ear cups and a microphone.
    public static Geometry Headset { get; } = Create(
        "M3.5,9.5 V8 A5.5,5.5 0 0 1 14.5,8 V9.5 M2.5,9.5 H5.5 V14.5 H2.5 Z M12.5,9.5 H15.5 V14.5 H12.5 Z M4.5,14.5 V16.5 H8.5");

    // A mat with a mouse on it.
    public static Geometry Mousemat { get; } = Create(
        "M0.5,3.5 H17.5 V14.5 H0.5 Z M11.5,6.5 A2,2 0 0 1 13.5,8.5 V10.5 A2,2 0 0 1 9.5,10.5 V8.5 A2,2 0 0 1 11.5,6.5 Z M11.5,6.5 V8.5");

    // A headset hanging on a stand with a foot.
    public static Geometry HeadsetStand { get; } = Create(
        "M3.5,10.5 V8 A5,5 0 0 1 13.5,8 V10.5 M2.5,10.5 H4.5 V13.5 H2.5 Z M12.5,10.5 H14.5 V13.5 H12.5 Z M8.5,3 V15.5 M4.5,15.5 H12.5");

    // A cabinet with a tweeter above a woofer.
    public static Geometry Speaker { get; } = Create(
        "M3.5,1.5 H14.5 V16.5 H3.5 Z M8,5 A1,1 0 1 0 10,5 A1,1 0 1 0 8,5 Z M5.5,11 A3.5,3.5 0 1 0 12.5,11 A3.5,3.5 0 1 0 5.5,11 Z");

    // A controller with two grips, a direction pad and two buttons.
    public static Geometry Gamepad { get; } = Create(
        "M5.5,4.5 H12.5 A4,4 0 0 1 16.5,8.5 V12 A2,2 0 0 1 13,13.5 L11.5,11.5 H6.5 L5,13.5 A2,2 0 0 1 1.5,12 V8.5 A4,4 0 0 1 5.5,4.5 Z M5.5,7 V10 M4,8.5 H7 M11.5,9.5 A1,1 0 1 0 13.5,9.5 A1,1 0 1 0 11.5,9.5 Z M12.5,7 A0.5,0.5 0 1 0 13.5,7 A0.5,0.5 0 1 0 12.5,7 Z");

    // A microphone in its holder on a stand.
    public static Geometry Microphone { get; } = Create(
        "M7.5,4 A2,2 0 0 1 11.5,4 V8.5 A2,2 0 0 1 7.5,8.5 Z M5.5,8.5 A4,4 0 0 0 13.5,8.5 M9.5,12.5 V15.5 M6.5,15.5 H12.5");

    // A gaming keypad: two rows of keys above a palm rest.
    public static Geometry Keypad { get; } = Create(
        "M1.5,2.5 H16.5 V10.5 H1.5 Z M3.5,5.5 H5.5 M8,5.5 H10 M12.5,5.5 H14.5 M3.5,7.5 H5.5 M8,7.5 H10 M12.5,7.5 H14.5 M5.5,10.5 V15.5 H12.5 V10.5");

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
        DeviceType.Fan => Fan,
        DeviceType.LedStrip => LedStrip,
        DeviceType.Case => Case,
        DeviceType.Keyboard => Keyboard,
        DeviceType.Mouse => Mouse,
        DeviceType.Headset => Headset,
        DeviceType.Mousemat => Mousemat,
        DeviceType.HeadsetStand => HeadsetStand,
        DeviceType.Speaker => Speaker,
        DeviceType.Gamepad => Gamepad,
        DeviceType.Microphone => Microphone,
        DeviceType.Keypad => Keypad,
        _ => Device,
    };

    private static Geometry Create(string path)
    {
        var geometry = Geometry.Parse(path);
        geometry.Freeze();
        return geometry;
    }
}
