namespace BuurmansRgb.Contracts;

public static class EffectIds
{
    public const string Static = "static";

    public const string Breathing = "breathing";

    public const string Rainbow = "rainbow";

    public const string Wave = "wave";

    public const string ColorLoop = "color-loop";

    public const string Twinkle = "twinkle";

    public const string Comet = "comet";

    public const string Scanner = "scanner";

    public const string Fire = "fire";

    public const string PerLed = "per-led";

    // The effects that move, so their speed matters. EffectCatalogTests keeps this in line with
    // the effects themselves.
    public static bool IsAnimated(string? effectId) => effectId is Breathing or Rainbow or Wave or ColorLoop or Twinkle or Comet or Scanner or Fire;

    // Whether the effect shows the color of the device (LightingInfo.Color, the one "Op het
    // apparaat zetten" sets), also when only as the default of its own colors (EffectColors). Not
    // the rainbow, the color loop with its own loop and fire with its warm colors, nor an own
    // effect, whose steps have their colors. An effect the app does not know yet is taken to use it, so it
    // keeps the button. EffectCatalogTests keeps this in line with the effects themselves.
    public static bool UsesDeviceColor(string? effectId) =>
        effectId is not (Rainbow or ColorLoop or Fire) && !CustomEffects.IsCustom(effectId);
}
