using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts.Tests;

public sealed class DeviceInfoTests
{
    private static DeviceInfo Board(int headerLeds) =>
        new("board", "Board", DeviceType.Motherboard, 6 + headerLeds,
            [RgbZone.Fixed("Mainboard", 6), new RgbZone("ARGB header", headerLeds, 0, 120)]);

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
    public void From_TakesTheSourceOfTheDevice()
    {
        var device = new RgbDevice("fan", "Fan", DeviceType.Fan, [RgbZone.Fixed("Ring", 12)]) { Source = DeviceSource.Simulated };

        Assert.Equal(DeviceSource.Simulated, DeviceInfo.From(device).Source);
    }
}
