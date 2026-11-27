using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class LedOverlayStoreTests
{
    private static readonly RgbDevice Strip = new("strip", "Strip", DeviceType.LedStrip, [RgbZone.Fixed("Strip", 4)]) { Source = DeviceSource.Hardware };

    private readonly LedOverlayStore _store = new();

    [Fact]
    public void Set_StoresOnlyTheGivenLeds_WithoutTheNullsAtTheEnd()
    {
        _store.Set(Strip.Id, 1, [Red, null]);

        Assert.Equal([null, Red], _store.For(Strip.Id));
        Assert.Equal([null, Red, null, null], _store.Resolve(Strip));
    }

    [Fact]
    public void Set_ReturnsWhetherTheLayerChanged()
    {
        Assert.True(_store.Set(Strip.Id, 0, [Red]));
        Assert.False(_store.Set(Strip.Id, 0, [Red]));
        Assert.False(_store.Set(Strip.Id, 2, [null]));
        Assert.True(_store.Set(Strip.Id, 0, [null]));
        Assert.Empty(_store.Snapshot());
    }

    // The render skips the work for a layer it was given before (DeviceOutput.ShowOverlay).
    [Fact]
    public void For_KeepsItsInstanceUntilTheLayerChanges()
    {
        _store.Set(Strip.Id, 0, [Red]);
        var before = _store.For(Strip.Id);

        _store.Set("other", 0, [Blue]);

        Assert.Same(before, _store.For(Strip.Id));
    }

    [Fact]
    public void Set_ThatChangesNothing_KeepsTheInstance()
    {
        _store.Set(Strip.Id, 0, [Red]);
        var before = _store.For(Strip.Id);

        _store.Set(Strip.Id, 0, [Red]);
        _store.Set(Strip.Id, 2, [null]);

        Assert.Same(before, _store.For(Strip.Id));
    }

    [Fact]
    public void Set_ThatChangesTheLayer_GivesANewInstance()
    {
        _store.Set(Strip.Id, 0, [Red]);
        var before = _store.For(Strip.Id);

        _store.Set(Strip.Id, 0, [Blue]);

        Assert.NotSame(before, _store.For(Strip.Id));
    }

    [Fact]
    public void Snapshot_IsACopy()
    {
        _store.Set(Strip.Id, 0, [Red]);
        var snapshot = _store.Snapshot();

        _store.Set(Strip.Id, 0, [Blue]);

        Assert.Equal([Red], snapshot[Strip.Id]);
    }

    [Fact]
    public void Replace_LeavesOutALayerWithoutAColor_AndCutsOffWhatIsPastTheMost()
    {
        RgbColor?[] tooMany = [.. Enumerable.Repeat<RgbColor?>(Red, LedOverlayStore.MaxLeds + 10)];

        _store.Replace(new Dictionary<string, IReadOnlyList<RgbColor?>> { ["empty"] = [null, null], ["many"] = tooMany });

        Assert.Equal(["many"], _store.Snapshot().Keys);
        Assert.Equal(LedOverlayStore.MaxLeds, _store.For("many").Count);
    }

    // Only the LEDs the device has now count: a color past its end does not show.
    [Fact]
    public void SameAs_ComparesTheLedsOfTheDeviceOnly()
    {
        var before = new Dictionary<string, IReadOnlyList<RgbColor?>> { [Strip.Id] = [Red, null, null, null, Blue] };

        _store.Set(Strip.Id, 0, [Red]);

        Assert.True(_store.SameAs(before, Strip));
        Assert.False(_store.SameAs(new Dictionary<string, IReadOnlyList<RgbColor?>>(), Strip));
    }
}
