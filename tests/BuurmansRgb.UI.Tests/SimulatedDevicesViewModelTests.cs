using BuurmansRgb.Contracts;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class SimulatedDevicesViewModelTests
{
    private readonly SimulatedDevicesViewModel _simulatedDevices = new();
    private readonly List<SimulatedDevicesMode> _requests = [];

    public SimulatedDevicesViewModelTests() => _simulatedDevices.ModeChanged += (_, mode) => _requests.Add(mode);

    [Fact]
    public void Options_AreEveryModeWithADutchName()
    {
        Assert.Equal(
            [(SimulatedDevicesMode.Automatic, "Automatisch"), (SimulatedDevicesMode.Always, "Altijd"), (SimulatedDevicesMode.Never, "Nooit")],
            SimulatedDevicesViewModel.Options.Select(option => (option.Mode, option.DisplayName)));
    }

    [Fact]
    public void Show_SelectsTheModeWithoutAsking()
    {
        _simulatedDevices.Show(SimulatedDevicesMode.Never);

        Assert.Equal(SimulatedDevicesMode.Never, _simulatedDevices.SelectedOption?.Mode);
        Assert.Empty(_requests);
    }

    [Fact]
    public void PickingAMode_AsksForIt()
    {
        _simulatedDevices.Show(SimulatedDevicesMode.Automatic);

        _simulatedDevices.SelectedOption = SimulatedDevicesViewModel.Options.Single(option => option.Mode == SimulatedDevicesMode.Always);

        Assert.Equal([SimulatedDevicesMode.Always], _requests);
    }

    [Fact]
    public void ClearingTheSelection_AsksNothing()
    {
        _simulatedDevices.Show(SimulatedDevicesMode.Automatic);

        _simulatedDevices.SelectedOption = null;

        Assert.Empty(_requests);
    }
}
