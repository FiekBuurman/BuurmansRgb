using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Resources;

namespace BuurmansRgb.Engine.Profiles;

// The scenes a new installation starts with (ProfileLibrary.AddStartScenes), so Scènes and the
// tray menu have something to try at once. Each is one lighting for all devices (Global, no device
// of its own), so it also takes a device that comes later. Built from the effects there are: a
// loop of four pastels is the color loop with its own colors. The names are in the language of
// Strings.resx when they are made; after that they are saved names like any other.
internal static class StartScenes
{
    // As "Rustig wit", but off: "Alles aan" after it gives that calm light, not full white.
    public static LightingState AllOff { get; } = Everywhere(CalmWhiteLighting with { On = false });

    // Warm wit of Kleuren at 40 %.
    public static LightingState CalmWhite { get; } = Everywhere(CalmWhiteLighting);

    public static LightingState Rainbow { get; } = Everywhere(DeviceLighting.Default with { EffectId = EffectIds.Rainbow });

    // Lichtroze, Lila, Mint and Babyblauw of Kleuren, spread over the LEDs as the rainbow.
    public static LightingState Pastel { get; } = Everywhere(DeviceLighting.Default with
    {
        EffectId = EffectIds.ColorLoop,
        Loop = new ColorLoop([ToolboxColors.LightPink, ToolboxColors.Lilac, ToolboxColors.Mint, ToolboxColors.BabyBlue]),
    });

    // A comet that is red one round and blue the next: Fel rood and Fel blauw of Kleuren.
    public static LightingState Gaming { get; } = Everywhere(DeviceLighting.Default with
    {
        Color = ToolboxColors.PureRed,
        EffectId = EffectIds.Comet,
        EffectColors = EffectColors.None.With(EffectIds.Comet, [ToolboxColors.PureRed, ToolboxColors.PureBlue]),
    });

    private static DeviceLighting CalmWhiteLighting => new(ToolboxColors.WarmWhite, 0.4, EffectIds.Static);

    // In the order of the panel; the library sorts them by name.
    public static IReadOnlyList<Profile> Create() =>
    [
        new(Strings.StartSceneAllOff, AllOff),
        new(Strings.StartSceneCalmWhite, CalmWhite),
        new(Strings.StartSceneRainbow, Rainbow),
        new(Strings.StartScenePastel, Pastel),
        new(Strings.StartSceneGaming, Gaming),
    ];

    private static LightingState Everywhere(DeviceLighting lighting) =>
        new(lighting, new Dictionary<string, DeviceLighting>(), new Dictionary<string, LedColorEntry>());
}
