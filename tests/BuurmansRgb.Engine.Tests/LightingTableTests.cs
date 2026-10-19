using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingTableTests
{
    private static readonly DeviceLighting Orange = new(new RgbColor(255, 136, 0), 0.5, EffectIds.Static);

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
        _table.Set("device", Orange);

        Assert.True(_table.IsSet);
        Assert.Equal(DeviceLighting.Default, _table.Global);
        Assert.Equal(Orange, _table.Get("device"));
    }

    [Fact]
    public void Set_EqualToGlobal_FollowsTheGlobalAgain()
    {
        _table.Set("device", Orange);

        _table.Set("device", DeviceLighting.Default);

        Assert.Empty(_table.Snapshot());
    }

    [Fact]
    public void SetGlobal_AppliesTheChangeToDevicesWithTheirOwnLighting()
    {
        _table.Set("device", Orange);

        _table.SetGlobal(lighting => lighting with { Brightness = 0.25 });

        Assert.Equal(0.25, _table.Global.Brightness);
        Assert.Equal(Orange with { Brightness = 0.25 }, _table.Get("device"));
    }

    [Fact]
    public void SetGlobal_InvalidForOneDevice_ChangesNothing()
    {
        _table.Set("device", Orange);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => _table.SetGlobal(lighting => lighting with { Brightness = lighting == Orange ? 2.0 : 0.25 }));

        Assert.Equal(DeviceLighting.Default, _table.Global);
        Assert.Equal(Orange, _table.Get("device"));
    }

    [Fact]
    public void Replace_KeepsDevicesEqualToGlobal()
    {
        _table.Replace(Orange, new Dictionary<string, DeviceLighting> { ["device"] = Orange });

        Assert.Equal(Orange, _table.Global);
        Assert.Equal(Orange, Assert.Single(_table.Snapshot()).Value);
    }
}
