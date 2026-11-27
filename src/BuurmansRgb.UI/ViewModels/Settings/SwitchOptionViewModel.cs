using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// An option on Instellingen that is a switch, starting as isOn, as the engine has it until said.
// This only asks; MainViewModel sends a change to the engine, which saves it.
public sealed partial class SwitchOptionViewModel(bool isOn) : ObservableObject
{
    private readonly ReentrancyGuard _showing = new();

    // Raised with the new value when the user switches the option.
    public event EventHandler<bool>? Switched;

    [ObservableProperty]
    public partial bool IsOn { get; set; } = isOn;

    // Shows the option of the engine, without sending it back.
    public void Show(bool isOn) => _showing.Run(() => IsOn = isOn);

    partial void OnIsOnChanged(bool value)
    {
        if (!_showing.IsActive)
        {
            Switched?.Invoke(this, value);
        }
    }
}
