using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// Mixes one color (design/DESIGN.md § 5.2): the Rood, Groen and Blauw sliders, the hex code and the
// presets change Color at once, so the preview shows it, but the color only goes to the lamp with
// Apply ("Op de strip zetten"), which raises ColorPicked. Show shows the color the lamp has.
public sealed partial class ColorPickerViewModel : ObservableObject
{
    private readonly ReentrancyGuard _showing = new();

    // The color last shown or applied: what the lamp has.
    private RgbColor _applied = RgbColor.White;

    public event EventHandler<RgbColor>? ColorPicked;

    // "Uit de gereedschapskist", with the colors of the design.
    public static IReadOnlyList<ColorPreset> Presets { get; } =
    [
        new(new(255, 244, 229), Strings.PresetKlusWhite),
        new(new(255, 59, 59), Strings.PresetRed),
        new(new(255, 154, 61), Strings.PresetOrange),
        new(new(31, 196, 106), Strings.PresetGreen),
        new(new(47, 107, 255), Strings.PresetBlue),
        new(new(169, 123, 255), Strings.PresetPurple),
    ];

    // 0 to 255 each.
    [ObservableProperty]
    public partial double Red { get; set; } = 255;

    [ObservableProperty]
    public partial double Green { get; set; } = 255;

    [ObservableProperty]
    public partial double Blue { get; set; } = 255;

    [ObservableProperty]
    public partial string HexColor { get; set; } = RgbColor.White.ToHex();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RgbText), nameof(HasUnappliedChange))]
    public partial RgbColor Color { get; private set; } = RgbColor.White;

    // "255 · 154 · 61", as the preview of the design shows it.
    public string RgbText => UiText.Format(Strings.RgbText, Color.R, Color.G, Color.B);

    // The mixed color is not on the lamp yet.
    public bool HasUnappliedChange => Color != _applied;

    public void Show(RgbColor color) => _showing.Run(() =>
    {
        _applied = color;
        ShowColor(color);
        OnPropertyChanged(nameof(HasUnappliedChange));
    });

    [RelayCommand]
    private void Apply()
    {
        _applied = Color;
        OnPropertyChanged(nameof(HasUnappliedChange));
        ColorPicked?.Invoke(this, Color);
    }

    [RelayCommand]
    private void SelectPreset(RgbColor color) => _showing.Run(() => ShowColor(color));

    partial void OnRedChanged(double value) => ColorFromSliders();

    partial void OnGreenChanged(double value) => ColorFromSliders();

    partial void OnBlueChanged(double value) => ColorFromSliders();

    partial void OnHexColorChanged(string value)
    {
        if (!_showing.IsActive && RgbColor.TryParseHex(value.Trim(), out var color))
        {
            _showing.Run(() => ShowColor(color, updateHex: false));
        }
    }

    private void ColorFromSliders()
    {
        if (_showing.IsActive)
        {
            return;
        }

        var color = new RgbColor(ToByte(Red), ToByte(Green), ToByte(Blue));
        _showing.Run(() =>
        {
            Color = color;
            HexColor = color.ToHex();
        });
    }

    private void ShowColor(RgbColor color, bool updateHex = true)
    {
        Color = color;
        Red = color.R;
        Green = color.G;
        Blue = color.B;
        if (updateHex)
        {
            HexColor = color.ToHex();
        }
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}
