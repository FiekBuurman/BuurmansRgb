namespace BuurmansRgb.Contracts.Tests;

// What the lookups per effect say for every effect of the catalog, an own effect, and ids the app
// does not know: one table per method, with the same answers as before the table of EffectTraits.
// An older settings app calls these methods against a newer engine and the other way round, so
// they keep their answers.
public sealed class EffectLookupsTests
{
    // An own effect, ids that look like one but are not, and ids the app does not know.
    private const string OwnEffect = "custom:1a2b3c4d";

    private const string OnlyThePrefix = "custom:";

    private const string PrefixInCapitals = "Custom:1a2b3c4d";

    private const string NameInCapitals = "Rainbow";

    private const string Unknown = "unknown";

    public static TheoryData<string?, bool> Animated { get; } = new()
    {
        { EffectIds.Static, false },
        { EffectIds.Breathing, true },
        { EffectIds.Rainbow, true },
        { EffectIds.Wave, true },
        { EffectIds.ColorLoop, true },
        { EffectIds.Twinkle, true },
        { EffectIds.Comet, true },
        { EffectIds.Scanner, true },
        { EffectIds.Fire, true },
        { EffectIds.PerLed, false },
        { OwnEffect, false },
        { OnlyThePrefix, false },
        { PrefixInCapitals, false },
        { NameInCapitals, false },
        { Unknown, false },
        { "", false },
        { null, false },
    };

    public static TheoryData<string?, bool> UsesDeviceColor { get; } = new()
    {
        { EffectIds.Static, true },
        { EffectIds.Breathing, true },
        { EffectIds.Rainbow, false },
        { EffectIds.Wave, true },
        { EffectIds.ColorLoop, false },
        { EffectIds.Twinkle, true },
        { EffectIds.Comet, true },
        { EffectIds.Scanner, true },
        { EffectIds.Fire, false },
        { EffectIds.PerLed, true },
        { OwnEffect, false },
        { OnlyThePrefix, true },
        { PrefixInCapitals, true },
        { NameInCapitals, true },
        { Unknown, true },
        { "", true },
        { null, true },
    };

    public static TheoryData<string?, bool> TakesColorList { get; } = new()
    {
        { EffectIds.Static, false },
        { EffectIds.Breathing, false },
        { EffectIds.Rainbow, false },
        { EffectIds.Wave, false },
        { EffectIds.ColorLoop, false },
        { EffectIds.Twinkle, true },
        { EffectIds.Comet, true },
        { EffectIds.Scanner, true },
        { EffectIds.Fire, true },
        { EffectIds.PerLed, false },
        { OwnEffect, false },
        { OnlyThePrefix, false },
        { PrefixInCapitals, false },
        { NameInCapitals, false },
        { Unknown, false },
        { "", false },
        { null, false },
    };

    // Whether the default colors are the warm colors of the fire; else they are the lamp color.
    public static TheoryData<string?, bool> DefaultIsFire { get; } = new()
    {
        { EffectIds.Static, false },
        { EffectIds.Breathing, false },
        { EffectIds.Rainbow, false },
        { EffectIds.Wave, false },
        { EffectIds.ColorLoop, false },
        { EffectIds.Twinkle, false },
        { EffectIds.Comet, false },
        { EffectIds.Scanner, false },
        { EffectIds.Fire, true },
        { EffectIds.PerLed, false },
        { OwnEffect, false },
        { OnlyThePrefix, false },
        { PrefixInCapitals, false },
        { NameInCapitals, false },
        { Unknown, false },
        { "", false },
        { null, false },
    };

    // Whether a direction applies without and with spread.
    public static TheoryData<string?, bool, bool> Direction { get; } = new()
    {
        { EffectIds.Static, false, false },
        { EffectIds.Breathing, false, false },
        { EffectIds.Rainbow, true, true },
        { EffectIds.Wave, true, true },
        { EffectIds.ColorLoop, false, true },
        { EffectIds.Twinkle, false, false },
        { EffectIds.Comet, false, false },
        { EffectIds.Scanner, false, false },
        { EffectIds.Fire, false, false },
        { EffectIds.PerLed, false, false },
        { OwnEffect, false, false },
        { OnlyThePrefix, false, false },
        { PrefixInCapitals, false, false },
        { NameInCapitals, false, false },
        { Unknown, false, false },
        { "", false, false },
        { null, false, false },
    };

    [Theory]
    [MemberData(nameof(Animated))]
    public void IsAnimated_PerEffect(string? effectId, bool animated) =>
        Assert.Equal(animated, EffectIds.IsAnimated(effectId));

    [Theory]
    [MemberData(nameof(UsesDeviceColor))]
    public void UsesDeviceColor_PerEffect(string? effectId, bool uses) =>
        Assert.Equal(uses, EffectIds.UsesDeviceColor(effectId));

    [Theory]
    [MemberData(nameof(TakesColorList))]
    public void Takes_PerEffect(string? effectId, bool takes) =>
        Assert.Equal(takes, EffectColors.Takes(effectId));

    // The warm colors of the fire themselves, or else a list with the one lamp color.
    [Theory]
    [MemberData(nameof(DefaultIsFire))]
    public void Default_PerEffect(string? effectId, bool isFire)
    {
        var colors = EffectColors.Default(effectId!, Orange);

        Assert.Equal(isFire ? EffectColors.DefaultFire : [Orange], colors);
        Assert.Equal(isFire, ReferenceEquals(EffectColors.DefaultFire, colors));
    }

    [Theory]
    [MemberData(nameof(Direction))]
    public void Applies_PerEffect(string? effectId, bool alongTheLeds, bool spread)
    {
        Assert.Equal(alongTheLeds, EffectDirection.Applies(effectId, spread: false));
        Assert.Equal(spread, EffectDirection.Applies(effectId, spread: true));
    }
}
