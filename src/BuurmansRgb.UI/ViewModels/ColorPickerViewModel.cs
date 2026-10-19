using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// Hex code, presets and hue/saturation sliders for one color. ColorPicked is raised only for a
// change the user made, not for a color shown with Show.
public sealed partial class ColorPickerViewModel : ObservableObject
{
    private readonly ReentrancyGuard _showing = new();
    private double _value = 1.0;

    public event EventHandler<RgbColor>? ColorPicked;

    public static IReadOnlyList<ColorPreset> Presets { get; } =
    [
        new(new(255, 0, 0), Strings.PresetRed),
        new(new(255, 136, 0), Strings.PresetOrange),
        new(new(255, 221, 0), Strings.PresetYellow),
        new(new(0, 255, 0), Strings.PresetGreen),
        new(new(0, 255, 255), Strings.PresetCyan),
        new(new(0, 0, 255), Strings.PresetBlue),
        new(new(136, 0, 255), Strings.PresetPurple),
        new(new(255, 0, 170), Strings.PresetPink),
        new(RgbColor.White, Strings.PresetWhite),
    ];

    // 0 to 360 degrees.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HueColor))]
    public partial double Hue { get; set; }

    // 0 to 100 percent.
    [ObservableProperty]
    public partial double Saturation { get; set; }

    [ObservableProperty]
    public partial string HexColor { get; set; } = RgbColor.White.ToHex();

    [ObservableProperty]
    public partial RgbColor Color { get; private set; } = RgbColor.White;

    // The fully saturated color of the current hue, for the saturation slider's gradient.
    public RgbColor HueColor => RgbColor.FromHsv(Hue / 360.0, 1.0, 1.0);

    public void Show(RgbColor color) => _showing.Run(() => ShowColor(color));

    [RelayCommand]
    private void SelectPreset(RgbColor color)
    {
        Show(color);
        ColorPicked?.Invoke(this, color);
    }

    partial void OnHueChanged(double value) => ColorFromSliders();

    partial void OnSaturationChanged(double value) => ColorFromSliders();

    partial void OnHexColorChanged(string value)
    {
        if (_showing.IsActive || !RgbColor.TryParseHex(value.Trim(), out var color))
        {
            return;
        }

        _showing.Run(() => ShowColor(color, updateHex: false));
        ColorPicked?.Invoke(this, color);
    }

    private void ColorFromSliders()
    {
        if (_showing.IsActive)
        {
            return;
        }

        var color = RgbColor.FromHsv(Hue / 360.0, Saturation / 100.0, _value);
        _showing.Run(() =>
        {
            Color = color;
            HexColor = color.ToHex();
        });
        ColorPicked?.Invoke(this, color);
    }

    // The sliders set hue and saturation; the value (darkness) of a typed color is kept, except
    // black, which would make the sliders do nothing.
    private void ShowColor(RgbColor color, bool updateHex = true)
    {
        Color = color;
        var (hue, saturation, value) = color.ToHsv();
        Hue = hue * 360.0;
        Saturation = saturation * 100.0;
        _value = value > 0.0 ? value : 1.0;
        if (updateHex)
        {
            HexColor = color.ToHex();
        }
    }
}
