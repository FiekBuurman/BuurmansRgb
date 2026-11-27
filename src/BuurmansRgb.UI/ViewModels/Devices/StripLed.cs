using BuurmansRgb.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// One bulb of the strip on the "Hele huis" card: an LED of one of the lamps, dark when that lamp is off.
public sealed partial class StripLed : ObservableObject
{
    [ObservableProperty]
    public partial RgbColor Color { get; set; }

    [ObservableProperty]
    public partial bool IsLit { get; set; }
}
