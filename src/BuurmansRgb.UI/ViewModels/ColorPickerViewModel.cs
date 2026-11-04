using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// Mixes one color (design/DESIGN.md § 5.2): the Rood, Groen and Blauw sliders, the hex code, the
// presets and the own colors change Color, and every mixed color goes on at once by raising
// ColorPicked, for the whole device as for a color of the loop or of chosen LEDs; the call queue of
// MainViewModel lets the last one of a drag win. With IsUnused, as for the rainbow, the effect uses
// no color of the device: a mixed color only shows, for the own colors, and raises nothing. Show
// shows the color the device has and raises nothing.
public sealed partial class ColorPickerViewModel : ObservableObject
{
    private readonly ReentrancyGuard _showing = new();

    // The color last shown or put on: what the device has.
    private RgbColor _applied = RgbColor.White;

    public event EventHandler<RgbColor>? ColorPicked;

    // "Kleuren": the colors of the design, after them red, green and blue at full strength, which
    // the tinted ones of the design are not, and then pink and the pastels the user asked for.
    public static IReadOnlyList<ColorPreset> Presets { get; } =
    [
        new(new(255, 244, 229), Strings.PresetWarmWhite),
        new(new(255, 59, 59), Strings.PresetRed),
        new(new(255, 154, 61), Strings.PresetOrange),
        new(new(31, 196, 106), Strings.PresetGreen),
        new(new(47, 107, 255), Strings.PresetBlue),
        new(new(169, 123, 255), Strings.PresetPurple),
        new(new(255, 0, 0), Strings.PresetPureRed),
        new(new(0, 255, 0), Strings.PresetPureGreen),
        new(new(0, 0, 255), Strings.PresetPureBlue),
        new(new(255, 105, 180), Strings.PresetPink),
        new(new(255, 182, 193), Strings.PresetLightPink),
        new(new(204, 153, 255), Strings.PresetLilac),
        new(new(152, 255, 204), Strings.PresetMint),
        new(new(137, 207, 240), Strings.PresetBabyBlue),
    ];

    // 0 to 255 each.
    [ObservableProperty]
    public partial double Red { get; set; } = 255;

    [ObservableProperty]
    public partial double Green { get; set; } = 255;

    [ObservableProperty]
    public partial double Blue { get; set; } = 255;

    // A code with or without "#" mixes its color as it is typed; one that is no color changes
    // nothing until the field loses the focus (EndHexEdit).
    [ObservableProperty]
    public partial string HexColor { get; set; } = RgbColor.White.ToHex();

    // Why the code typed last was not taken: it was no color, so the field shows the code of the
    // color again, and this says which code went and which color stays. Null again with a code that
    // is a color, another color, or a color that Show shows.
    [ObservableProperty]
    public partial string? HexError { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RgbText), nameof(HasColorNotOnDevice))]
    public partial RgbColor Color { get; private set; } = RgbColor.White;

    // "255 · 154 · 61", as the preview of the design shows it.
    public string RgbText => UiText.Format(Strings.RgbText, Color.R, Color.G, Color.B);

    // The picker shows a color that is not on the device. Outside IsUnused every mixed color goes
    // on at once (Mix), so this only stays true for a color mixed while IsUnused, as with the
    // rainbow, until Show shows the device again.
    public bool HasColorNotOnDevice => Color != _applied;

    // The effect of the device uses no color of its own, so a mixed color goes nowhere. Ending it
    // puts nothing on: MainViewModel shows the color of the device again.
    [ObservableProperty]
    public partial bool IsUnused { get; set; }

    // The hex field has the keyboard focus (the view says so); another window, such as after
    // Alt+Tab, takes the keyboard focus away too. Meanwhile Show leaves a code that is
    // no color yet alone, so a late answer does not rewrite a code that is being typed, such as
    // "#FF00F" after Backspace; EndHexEdit handles it once the focus goes. A whole code follows the
    // color shown as without the focus, for a click on a LED leaves the focus in the field.
    public bool IsHexFocused { get; set; }

    public void Show(RgbColor color) => _showing.Run(() =>
    {
        _applied = color;
        ShowColor(color, updateHex: !IsHexFocused || RgbColor.TryParseHex(HexColor.Trim(), out _));
        HexError = null;
        OnPropertyChanged(nameof(HasColorNotOnDevice));
    });

    // The hex field lost the focus. A code that is no color, such as "zzz" or "#12", makes way for
    // the code of the color, so the field shows what the color is, and HexError says so; an empty
    // field gets the code back without a message, as nothing was typed.
    public void EndHexEdit()
    {
        var text = HexColor.Trim();
        if (RgbColor.TryParseHex(text, out _))
        {
            return;
        }

        if (text.Length > 0)
        {
            ShowHexError(UiText.Format(Strings.HexCodeInvalidKept, text, Color.ToHex()));
        }

        _showing.Run(() => HexColor = Color.ToHex());
    }

    [RelayCommand]
    private void SelectPreset(RgbColor color) => Mix(() => ShowColor(color));

    partial void OnRedChanged(double value) => ColorFromSliders();

    partial void OnGreenChanged(double value) => ColorFromSliders();

    partial void OnBlueChanged(double value) => ColorFromSliders();

    partial void OnHexColorChanged(string value)
    {
        if (!_showing.IsActive && RgbColor.TryParseHex(value.Trim(), out var color))
        {
            HexError = null;
            Mix(() => ShowColor(color, updateHex: false));
        }
    }

    partial void OnColorChanged(RgbColor value) => HexError = null;

    // The same message again is news too: a screen reader says it again.
    private void ShowHexError(string error)
    {
        if (error == HexError)
        {
            OnPropertyChanged(nameof(HexError));
        }

        HexError = error;
    }

    private void ColorFromSliders()
    {
        if (_showing.IsActive)
        {
            return;
        }

        var color = new RgbColor(ToByte(Red), ToByte(Green), ToByte(Blue));
        Mix(() =>
        {
            Color = color;
            HexColor = color.ToHex();
        });
    }

    // A slider step that rounds to the same color sends nothing.
    private void Mix(Action mix)
    {
        _showing.Run(mix);
        if (!IsUnused && HasColorNotOnDevice)
        {
            _applied = Color;
            OnPropertyChanged(nameof(HasColorNotOnDevice));
            ColorPicked?.Invoke(this, Color);
        }
    }

    // A code in the field that already gives the color stays as it was typed, such as "ff00ff":
    // the color coming back from the engine does not rewrite the field under the cursor.
    private void ShowColor(RgbColor color, bool updateHex = true)
    {
        Color = color;
        Red = color.R;
        Green = color.G;
        Blue = color.B;
        if (updateHex && !(RgbColor.TryParseHex(HexColor.Trim(), out var typed) && typed == color))
        {
            HexColor = color.ToHex();
        }
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}
