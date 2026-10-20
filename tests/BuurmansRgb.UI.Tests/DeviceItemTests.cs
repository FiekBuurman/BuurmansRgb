using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class DeviceItemTests
{
    // The Dutch name of every device type, or null while it has none in Strings.resx. A new type, a
    // renamed type or a mistyped "DeviceType_<type>" key makes TypeName_OfEveryDeviceType fail.
    private static readonly Dictionary<DeviceType, string?> DutchTypeNames = new()
    {
        [DeviceType.Motherboard] = "Moederbord",
        [DeviceType.Ram] = "RAM",
        [DeviceType.Gpu] = "Videokaart",
        [DeviceType.Fan] = "Ventilator",
        [DeviceType.LedStrip] = "LED-strip",
        [DeviceType.Case] = "Behuizing",
        [DeviceType.Keyboard] = "Toetsenbord",
        [DeviceType.Mouse] = "Muis",
        [DeviceType.Headset] = "Headset",
        [DeviceType.Other] = null,
    };

    [Fact]
    public void TypeName_OfEveryDeviceType()
    {
        Assert.Equal(Enum.GetValues<DeviceType>(), DutchTypeNames.Keys.Order());
        Assert.All(DutchTypeNames, pair =>
            Assert.Equal(pair.Value, DeviceItem.For(TestDevices.Create("id", "Device", pair.Key, 1)).TypeName));
    }

    [Fact]
    public void For_MotherboardWithZones_ShowsTypeLedsAndZones()
    {
        var motherboard = new DeviceInfo("board", "ASUS TUF GAMING B650-PLUS", DeviceType.Motherboard, 7,
            [RgbZone.Fixed("Mainboard", 6), RgbZone.Fixed("RGB header", 1), new RgbZone("ARGB header", 0, 0, 120)]) { Source = DeviceSource.Hardware };

        var item = DeviceItem.For(motherboard);

        Assert.Equal("board", item.DeviceId);
        Assert.Equal("ASUS TUF GAMING B650-PLUS", item.Name);
        Assert.Equal(DeviceType.Motherboard, item.Type);
        Assert.Equal("Moederbord", item.TypeName);
        Assert.Equal("Moederbord · 7 LED's in 3 zones", item.Details);
    }

    [Fact]
    public void For_RamStickWithOneZone_ShowsTypeAndLeds()
    {
        var item = DeviceItem.For(TestDevices.Create("ram", "ENE DRAM", DeviceType.Ram, 8));

        Assert.Equal("RAM", item.TypeName);
        Assert.Equal("RAM · 8 LED's", item.Details);
    }

    [Fact]
    public void For_GpuWithLightBarAndLogo_ShowsTypeLedsAndZones()
    {
        var gpu = new DeviceInfo("gpu", "ASUS ROG STRIX RTX 4080", DeviceType.Gpu, 13,
            [RgbZone.Fixed("Light bar", 12), RgbZone.Fixed("Logo", 1)]) { Source = DeviceSource.Hardware };

        var item = DeviceItem.For(gpu);

        Assert.Equal(DeviceType.Gpu, item.Type);
        Assert.Equal("Videokaart", item.TypeName);
        Assert.Equal("Videokaart · 13 LED's in 2 zones", item.Details);
    }

    [Fact]
    public void For_FanWithOneRing_ShowsTypeAndLeds()
    {
        var item = DeviceItem.For(TestDevices.Create("fan", "Fan", DeviceType.Fan, 12));

        Assert.Equal("Ventilator", item.TypeName);
        Assert.Equal("Ventilator · 12 LED's", item.Details);
    }

    [Fact]
    public void For_KeyboardWithOneZone_ShowsTypeAndLeds()
    {
        var item = DeviceItem.For(TestDevices.Create("keyboard", "Keyboard", DeviceType.Keyboard, 104));

        Assert.Equal("Toetsenbord", item.TypeName);
        Assert.Equal("Toetsenbord · 104 LED's", item.Details);
    }

    [Fact]
    public void For_MouseWithZones_ShowsTypeLedsAndZones()
    {
        var mouse = new DeviceInfo("mouse", "Logitech G502 HERO", DeviceType.Mouse, 2,
            [RgbZone.Fixed("Logo", 1), RgbZone.Fixed("DPI", 1)]) { Source = DeviceSource.Hardware };

        var item = DeviceItem.For(mouse);

        Assert.Equal(DeviceType.Mouse, item.Type);
        Assert.Equal("Muis", item.TypeName);
        Assert.Equal("Muis · 2 LED's in 2 zones", item.Details);
    }

    [Fact]
    public void For_TypeWithoutDutchName_ShowsOnlyTheLeds()
    {
        var item = DeviceItem.For(TestDevices.Create("mousemat", "Mousemat", DeviceType.Other, 15));

        Assert.Null(item.TypeName);
        Assert.Equal("15 LED's", item.Details);
    }

    [Theory]
    [InlineData(DeviceSource.Hardware, "Echte apparaten (OpenRGB)")]
    [InlineData(DeviceSource.Simulated, "Gesimuleerde apparaten")]
    public void For_RealOrSimulated_HasItsHeading(DeviceSource source, string group)
    {
        var device = TestDevices.Create("fan", "Fan", DeviceType.Fan, 12, source);

        Assert.Equal(group, DeviceItem.For(device).Group);
    }

    // A new source without "DeviceSource_<source>" in Strings.resx would end up with "all devices".
    [Fact]
    public void For_EverySource_HasAHeading() =>
        Assert.All(Enum.GetValues<DeviceSource>(), source =>
            Assert.NotNull(DeviceItem.For(TestDevices.Create("fan", "Fan", DeviceType.Fan, 12, source)).Group));

    // Two sticks of a kit have the same name; their address on the SMBus says which slot is which.
    [Fact]
    public void For_RamStickWithAnAddress_ShowsWhereItIs()
    {
        var stick = TestDevices.Create("ram", "ENE DRAM 2", DeviceType.Ram, 8) with { Location = new DeviceLocation("PIIX4", "0x52") };

        var item = DeviceItem.For(stick);

        Assert.Equal("adres 0x52 op PIIX4", item.Location);
        Assert.True(item.ShowsLocation);
        Assert.Equal("Locatie: adres 0x52 op PIIX4", item.LocationToolTip);
    }

    // A USB path tells the user little, so it is only a tooltip.
    [Fact]
    public void For_DeviceWithoutAddress_HasTheLocationOnlyAsToolTip()
    {
        var keyboard = TestDevices.Create("keyboard", "Keyboard", DeviceType.Keyboard, 104) with { Location = new DeviceLocation(@"HID: \\?\hid#vid_1b1c") };

        var item = DeviceItem.For(keyboard);

        Assert.False(item.ShowsLocation);
        Assert.Equal(@"Locatie: HID: \\?\hid#vid_1b1c", item.LocationToolTip);
    }

    [Fact]
    public void For_DeviceWithoutLocation_ShowsNone()
    {
        var item = DeviceItem.For(TestDevices.Create("fan", "Fan", DeviceType.Fan, 12));

        Assert.Null(item.Location);
        Assert.False(item.ShowsLocation);
        Assert.Null(item.LocationToolTip);
    }

    [Fact]
    public void AllDevices_HasNoIdNoTypeAndNoHeading()
    {
        var item = DeviceItem.AllDevices(3);

        Assert.Null(item.DeviceId);
        Assert.Null(item.Type);
        Assert.Null(item.Group);
        Assert.Equal("3 apparaten", item.Details);
    }
}
