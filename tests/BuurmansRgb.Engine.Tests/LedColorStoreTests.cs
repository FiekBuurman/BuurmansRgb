using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class LedColorStoreTests
{
    private const string Device = "device";

    private static readonly RgbColor Red = new(255, 0, 0);
    private static readonly RgbColor Blue = new(0, 0, 255);
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

    [Fact]
    public void Replace_DropsWhatWasStored()
    {
        _store.Set(Device, 0, [Red]);

        _store.Replace(new Dictionary<string, IReadOnlyList<RgbColor?>> { ["other"] = [Blue] });

        Assert.Empty(_store.For(Device));
        Assert.Equal([Blue], _store.For("other"));
    }
}
