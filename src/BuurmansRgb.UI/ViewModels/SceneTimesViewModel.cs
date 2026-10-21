using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// The option of the scene times on Instellingen: whether a time that passed while the pc slept or
// the engine did not run is caught up on. This only asks; MainViewModel sends a change to the
// engine, which saves it.
public sealed partial class SceneTimesViewModel : ObservableObject
{
    private readonly ReentrancyGuard _showing = new();

    // Raised when the user switches the catch-up.
    public event EventHandler<bool>? CatchUpChanged;

    // Off, as the engine has it until said.
    [ObservableProperty]
    public partial bool CatchUp { get; set; }

    // Shows the option of the engine, without sending it back.
    public void Show(bool catchUp) => _showing.Run(() => CatchUp = catchUp);

    partial void OnCatchUpChanged(bool value)
    {
        if (!_showing.IsActive)
        {
            CatchUpChanged?.Invoke(this, value);
        }
    }
}
