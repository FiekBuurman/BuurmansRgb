using BuurmansRgb.Contracts;
using BuurmansRgb.Contracts.Testing;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The options of Instellingen and the toolbox on Kleur, without MainViewModel: a session on a fake
// engine, and the view models that the coordinator shows the options in.
public sealed class EngineOptionsCoordinatorTests
{
    private static readonly RgbColor Red = new(255, 0, 0);

    private readonly FakeEngineConnection _connection = new();
    private readonly SimulatedDevicesViewModel _simulatedDevices = new();
    private readonly EffectOptionsViewModel _effectOptions = new();
    private readonly SwitchOptionViewModel _sceneTimes = new(isOn: false);
    private readonly SwitchOptionViewModel _hotkeys = new(isOn: true);
    private readonly SwitchOptionViewModel _autoStart = new(isOn: false);
    private readonly CustomColorsViewModel _customColors = new(new ColorPickerViewModel());
    private readonly EngineOptionsCoordinator _options;
    private readonly List<string> _messages = [];
    private bool? _hadRealDevices;

    public EngineOptionsCoordinatorTests()
    {
        var session = new EngineSession(new UiThread(), _ => { }, _ => { });
        _options = new EngineOptionsCoordinator(session, _simulatedDevices, _effectOptions, _sceneTimes, _hotkeys, _autoStart, _customColors);
        _options.Message += (_, message) => _messages.Add(message);
        _options.HadRealDevicesRead += (_, hadRealDevices) => _hadRealDevices = hadRealDevices;
        session.Attach(_connection);
    }

    private FakeEngineApi Api => _connection.FakeApi;

    // What the engine has shows without going back to it.
    [Fact]
    public async Task LoadAsync_ShowsTheOptionsOfTheEngine()
    {
        Api.SimulatedDevices = SimulatedDevicesMode.Never;
        Api.HadRealDevices = true;
        Api.FramesPerSecond = 60;
        Api.CatchUpSceneTimes = true;
        Api.Hotkeys = false;
        Api.AutoStart = true;
        Api.CustomColors = [Red];

        await _options.LoadAsync(Api);

        Assert.Equal(SimulatedDevicesMode.Never, _simulatedDevices.SelectedOption?.Mode);
        Assert.True(_hadRealDevices);
        Assert.Equal(60, _effectOptions.SelectedFrameRate?.FramesPerSecond);
        Assert.True(_sceneTimes.IsOn);
        Assert.False(_hotkeys.IsOn);
        Assert.True(_autoStart.IsOn);
        Assert.Equal([Red], _customColors.Colors.Select(item => item.Color));
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task ChangesOfTheUser_GoToTheEngine()
    {
        await _options.LoadAsync(Api);

        _simulatedDevices.SelectedOption = SimulatedDevicesViewModel.Options.Single(option => option.Mode == SimulatedDevicesMode.Always);
        _effectOptions.SelectedFrameRate = _effectOptions.FrameRates.Single(rate => rate.FramesPerSecond == 15);
        _sceneTimes.IsOn = true;
        _hotkeys.IsOn = false;

        Assert.Equal(["simulated Always", "fps 15", "catch up True", "hotkeys False"], Api.Calls);
    }

    // Windows may not let the registry change: the switch shows what the engine has again.
    [Fact]
    public async Task AutoStart_WhenWindowsRefuses_SaysSoAndShowsTheEngineAgain()
    {
        Api.AutoStartChangeable = false;
        await _options.LoadAsync(Api);

        _autoStart.IsOn = true;

        Assert.False(_autoStart.IsOn);
        Assert.Equal([Strings.AutoStartFailed], _messages);
        Assert.Equal(["autostart True"], Api.Calls);
    }

    // After OptionsChanged, such as the tray menu that switched "Starten met Windows".
    [Fact]
    public async Task Reload_ShowsWhatTheEngineHasNow()
    {
        await _options.LoadAsync(Api);
        Api.AutoStart = true;
        Api.Hotkeys = false;

        _options.Reload();

        Assert.True(_autoStart.IsOn);
        Assert.False(_hotkeys.IsOn);
        Assert.Empty(Api.Calls);
        Assert.Empty(_messages);
    }
}
