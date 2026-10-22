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

    public const string PerLed = "per-led";

    // The effects that move, so their speed matters. EffectCatalogTests keeps this in line with
    // the effects themselves.
    public static bool IsAnimated(string? effectId) => effectId is Breathing or Rainbow or Wave or ColorLoop or Twinkle or Comet;
}
