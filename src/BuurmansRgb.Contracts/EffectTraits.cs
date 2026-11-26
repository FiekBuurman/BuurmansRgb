using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts;

// When an effect can run in a direction (EffectDirection).
public enum DirectionUse
{
    Never,

    Always,

    // Only when it spreads over the LEDs, as the color loop does.
    WhenSpread,
}

// What the app knows of an effect, per effect id in one place, so a new effect takes one row here
// (and one in the engine's EffectCatalog, whose tests keep the two in line). EffectIds.IsAnimated,
// EffectIds.UsesDeviceColor, EffectColors.Takes, EffectColors.Default and EffectDirection.Applies
// look up their answer here; they keep their names, so an older settings app and a newer engine,
// or the other way round, still agree.
//  - IsAnimated: the effect moves, so its speed matters.
//  - UsesDeviceColor: the effect shows the color of the device (LightingInfo.Color, the one "Op
//    het apparaat zetten" sets), also when only as the default of its own colors.
//  - TakesColorList: the effect takes a list of its own colors (EffectColors).
//  - DefaultColors: its colors without a list of its own; null is the one color of the device.
//  - Direction: whether it can run in a direction.
public sealed record EffectTraits(bool IsAnimated, bool UsesDeviceColor, bool TakesColorList, IReadOnlyList<RgbColor>? DefaultColors, DirectionUse Direction)
{
    // An effect the app does not know yet: still, with the device color, so it keeps the button.
    public static EffectTraits Unknown { get; } = new(IsAnimated: false, UsesDeviceColor: true, TakesColorList: false, DefaultColors: null, Direction: DirectionUse.Never);

    // An own effect (CustomEffects.IsCustom): its steps have their colors. Whether it moves depends
    // on its steps; the settings app offers the speed for every own effect.
    public static EffectTraits Custom { get; } = new(IsAnimated: false, UsesDeviceColor: false, TakesColorList: false, DefaultColors: null, Direction: DirectionUse.Never);

    // In the order the settings app lists them.
    private static readonly OrderedDictionary<string, EffectTraits> Known = new()
    {
        [EffectIds.Static] = new(IsAnimated: false, UsesDeviceColor: true, TakesColorList: false, DefaultColors: null, Direction: DirectionUse.Never),
        [EffectIds.Breathing] = new(IsAnimated: true, UsesDeviceColor: true, TakesColorList: false, DefaultColors: null, Direction: DirectionUse.Never),
        [EffectIds.Rainbow] = new(IsAnimated: true, UsesDeviceColor: false, TakesColorList: false, DefaultColors: null, Direction: DirectionUse.Always),
        [EffectIds.Wave] = new(IsAnimated: true, UsesDeviceColor: true, TakesColorList: false, DefaultColors: null, Direction: DirectionUse.Always),
        [EffectIds.ColorLoop] = new(IsAnimated: true, UsesDeviceColor: false, TakesColorList: false, DefaultColors: null, Direction: DirectionUse.WhenSpread),
        [EffectIds.Twinkle] = new(IsAnimated: true, UsesDeviceColor: true, TakesColorList: true, DefaultColors: null, Direction: DirectionUse.Never),
        [EffectIds.Comet] = new(IsAnimated: true, UsesDeviceColor: true, TakesColorList: true, DefaultColors: null, Direction: DirectionUse.Never),
        [EffectIds.Scanner] = new(IsAnimated: true, UsesDeviceColor: true, TakesColorList: true, DefaultColors: null, Direction: DirectionUse.Never),
        [EffectIds.Fire] = new(IsAnimated: true, UsesDeviceColor: false, TakesColorList: true, DefaultColors: EffectColors.DefaultFire, Direction: DirectionUse.Never),
        [EffectIds.PerLed] = new(IsAnimated: false, UsesDeviceColor: true, TakesColorList: false, DefaultColors: null, Direction: DirectionUse.Never),
    };

    // The effects the app knows, in the order the settings app lists them.
    public static IReadOnlyList<string> Ids { get; } = [.. Known.Keys];

    // The traits of an effect of Ids, of an own effect, or else Unknown.
    public static EffectTraits Of(string? effectId) =>
        effectId is not null && Known.TryGetValue(effectId, out var traits) ? traits
        : CustomEffects.IsCustom(effectId) ? Custom
        : Unknown;

    // Whether the effect runs in a direction, with the color loop spread over the LEDs or not.
    public bool RunsInDirection(bool spread) => Direction == DirectionUse.Always || (Direction == DirectionUse.WhenSpread && spread);

    // The colors of the effect without a list of its own on a device of deviceColor.
    public IReadOnlyList<RgbColor> DefaultColorsFor(RgbColor deviceColor) => DefaultColors ?? [deviceColor];
}
