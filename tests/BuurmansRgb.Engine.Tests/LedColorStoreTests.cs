using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class LedColorStoreTests
{
    private const string Device = "device";

    private static readonly RgbColor White = RgbColor.White;

    private readonly LedColorStore _store = new();

    [Fact]
    public void Resolve_NothingStored_IsTheFallbackOnEveryLed()
    {
        Assert.Equal([White, White], _store.Resolve(Device, 2, White));
    }

    [Fact]
    public void Set_StoresOnlyTheGivenLeds()
    {
        _store.Set(Device, 1, [Red]);
        _store.Set(Device, 3, [Blue]);

        Assert.Equal([null, Red, null, Blue], _store.For(Device));
        Assert.Equal([White, Red, White, Blue, White], _store.Resolve(Device, 5, White));
    }

    [Fact]
    public void Set_InsideStoredColors_KeepsTheColorsPastIt()
    {
        _store.Replace(new Dictionary<string, IReadOnlyList<RgbColor?>> { [Device] = [Red, Red, Red, Red] });

        _store.Set(Device, 0, [Blue]);

        Assert.Equal([Blue, Red, Red, Red], _store.For(Device));
        Assert.Equal([Blue, Red], _store.Resolve(Device, 2, White));
    }

    [Fact]
    public void Snapshot_IsACopy()
    {
        _store.Set(Device, 0, [Red]);
        var snapshot = _store.Snapshot();

        _store.Set(Device, 0, [Blue]);

        Assert.Equal([Red], snapshot[Device]);
    }

    // A board with two fixed LEDs, a header of two LEDs and one more fixed LED.
    [Fact]
    public void MoveWithZones_HeaderGrows_LaterZoneMovesAlong()
    {
        _store.Set(Device, 0, [Red, Red, Blue, Blue, White]);

        var moved = _store.MoveWithZones(Device, Board(header: 2), Board(header: 4));

        Assert.True(moved);
        Assert.Equal([Red, Red, Blue, Blue, null, null, White], _store.For(Device));
    }

    [Fact]
    public void MoveWithZones_HeaderShrinks_DropsItsLostLedsAndMovesTheRest()
    {
        _store.Set(Device, 0, [Red, Red, Blue, Blue, White]);

        _store.MoveWithZones(Device, Board(header: 2), Board(header: 1));

        Assert.Equal([Red, Red, Blue, White], _store.For(Device));
    }

    [Fact]
    public void MoveWithZones_LastZoneShrinks_KeepsItsColorsForWhenItGrowsBack()
    {
        _store.Set(Device, 0, [Red, Blue, Blue, Blue]);
        IReadOnlyList<RgbZone> longStrip = [RgbZone.Fixed("Logo", 1), new RgbZone("Strip", 3, 0, 60)];
        IReadOnlyList<RgbZone> shortStrip = [RgbZone.Fixed("Logo", 1), new RgbZone("Strip", 1, 0, 60)];

        _store.MoveWithZones(Device, longStrip, shortStrip);

        Assert.Equal([Red, Blue, Blue, Blue], _store.For(Device));
    }

    [Fact]
    public void MoveWithZones_OnlyEmptyLedsLeft_ForgetsTheDevice()
    {
        _store.Set(Device, 2, [Blue]);

        _store.MoveWithZones(Device, Board(header: 2), Board(header: 0));

        Assert.Empty(_store.Snapshot());
    }

    [Fact]
    public void MoveWithZones_SameLengthsOrOtherZones_MovesNothing()
    {
        _store.Set(Device, 0, [Red, Red, Blue, Blue, White]);

        Assert.False(_store.MoveWithZones(Device, Board(header: 2), Board(header: 2)));
        Assert.False(_store.MoveWithZones(Device, Board(header: 2), [RgbZone.Fixed("Mainboard", 3)]));
        Assert.False(_store.MoveWithZones("other", Board(header: 2), Board(header: 4)));
        Assert.Equal([Red, Red, Blue, Blue, White], _store.For(Device));
    }

    [Fact]
    public void Replace_DropsWhatWasStored()
    {
        _store.Set(Device, 0, [Red]);

        _store.Replace(new Dictionary<string, IReadOnlyList<RgbColor?>> { ["other"] = [Blue] });

        Assert.Empty(_store.For(Device));
        Assert.Equal([Blue], _store.For("other"));
    }

    private static IReadOnlyList<RgbZone> Board(int header) =>
        [RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB header", header, 0, 120), RgbZone.Fixed("Logo", 1)];
}
