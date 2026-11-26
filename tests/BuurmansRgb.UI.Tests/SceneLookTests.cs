using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The bulbs of a scene card and the one color of a lighting, per effect of the catalog, for an own
// effect and for an effect the app does not know: the same as before the table of EffectTraits.
public sealed class SceneLookTests
{
    private const string OwnEffect = "custom:1a2b3c4d";

    private const string Unknown = "unknown";

    // The fire without a list of its own glows red, orange, yellow, orange and red: the warm
    // colors without their dark red.
    private static readonly RgbColor[] WarmFire =
    [
        EffectColors.DefaultFire[1], EffectColors.DefaultFire[2], EffectColors.DefaultFire[3], EffectColors.DefaultFire[2], EffectColors.DefaultFire[1],
    ];

    private static readonly RgbColor[] AllOrange = [Orange, Orange, Orange, Orange, Orange];

    public static TheoryData<string, RgbColor[]> Dots => new()
    {
        { EffectIds.Static, AllOrange },
        { EffectIds.Breathing, AllOrange },
        { EffectIds.Rainbow, [.. Enumerable.Range(0, SceneLook.DotCount).Select(i => RgbColor.FromHsv(i / (double)SceneLook.DotCount, 1.0, 1.0))] },
        { EffectIds.Wave, AllOrange },
        { EffectIds.ColorLoop, [.. Enumerable.Range(0, SceneLook.DotCount).Select(i => ColorLoop.Default.Colors[i % ColorLoop.Default.Colors.Count])] },
        { EffectIds.Twinkle, AllOrange },
        { EffectIds.Comet, AllOrange },
        { EffectIds.Scanner, AllOrange },
        { EffectIds.Fire, WarmFire },
        { EffectIds.PerLed, AllOrange },
        { OwnEffect, AllOrange },
        { Unknown, AllOrange },
    };

    // The effects with a list show its colors in turn; the others do not look at a list.
    public static TheoryData<string, RgbColor[]> DotsWithAList => new()
    {
        { EffectIds.Static, AllOrange },
        { EffectIds.Twinkle, [Red, Blue, Red, Blue, Red] },
        { EffectIds.Comet, [Red, Blue, Red, Blue, Red] },
        { EffectIds.Scanner, [Red, Blue, Red, Blue, Red] },
        { EffectIds.Fire, [Red, Blue, Red, Blue, Red] },
        { OwnEffect, AllOrange },
        { Unknown, AllOrange },
    };

    // Null for the effects that show more colors, or none of the lamp.
    public static TheoryData<string, RgbColor?> OneColor => new()
    {
        { EffectIds.Static, Orange },
        { EffectIds.Breathing, Orange },
        { EffectIds.Rainbow, null },
        { EffectIds.Wave, Orange },
        { EffectIds.ColorLoop, null },
        { EffectIds.Twinkle, Orange },
        { EffectIds.Comet, Orange },
        { EffectIds.Scanner, Orange },
        { EffectIds.Fire, null },
        { EffectIds.PerLed, null },
        { OwnEffect, null },
        { Unknown, Orange },
    };

    // With a list of one color: also the fire has one color, and still not the others without one.
    public static TheoryData<string, RgbColor?> OneColorOfAList => new()
    {
        { EffectIds.Static, null },
        { EffectIds.Rainbow, null },
        { EffectIds.ColorLoop, null },
        { EffectIds.Twinkle, Blue },
        { EffectIds.Comet, Blue },
        { EffectIds.Scanner, Blue },
        { EffectIds.Fire, Blue },
        { EffectIds.PerLed, null },
        { OwnEffect, null },
        { Unknown, null },
    };

    [Theory]
    [MemberData(nameof(Dots))]
    public void DotsOf_PerEffect(string effectId, RgbColor[] colors)
    {
        var dots = SceneLook.DotsOf(new LightingInfo(Orange, 0.8, effectId));

        Assert.Equal(colors, dots.Select(dot => dot.Color));
        Assert.All(dots, dot => Assert.True(dot.IsLit));
    }

    [Theory]
    [MemberData(nameof(DotsWithAList))]
    public void DotsOf_WithAList_PerEffect(string effectId, RgbColor[] colors)
    {
        var lighting = new LightingInfo(Orange, 0.8, effectId, EffectColors: EffectColors.None.With(effectId, [Red, Blue]));

        Assert.Equal(colors, SceneLook.DotsOf(lighting).Select(dot => dot.Color));
    }

    [Theory]
    [MemberData(nameof(OneColor))]
    public void OneColorOf_PerEffect(string effectId, RgbColor? color) =>
        Assert.Equal(color, SceneLook.OneColorOf(new LightingInfo(Orange, 0.8, effectId)));

    // A list with one color for an effect that takes one; for the others a list of two, which the
    // engine never sends, so a list that is not looked at counts as more colors.
    [Theory]
    [MemberData(nameof(OneColorOfAList))]
    public void OneColorOf_WithAList_PerEffect(string effectId, RgbColor? color)
    {
        IReadOnlyList<RgbColor> list = EffectColors.Takes(effectId) ? [Blue] : [Blue, Red];
        var lighting = new LightingInfo(Orange, 0.8, effectId, EffectColors: EffectColors.None.With(effectId, list));

        Assert.Equal(color, SceneLook.OneColorOf(lighting));
    }
}
