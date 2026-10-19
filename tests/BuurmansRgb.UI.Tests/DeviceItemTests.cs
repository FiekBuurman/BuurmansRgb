using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class DeviceItemTests
{
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
