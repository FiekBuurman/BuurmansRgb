namespace BuurmansRgb.Core.Tests;

public sealed class RgbDeviceTests
{
    private static readonly RgbDevice Board = new("board", "Board", DeviceType.Motherboard,
    [
        RgbZone.Fixed("Mainboard", 6),
        new RgbZone("ARGB header", 0, 0, 120),
    ])
    {
        Source = DeviceSource.Hardware,
    };

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void GetZone_UnknownIndex_Throws(int zoneIndex)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Board.GetZone(zoneIndex));

        Assert.Equal("zoneIndex", exception.ParamName);
    }

    [Fact]
    public void GetZoneLeds_StartsAfterTheZonesBefore()
    {
        var resized = Board.WithZoneLength(1, 30);

        Assert.Equal((0, 6), resized.GetZoneLeds(0));
        Assert.Equal((6, 30), resized.GetZoneLeds(1));
    }

    [Fact]
    public void GetZoneLeds_ZoneWithoutLeds_HasNone()
    {
        Assert.Equal((6, 0), Board.GetZoneLeds(1));
    }

    // As a device can report fewer LEDs than its zones claim: the range stays within the device.
    [Fact]
    public void GetZoneLeds_ZonesClaimMoreThanTheDeviceHas_AreCutShort()
    {
        var cutShort = new RgbDevice("strip", "Strip", DeviceType.LedStrip,
            [RgbZone.Fixed("A", 4), RgbZone.Fixed("B", 4), RgbZone.Fixed("C", 4)], LedCount: 6)
        {
            Source = DeviceSource.Hardware,
        };

        Assert.Equal((4, 2), cutShort.GetZoneLeds(1));
        Assert.Equal((6, 0), cutShort.GetZoneLeds(2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void GetZoneLeds_UnknownIndex_Throws(int zoneIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Board.GetZoneLeds(zoneIndex));
    }

    [Fact]
    public void WithZoneLength_ChangesOnlyThatZoneAndTheTotal()
    {
        var resized = Board.WithZoneLength(1, 30);

        Assert.Equal(36, resized.LedCount);
        Assert.Equal(new RgbZone("ARGB header", 30, 0, 120), resized.Zones[1]);
        Assert.Same(Board.Zones[0], resized.Zones[0]);
        Assert.Equal((Board.Id, Board.Name, Board.Type), (resized.Id, resized.Name, resized.Type));
        Assert.Equal(0, Board.Zones[1].LedCount);
    }

    [Fact]
    public void WithZoneLength_KeepsTheSourceAndLocation()
    {
        var board = Board with { Source = DeviceSource.Simulated, Location = new DeviceLocation("PIIX4", "0x4E") };

        var resized = board.WithZoneLength(1, 30);

        Assert.Equal(DeviceSource.Simulated, resized.Source);
        Assert.Equal(new DeviceLocation("PIIX4", "0x4E"), resized.Location);
    }

    [Fact]
    public void WithZoneLength_DropsTheLedNamesOfThatZone()
    {
        var strip = new RgbDevice("strip", "Strip", DeviceType.LedStrip,
            [RgbZone.Fixed("Logo", 1) with { LedNames = ["Logo"] }, new RgbZone("ARGB", 2, 0, 60) with { LedNames = ["LED 1", "LED 2"] }])
        {
            Source = DeviceSource.Hardware,
        };

        var resized = strip.WithZoneLength(1, 3);

        Assert.Null(resized.Zones[1].LedNames);
        Assert.Equal(["Logo"], resized.Zones[0].LedNames!);
    }

    [Theory]
    [InlineData(1, 121)]
    [InlineData(0, 5)]
    [InlineData(2, 1)]
    public void WithZoneLength_OutsideTheRangeOrZones_Throws(int zoneIndex, int ledCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Board.WithZoneLength(zoneIndex, ledCount));
    }
}
