using BuurmansRgb.Core;
using OpenRgbDeviceType = OpenRGB.NET.DeviceType;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

public sealed class OpenRgbDeviceMapperTests
{
    [Fact]
    public void Map_CopiesNameZonesAndLedCount()
    {
        var data = TestDevices.Create(
            index: 3,
            name: "ASUS ROG STRIX B550-F",
            type: OpenRgbDeviceType.Motherboard,
            zones: [new("Aura Mainboard", 4, 4, 4), new("Aura Addressable 1", 0, 0, 120)]);

        var mapped = Assert.Single(OpenRgbDeviceMapper.Map([data]));

        Assert.Equal(3, mapped.Index);
        Assert.Equal("ASUS ROG STRIX B550-F", mapped.Device.Name);
        Assert.Equal(DeviceType.Motherboard, mapped.Device.Type);
        Assert.Equal(4, mapped.Device.LedCount);
        Assert.Equal(
            [new RgbZone("Aura Mainboard", 4, Resizable: false), new RgbZone("Aura Addressable 1", 0, Resizable: true)],
            mapped.Device.Zones);
    }

    [Theory]
    [InlineData(OpenRgbDeviceType.Motherboard, DeviceType.Motherboard)]
    [InlineData(OpenRgbDeviceType.Dram, DeviceType.Ram)]
    [InlineData(OpenRgbDeviceType.Gpu, DeviceType.Gpu)]
    [InlineData(OpenRgbDeviceType.Cooler, DeviceType.Fan)]
    [InlineData(OpenRgbDeviceType.Ledstrip, DeviceType.LedStrip)]
    [InlineData(OpenRgbDeviceType.Keyboard, DeviceType.Keyboard)]
    [InlineData(OpenRgbDeviceType.Mouse, DeviceType.Mouse)]
    [InlineData(OpenRgbDeviceType.Headset, DeviceType.Headset)]
    [InlineData(OpenRgbDeviceType.Mousemat, DeviceType.Other)]
    [InlineData(OpenRgbDeviceType.Speaker, DeviceType.Other)]
    [InlineData(OpenRgbDeviceType.Unknown, DeviceType.Other)]
    [InlineData((OpenRgbDeviceType)99, DeviceType.Other)]
    public void ToDeviceType_MapsOpenRgbTypes(OpenRgbDeviceType type, DeviceType expected) =>
        Assert.Equal(expected, OpenRgbDeviceMapper.ToDeviceType(type));

    [Fact]
    public void CreateId_IsReadableAndStable()
    {
        var device = TestDevices.Create(name: "Corsair Vengeance Pro RGB", location: "I2C: PIIX4, address 0x58");

        var id = OpenRgbDeviceMapper.CreateId(device);

        Assert.Matches("^openrgb-corsair-vengeance-pro-rgb-[0-9a-f]{8}$", id);
        Assert.Equal(id, OpenRgbDeviceMapper.CreateId(device with { Index = 7 }));
    }

    [Fact]
    public void CreateId_SameModelOnOtherLocation_GetsOtherId()
    {
        var first = TestDevices.Create(name: "Corsair Vengeance Pro RGB", location: "I2C: PIIX4, address 0x58");
        var second = first with { Location = "I2C: PIIX4, address 0x59" };

        Assert.NotEqual(OpenRgbDeviceMapper.CreateId(first), OpenRgbDeviceMapper.CreateId(second));
    }

    [Fact]
    public void CreateId_NameWithoutLettersOrDigits_StillHasPrefixAndHash() =>
        Assert.Matches("^openrgb-[0-9a-f]{8}$", OpenRgbDeviceMapper.CreateId(TestDevices.Create(name: " ** ")));

    [Fact]
    public void CreateId_LongName_IsShortened()
    {
        var id = OpenRgbDeviceMapper.CreateId(TestDevices.Create(name: new string('a', 100)));

        Assert.Equal("openrgb-".Length + 40 + "-12345678".Length, id.Length);
    }

    [Fact]
    public void Map_IdenticalDevices_GetNumberedIds()
    {
        var device = TestDevices.Create(name: "Fan hub");

        var mapped = OpenRgbDeviceMapper.Map([device, device with { Index = 1 }, device with { Index = 2 }]);

        var id = OpenRgbDeviceMapper.CreateId(device);
        Assert.Equal([id, $"{id}-2", $"{id}-3"], mapped.Select(item => item.Device.Id));
    }

    [Theory]
    [InlineData("Direct")]
    [InlineData("Custom")]
    [InlineData("direct")]
    public void HasDirectMode_WithDirectOrCustomMode_IsTrue(string mode) =>
        Assert.True(OpenRgbDeviceMapper.HasDirectMode(TestDevices.Create(modes: ["Static", mode, "Breathing"])));

    [Fact]
    public void HasDirectMode_WithOnlyHardwareModes_IsFalse() =>
        Assert.False(OpenRgbDeviceMapper.HasDirectMode(TestDevices.Create(modes: ["Static", "Breathing"])));
}
