using System.Text.Json;

namespace BuurmansRgb.Core.Tests;

public sealed class RgbZoneTests
{
    [Fact]
    public void Fixed_HoldsOnlyItsOwnLength()
    {
        var zone = RgbZone.Fixed("Ring", 12);

        Assert.False(zone.Resizable);
        Assert.Equal((12, 12, 12), (zone.LedCount, zone.MinLedCount, zone.MaxLedCount));
        Assert.True(zone.CanHold(12));
        Assert.False(zone.CanHold(11));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(60, true)]
    [InlineData(120, true)]
    [InlineData(-1, false)]
    [InlineData(121, false)]
    public void CanHold_ResizableZone_AcceptsItsRange(int ledCount, bool expected)
    {
        var zone = new RgbZone("ARGB header", 0, 0, 120);

        Assert.True(zone.Resizable);
        Assert.Equal(expected, zone.CanHold(ledCount));
    }

    // Names that came over the pipe are another list with the same names.
    [Fact]
    public void Equals_SameLedNamesInAnotherList_IsEqual()
    {
        var zone = RgbZone.Fixed("Keys", 2) with { LedNames = ["Escape", "F1"] };
        var copy = zone with { LedNames = new List<string> { "Escape", "F1" } };

        Assert.Equal(zone, copy);
        Assert.Equal(zone.GetHashCode(), copy.GetHashCode());
    }

    [Fact]
    public void Equals_LedNamesDiffer_IsNotEqual()
    {
        var zone = RgbZone.Fixed("Keys", 2) with { LedNames = ["Escape", "F1"] };

        Assert.NotEqual(zone, zone with { LedNames = ["Escape", "F2"] });
        Assert.NotEqual(zone, zone with { LedNames = null });
        Assert.NotEqual(zone with { LedNames = null }, zone);
    }

    [Fact]
    public void Equals_OtherFields_StillCount()
    {
        var zone = new RgbZone("Strip", 30, 1, 60) with { Matrix = new LedMatrix(1, 1, [0]) };

        Assert.Equal(zone, zone with { Matrix = new LedMatrix(1, 1, [0]) });
        Assert.NotEqual(zone, zone with { Matrix = null });
        Assert.NotEqual(zone, zone with { Name = "Other" });
        Assert.NotEqual(zone, zone with { LedCount = 31 });
        Assert.NotEqual(zone, zone with { MinLedCount = 0 });
        Assert.NotEqual(zone, zone with { MaxLedCount = 61 });
    }

    // Most zones have no names; they should not cost bytes on the pipe.
    [Fact]
    public void Json_WithoutLedNames_LeavesThemOut()
    {
        var json = JsonSerializer.Serialize(RgbZone.Fixed("Ring", 12));

        Assert.DoesNotContain(nameof(RgbZone.LedNames), json);
    }

    [Fact]
    public void Json_WithLedNames_RoundTrips()
    {
        var zone = RgbZone.Fixed("Keys", 2) with { LedNames = ["Escape", "F1"] };

        var json = JsonSerializer.Serialize(zone);

        Assert.Equal(zone, JsonSerializer.Deserialize<RgbZone>(json));
    }

    [Fact]
    public void RangesOf_EachZoneStartsAfterTheZonesBefore()
    {
        RgbZone[] zones = [RgbZone.Fixed("Mainboard", 6), new RgbZone("ARGB header", 30, 0, 120), RgbZone.Fixed("Logo", 1)];

        Assert.Equal([(0, 6), (6, 30), (36, 1)], RgbZone.RangesOf(zones, 37));
    }

    [Fact]
    public void RangesOf_ZoneWithoutLeds_HasNone()
    {
        RgbZone[] zones = [RgbZone.Fixed("Mainboard", 6), new RgbZone("ARGB header", 0, 0, 120), RgbZone.Fixed("Logo", 1)];

        Assert.Equal([(0, 6), (6, 0), (6, 1)], RgbZone.RangesOf(zones, 7));
    }

    // As a device can report fewer LEDs than its zones claim: every range stays within the device.
    [Fact]
    public void RangesOf_ZonesClaimMoreThanTheDeviceHas_AreCutShort()
    {
        RgbZone[] zones = [RgbZone.Fixed("A", 4), RgbZone.Fixed("B", 4), RgbZone.Fixed("C", 4)];

        Assert.Equal([(0, 4), (4, 2), (6, 0)], RgbZone.RangesOf(zones, 6));
    }

    // The LEDs after the last range belong to no zone, such as "Overige LED's" in the preview.
    [Fact]
    public void RangesOf_DeviceHasMoreLedsThanItsZones_LeavesTheRestAfterTheLastRange()
    {
        RgbZone[] zones = [RgbZone.Fixed("A", 2), RgbZone.Fixed("B", 3)];

        var ranges = RgbZone.RangesOf(zones, 8);

        Assert.Equal([(0, 2), (2, 3)], ranges);
        Assert.Equal(5, ranges[^1].FirstLed + ranges[^1].LedCount);
    }

    [Fact]
    public void RangesOf_NoZones_IsEmpty()
    {
        Assert.Empty(RgbZone.RangesOf([], 10));
    }

    [Fact]
    public void RangesOf_NegativeLedCount_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => RgbZone.RangesOf([RgbZone.Fixed("A", 1)], -1));

        Assert.Equal("ledCount", exception.ParamName);
    }

    [Fact]
    public void ThrowIfCannotHold_OutsideTheRange_ThrowsWithTheRange()
    {
        var zone = new RgbZone("ARGB header", 0, 0, 120);

        zone.ThrowIfCannotHold(120);
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => zone.ThrowIfCannotHold(121));
        Assert.Equal("ledCount", exception.ParamName);
        Assert.Contains("0 to 120", exception.Message);
    }
}
