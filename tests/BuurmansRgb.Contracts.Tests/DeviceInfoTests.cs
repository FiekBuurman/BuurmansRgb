using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts.Tests;

public sealed class DeviceInfoTests
{
    private static DeviceInfo Board(int headerLeds) =>
        new("board", "Board", DeviceType.Motherboard, 6 + headerLeds,
            [new RgbZone("Mainboard", 6, Resizable: false), new RgbZone("ARGB header", headerLeds, Resizable: true)]);

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
}
