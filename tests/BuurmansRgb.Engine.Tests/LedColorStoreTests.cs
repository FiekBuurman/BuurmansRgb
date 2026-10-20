using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class LedColorStoreTests
{
    private static readonly RgbColor White = RgbColor.White;

    // A strip of five LEDs in one fixed zone.
    private static readonly RgbDevice Strip = Device(RgbZone.Fixed("Strip", 5));

    private readonly LedColorStore _store = new();

    [Fact]
    public void Resolve_NothingStored_IsTheFallbackOnEveryLed()
    {
        Assert.Equal([White, White], _store.Resolve(Device(RgbZone.Fixed("Strip", 2)), White));
    }

    [Fact]
    public void Set_StoresOnlyTheGivenLeds()
    {
        _store.Set(Strip, 1, [Red]);
        _store.Set(Strip, 3, [Blue]);

        Assert.Equal([null, Red, null, Blue], _store.For(Strip));
        Assert.Equal([White, Red, White, Blue, White], _store.Resolve(Strip, White));
    }

    [Fact]
    public void Set_InsideStoredColors_KeepsTheColorsPastIt()
    {
        _store.Replace(Colors(Red, Red, Red, Red), NoZoneLengths());

        _store.Set(Strip, 0, [Blue]);

        Assert.Equal([Blue, Red, Red, Red], _store.For(Strip));
    }

    [Fact]
    public void Set_StoresTheZoneLengthsOfTheDevice()
    {
        _store.Set(Board(header: 2), 0, [Red]);

        Assert.Equal([2, 2, 1], _store.SnapshotZoneLengths()[Strip.Id]);
    }

    [Fact]
    public void Snapshot_IsACopy()
    {
        _store.Set(Strip, 0, [Red]);
        var snapshot = _store.Snapshot();

        _store.Set(Strip, 0, [Blue]);

        Assert.Equal([Red], snapshot[Strip.Id]);
    }

    // A board with two fixed LEDs, a header and one more fixed LED.
    [Fact]
    public void For_HeaderGrew_LaterZoneKeepsItsColors()
    {
        _store.Set(Board(header: 2), 0, [Red, Red, Blue, Blue, White]);

        Assert.Equal([Red, Red, Blue, Blue, null, null, White], _store.For(Board(header: 4)));
    }

    [Fact]
    public void For_HeaderShrank_ShowsFewerOfItsColorsAndKeepsTheRest()
    {
        _store.Set(Board(header: 2), 0, [Red, Red, Blue, Blue, White]);

        Assert.Equal([Red, Red, Blue, White], _store.For(Board(header: 1)));
    }

    // As when OpenRGB forgets a length until the engine gives it back: nothing is lost meanwhile.
    [Fact]
    public void For_HeaderShrankAndGrewBack_HasAllItsColorsAgain()
    {
        _store.Set(Board(header: 2), 0, [Red, Red, Blue, Green, White]);

        Assert.Equal([Red, Red, White], _store.For(Board(header: 0)));
        Assert.Equal([Red, Red, Blue, Green, White], _store.For(Board(header: 2)));
        Assert.Equal([Red, Red, Blue, Green, White], _store.Snapshot()[Strip.Id]);
    }

    [Fact]
    public void For_LastZoneShrank_KeepsItsColorsForWhenItGrowsBack()
    {
        var longStrip = Device(RgbZone.Fixed("Logo", 1), new RgbZone("Strip", 3, 0, 60));
        _store.Set(longStrip, 0, [Red, Blue, Blue, Blue]);

        Assert.Equal([Red, Blue, Blue, Blue], _store.For(Device(RgbZone.Fixed("Logo", 1), new RgbZone("Strip", 1, 0, 60))));
    }

    [Fact]
    public void For_OtherNumberOfZones_GivesTheColorsAsTheyAre()
    {
        _store.Set(Board(header: 2), 0, [Red, Red, Blue, Blue, White]);

        Assert.Equal([Red, Red, Blue, Blue, White], _store.For(Device(RgbZone.Fixed("Mainboard", 3))));
    }

    // The colors of a file from before the zone lengths were stored.
    [Fact]
    public void Adopt_ColorsWithoutZoneLengths_TakeThoseOfTheDevice()
    {
        _store.Replace(Colors(Red, Red, Blue, Blue, White), NoZoneLengths());
        Assert.Empty(_store.SnapshotZoneLengths());

        _store.Adopt([Board(header: 2)]);

        Assert.Equal([2, 2, 1], _store.SnapshotZoneLengths()[Strip.Id]);
        Assert.Equal([Red, Red, Blue, Blue, null, null, White], _store.For(Board(header: 4)));
    }

    [Fact]
    public void Adopt_ColorsWithZoneLengths_KeepTheirOwn()
    {
        _store.Replace(Colors(Red, Red, Blue, Blue, White), new Dictionary<string, IReadOnlyList<int>> { [Strip.Id] = [2, 2, 1] });

        _store.Adopt([Board(header: 0)]);

        Assert.Equal([2, 2, 1], _store.SnapshotZoneLengths()[Strip.Id]);
        Assert.Equal([Red, Red, Blue, Blue, White], _store.For(Board(header: 2)));
    }

    // An edit stores the colors for the zones of that moment.
    [Fact]
    public void Set_WhileAZoneIsShorter_StoresTheColorsForTheZonesOfNow()
    {
        _store.Set(Board(header: 2), 0, [Red, Red, Blue, Blue, White]);

        _store.Set(Board(header: 1), 0, [Green]);

        Assert.Equal([Green, Red, Blue, White], _store.Snapshot()[Strip.Id]);
        Assert.Equal([2, 1, 1], _store.SnapshotZoneLengths()[Strip.Id]);
    }

    [Fact]
    public void Replace_DropsWhatWasStored()
    {
        _store.Set(Strip, 0, [Red]);
        var other = Strip with { Id = "other" };

        _store.Replace(new Dictionary<string, IReadOnlyList<RgbColor?>> { ["other"] = [Blue] }, NoZoneLengths());

        Assert.Empty(_store.For(Strip));
        Assert.Equal([Blue], _store.For(other));
    }

    private static RgbDevice Device(params IReadOnlyList<RgbZone> zones) =>
        new("device", "Device", DeviceType.Other, zones) { Source = DeviceSource.Simulated };

    private static RgbDevice Board(int header) =>
        Device(RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB header", header, 0, 120), RgbZone.Fixed("Logo", 1));

    private static Dictionary<string, IReadOnlyList<RgbColor?>> Colors(params RgbColor?[] colors) => new() { [Strip.Id] = colors };

    private static Dictionary<string, IReadOnlyList<int>> NoZoneLengths() => [];
}
