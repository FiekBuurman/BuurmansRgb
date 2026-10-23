using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts.Tests;

public sealed class DeviceInfoTests
{
    private static DeviceInfo Board(int headerLeds) =>
        new("board", "Board", DeviceType.Motherboard, 6 + headerLeds,
            [RgbZone.Fixed("Mainboard", 6), new RgbZone("ARGB header", headerLeds, 0, 120)])
        {
            Source = DeviceSource.Hardware,
        };

    [Fact]
    public void Equals_SameZonesInAnotherList_IsEqual()
    {
        var board = Board(2);
        var copy = board with { Zones = [.. board.Zones] };

        Assert.Equal(board, copy);
        Assert.Equal(board.GetHashCode(), copy.GetHashCode());
    }

    [Fact]
    public void Equals_AZoneDiffers_IsNotEqual()
    {
        var board = Board(2);

        Assert.NotEqual(board, board with { Zones = Board(3).Zones });
    }

    [Fact]
    public void Equals_TheSourceDiffers_IsNotEqual()
    {
        var board = Board(2);

        Assert.NotEqual(board, board with { Source = DeviceSource.Simulated });
    }

    [Fact]
    public void Equals_TheLocationDiffers_IsNotEqual()
    {
        var board = Board(2) with { Location = new DeviceLocation("PIIX4", "0x70") };

        Assert.NotEqual(board, board with { Location = new DeviceLocation("PIIX4", "0x71") });
        Assert.Equal(board, board with { Location = new DeviceLocation("PIIX4", "0x70") });
    }

    [Fact]
    public void Equals_DirectModeDiffers_IsNotEqual()
    {
        var board = Board(2);

        Assert.True(board.HasDirectMode);
        Assert.NotEqual(board, board with { HasDirectMode = false });
    }

    [Fact]
    public void Equals_ZonesAlikeDiffers_IsNotEqual()
    {
        var board = Board(2);

        Assert.False(board.ZonesAlike);
        Assert.NotEqual(board, board with { ZonesAlike = true });
    }

    [Fact]
    public void From_TakesZonesAlike()
    {
        var device = new RgbDevice("mouse", "Mouse", DeviceType.Mouse, [RgbZone.Fixed("Primary", 1), RgbZone.Fixed("Logo", 1)]) { Source = DeviceSource.Hardware };

        Assert.True(DeviceInfo.From(device, zonesAlike: true).ZonesAlike);
        Assert.False(DeviceInfo.From(device).ZonesAlike);
    }

    [Fact]
    public void From_TakesTheSourceLocationAndDirectModeOfTheDevice()
    {
        var device = new RgbDevice("fan", "Fan", DeviceType.Fan, [RgbZone.Fixed("Ring", 12)])
        {
            Source = DeviceSource.Simulated,
            Location = new DeviceLocation("HID: fan"),
            HasDirectMode = false,
        };

        var info = DeviceInfo.From(device);

        Assert.Equal(DeviceSource.Simulated, info.Source);
        Assert.Equal(new DeviceLocation("HID: fan"), info.Location);
        Assert.False(info.HasDirectMode);
    }
}
