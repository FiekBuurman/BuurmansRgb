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
        [DeviceType.Gpu] = null,
        [DeviceType.Fan] = null,
        [DeviceType.LedStrip] = null,
        [DeviceType.Case] = null,
        [DeviceType.Keyboard] = null,
        [DeviceType.Mouse] = null,
        [DeviceType.Headset] = null,
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
            [new RgbZone("Mainboard", 6, Resizable: false), new RgbZone("RGB header", 1, Resizable: false), new RgbZone("ARGB header", 0, Resizable: true)]);

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
    public void For_TypeWithoutDutchName_ShowsOnlyTheLeds()
    {
        var item = DeviceItem.For(TestDevices.Create("fan", "Fan", DeviceType.Fan, 12));

        Assert.Null(item.TypeName);
        Assert.Equal("12 LED's", item.Details);
    }

    [Fact]
    public void AllDevices_HasNoIdAndNoType()
    {
        var item = DeviceItem.AllDevices(3);

        Assert.Null(item.DeviceId);
        Assert.Null(item.Type);
        Assert.Equal("3 apparaten", item.Details);
    }
}
