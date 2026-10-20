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

    // "type · brightness", or "type · uit": on and off are also said in words (§ 7).
    public string Details
    {
        get
        {
            var state = IsOn ? UiText.Format(Strings.LampBrightness, Brightness * 100.0) : Strings.LampOff;
            return Device.TypeName is { } type ? UiText.Format(Strings.DeviceDetailsWithType, type, state) : state;
        }
    }

    public void Show(LightingInfo lighting) => _showing.Run(() =>
    {
        IsOn = lighting.On;
        Brightness = lighting.Brightness;
    });

    // On or off as the whole house was just switched, until the engine tells what it did.
    public void Show(bool on) => _showing.Run(() => IsOn = on);

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
