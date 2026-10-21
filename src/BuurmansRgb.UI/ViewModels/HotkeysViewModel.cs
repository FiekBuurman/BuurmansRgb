using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// The option of the hotkeys on Instellingen: whether Ctrl+Alt+Shift+A and Ctrl+Alt+Shift+U switch
// everything on and off. This only asks; MainViewModel sends a change to the engine, which saves
// it and registers or frees the keys.
public sealed partial class HotkeysViewModel : ObservableObject
{
    private readonly ReentrancyGuard _showing = new();

    // Raised when the user switches the hotkeys.
    public event EventHandler<bool>? EnabledChanged;

    // On, as the engine has it until said.
    [ObservableProperty]
    public partial bool Enabled { get; set; } = true;

    // Shows the option of the engine, without sending it back.
    public void Show(bool enabled) => _showing.Run(() => Enabled = enabled);

    partial void OnEnabledChanged(bool value)
    {
        if (!_showing.IsActive)
        {
            EnabledChanged?.Invoke(this, value);
        }
    }
}
