using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// One lamp on the Lampen page: a device, whether it is on, its brightness, and the color it shows
// now. SwitchRequested is raised only when the user switches it, not for what Show shows.
public sealed partial class LampItem(DeviceItem device) : ObservableObject
{
    private readonly ReentrancyGuard _showing = new();

    public event EventHandler<bool>? SwitchRequested;

    // The name, type, group, location label and Direct warning, as the device list has them.
    public DeviceItem Device { get; } = device;

    public string DeviceId => Device.DeviceId!;

    public string SwitchAccessibleName => UiText.Format(Strings.LampSwitchAccessibleName, Device.Name);

    public string OpenAccessibleName => UiText.Format(Strings.LampOpenAccessibleName, Device.Name);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    public partial bool IsOn { get; set; } = true;

    // 0 to 1, as the engine has it.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    public partial double Brightness { get; private set; } = 1.0;

    // The brightest LED of the last frame, so a lamp with a moving effect shows it moving.
    [ObservableProperty]
    public partial RgbColor Color { get; private set; }

    // The lighting the engine gave last, for what the lamp shows in Details; null until it did.
    private LightingInfo? _lighting;

    // "type · look · brightness", such as "LED-strip · Warm wit · 100%", or "type · uit": on and off
    // are also said in words (§ 7). Until the engine told the lighting there is no look.
    public string Details
    {
        get
        {
            var state = !IsOn ? Strings.LampOff
                : Look is { } look ? UiText.Format(Strings.LampLookAndBrightness, look, UiText.Format(Strings.LampBrightness, Brightness * 100.0))
                : UiText.Format(Strings.LampBrightness, Brightness * 100.0);
            return Device.TypeName is { } type ? UiText.Format(Strings.DeviceDetailsWithType, type, state) : state;
        }
    }

    // Names the effects of the engine and the own effects by their id, such as "Disco"; without it,
    // or for an id it does not know, an own effect is "Eigen effect" (EffectOption.Create).
    internal Func<string, string?>? EffectName { get; set; }

    // The name of the one color the lamp shows ("Warm wit"), as with Statisch, Ademen, Golf, or
    // Komeet, Scanner, Fonkelen and Vuur with one color (SceneLook.OneColorOf); else the name of the
    // effect, as with Regenboog, Kleurenloop, Vuur, Per LED, an own effect, or Komeet with more
    // colors. From the lighting the engine has, not from the frames, so it changes with the color
    // and not with every frame.
    private string? Look =>
        _lighting is not { } lighting ? null
        : SceneLook.OneColorOf(lighting) is { } color ? ColorNames.StandaloneNameOf(color)
        : EffectName?.Invoke(lighting.EffectId) ?? EffectOption.Create(lighting.EffectId).DisplayName;

    public void Show(LightingInfo lighting) => _showing.Run(() =>
    {
        IsOn = lighting.On;
        Brightness = lighting.Brightness;
        ShowLook(lighting);
    });

    // The names of the effects changed, such as an own effect that got another name.
    internal void ShowEffectNames() => OnPropertyChanged(nameof(Details));

    // WPF names the list item, the card, after it for screen readers: the name the device list has.
    public override string ToString() => Device.DisplayName;

    // On or off as the whole house was just switched, until the engine tells what it did.
    public void Show(bool on) => _showing.Run(() => IsOn = on);

    // What the lamp of the same device showed before its device changed, until the next refresh.
    public void ShowAs(LampItem previous) => _showing.Run(() =>
    {
        IsOn = previous.IsOn;
        Brightness = previous.Brightness;
        Color = previous.Color;
        ShowLook(previous._lighting);
    });

    // Details changes only when the lighting does, such as each color of a drag on Kleur.
    private void ShowLook(LightingInfo? lighting)
    {
        if (lighting != _lighting)
        {
            _lighting = lighting;
            OnPropertyChanged(nameof(Details));
        }
    }

    // Leds holds three bytes per LED, as a DeviceFrame has them.
    internal void ShowLeds(byte[] leds)
    {
        var brightest = RgbColor.Black;
        var most = -1;
        for (var offset = 0; offset + 2 < leds.Length; offset += 3)
        {
            var sum = leds[offset] + leds[offset + 1] + leds[offset + 2];
            if (sum > most)
            {
                most = sum;
                brightest = new RgbColor(leds[offset], leds[offset + 1], leds[offset + 2]);
            }
        }

        Color = brightest;
    }

    partial void OnIsOnChanged(bool value)
    {
        if (!_showing.IsActive)
        {
            SwitchRequested?.Invoke(this, value);
        }
    }
}
