using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts;

// The colors of "Kleuren" on Kleur, shared so the engine can make its start scenes from the same
// colors (StartScenes): the settings app gives them their names (ColorPickerViewModel.Presets).
public static class ToolboxColors
{
    public static RgbColor WarmWhite { get; } = new(255, 244, 229);

    public static RgbColor Red { get; } = new(255, 59, 59);

    public static RgbColor Orange { get; } = new(255, 154, 61);

    public static RgbColor Green { get; } = new(31, 196, 106);

    public static RgbColor Blue { get; } = new(47, 107, 255);

    public static RgbColor Purple { get; } = new(169, 123, 255);

    public static RgbColor PureRed { get; } = new(255, 0, 0);

    public static RgbColor PureGreen { get; } = new(0, 255, 0);

    public static RgbColor PureBlue { get; } = new(0, 0, 255);

    public static RgbColor Pink { get; } = new(255, 105, 180);

    public static RgbColor LightPink { get; } = new(255, 182, 193);

    public static RgbColor Lilac { get; } = new(204, 153, 255);

    public static RgbColor Mint { get; } = new(152, 255, 204);

    public static RgbColor BabyBlue { get; } = new(137, 207, 240);
}
