namespace BuurmansRgb.Core.Tests;

public sealed class RgbDeviceTests
{
    private static readonly RgbDevice Board = new("board", "Board", DeviceType.Motherboard,
    [
        RgbZone.Fixed("Mainboard", 6),
        new RgbZone("ARGB header", 0, 0, 120),
    ]);

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void GetZone_UnknownIndex_Throws(int zoneIndex)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Board.GetZone(zoneIndex));

        Assert.Equal("zoneIndex", exception.ParamName);
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

    [Theory]
    [InlineData(1, 121)]
    [InlineData(0, 5)]
    [InlineData(2, 1)]
    public void WithZoneLength_OutsideTheRangeOrZones_Throws(int zoneIndex, int ledCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Board.WithZoneLength(zoneIndex, ledCount));
    }
}
