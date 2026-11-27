using BuurmansRgb.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// The choice when the simulated devices show. This only asks; MainViewModel sends the new mode to
// the engine, which changes the device list.
public sealed partial class SimulatedDevicesViewModel : ObservableObject
{
    private readonly ReentrancyGuard _showing = new();

    // Raised when the user picks another mode.
    public event EventHandler<SimulatedDevicesMode>? ModeChanged;

    public static IReadOnlyList<EnumOption<SimulatedDevicesMode>> Options { get; } =
        [.. Enum.GetValues<SimulatedDevicesMode>().Select(EnumOption<SimulatedDevicesMode>.Create)];

    [ObservableProperty]
    public partial EnumOption<SimulatedDevicesMode>? SelectedOption { get; set; }

    // Shows the mode of the engine, without asking for it again.
    public void Show(SimulatedDevicesMode mode) =>
        _showing.Run(() => SelectedOption = Options.FirstOrDefault(option => option.Mode == mode));

    partial void OnSelectedOptionChanged(EnumOption<SimulatedDevicesMode>? value)
    {
        if (!_showing.IsActive && value is not null)
        {
            ModeChanged?.Invoke(this, value.Mode);
        }
    }
}
