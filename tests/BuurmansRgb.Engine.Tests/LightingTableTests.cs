using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingTableTests
{
    private static readonly DeviceLighting HalfOrange = new(Orange, 0.5, EffectIds.Static);

    private readonly LightingTable _table = new();

    [Fact]
    public void New_IsNotSetAndGivesTheDefault()
    {
        Assert.False(_table.IsSet);
        Assert.Equal(DeviceLighting.Default, _table.Global);
        Assert.Equal(DeviceLighting.Default, _table.Get("device"));
    }

    [Fact]
    public void Set_PinsTheDefaultAsGlobal()
    {
        _table.Set("device", HalfOrange);

        Assert.True(_table.IsSet);
        Assert.Equal(DeviceLighting.Default, _table.Global);
        Assert.Equal(HalfOrange, _table.Get("device"));
    }

    [Fact]
    public void Set_EqualToGlobal_FollowsTheGlobalAgain()
    {
        _table.Set("device", HalfOrange);

        _table.Set("device", DeviceLighting.Default);

        Assert.Empty(_table.Snapshot());
    }

    [Fact]
    public void SetGlobal_AppliesTheChangeToDevicesWithTheirOwnLighting()
    {
        _table.Set("device", HalfOrange);

        _table.SetGlobal(lighting => lighting with { Brightness = 0.25 });

        Assert.Equal(0.25, _table.Global.Brightness);
        Assert.Equal(HalfOrange with { Brightness = 0.25 }, _table.Get("device"));
    }

    [Fact]
    public void SetGlobal_InvalidForOneDevice_ChangesNothing()
    {
        _table.Set("device", HalfOrange);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => _table.SetGlobal(lighting => lighting with { Brightness = lighting == HalfOrange ? 2.0 : 0.25 }));

        Assert.Equal(DeviceLighting.Default, _table.Global);
        Assert.Equal(HalfOrange, _table.Get("device"));
    }

    [Fact]
    public void Replace_KeepsDevicesEqualToGlobal()
    {
        _table.Replace(HalfOrange, new Dictionary<string, DeviceLighting> { ["device"] = HalfOrange });

        Assert.Equal(HalfOrange, _table.Global);
        Assert.Equal(HalfOrange, Assert.Single(_table.Snapshot()).Value);
    }
}
