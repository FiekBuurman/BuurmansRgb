using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

// The options of the engine that Instellingen and the toolbox on Kleur show: a change the user
// makes goes to the engine, and what the engine has shows when connecting and after
// OptionsChanged. A coordinator: it calls the engine of the session itself, and tells MainViewModel
// what is for the status line (Message) and whether this pc ever had a real device
// (HadRealDevicesRead). Used on the UI thread.
internal sealed class EngineOptionsCoordinator
{
    // The kind of call in the call queue that reads the options again; it changes nothing.
    internal const string OptionsReloadCall = "options-reload";

    // The kinds of call that change an option; a newer one replaces one that still waits.
    private const string SimulatedDevicesCall = "simulated-devices";
    private const string FramesPerSecondCall = "frames-per-second";
    private const string PauseOnLockCall = "pause-on-lock";
    private const string PausedLightsCall = "paused-lights";
    private const string SceneFadeCall = "scene-fade";
    private const string CatchUpCall = "catch-up";
    private const string HotkeysCall = "hotkeys";
    private const string AutoStartCall = "autostart";
    private const string CustomColorsCall = "custom-colors";

    private readonly EngineSession _session;
    private readonly SimulatedDevicesViewModel _simulatedDevices;
    private readonly EffectOptionsViewModel _effectOptions;
    private readonly SwitchOptionViewModel _sceneTimes;
    private readonly SwitchOptionViewModel _hotkeys;
    private readonly SwitchOptionViewModel _autoStart;
    private readonly CustomColorsViewModel _customColors;

    public EngineOptionsCoordinator(
        EngineSession session,
        SimulatedDevicesViewModel simulatedDevices,
        EffectOptionsViewModel effectOptions,
        SwitchOptionViewModel sceneTimes,
        SwitchOptionViewModel hotkeys,
        SwitchOptionViewModel autoStart,
        CustomColorsViewModel customColors)
    {
        _session = session;
        _simulatedDevices = simulatedDevices;
        _effectOptions = effectOptions;
        _sceneTimes = sceneTimes;
        _hotkeys = hotkeys;
        _autoStart = autoStart;
        _customColors = customColors;
        simulatedDevices.ModeChanged += (_, mode) => SendSimulatedDevices(mode);
        effectOptions.FramesPerSecondChanged += (_, framesPerSecond) =>
            SendOption(FramesPerSecondCall, api => api.SetFramesPerSecondAsync(framesPerSecond));
        effectOptions.PauseOnLockAndSleepChanged += (_, enabled) =>
            SendOption(PauseOnLockCall, api => api.SetPauseOnLockAndSleepAsync(enabled));
        effectOptions.PausedLightsChanged += (_, mode) =>
            SendOption(PausedLightsCall, api => api.SetPausedLightsAsync(mode));
        effectOptions.SceneFadeChanged += (_, fade) =>
            SendOption(SceneFadeCall, api => api.SetSceneFadeAsync(fade));
        sceneTimes.Switched += (_, enabled) =>
            SendOption(CatchUpCall, api => api.SetCatchUpSceneTimesAsync(enabled));
        hotkeys.Switched += (_, enabled) =>
            SendOption(HotkeysCall, api => api.SetHotkeysAsync(enabled));
        autoStart.Switched += (_, enabled) => SendAutoStart(enabled);
        customColors.ColorsChanged += (_, colors) =>
            SendOption(CustomColorsCall, api => api.SetCustomColorsAsync(colors));
    }

    // For the status line, such as "Starten met Windows" that Windows did not let change.
    public event EventHandler<string>? Message;

    // Whether a real device was ever in the list on this pc, as the engine recorded it.
    public event EventHandler<bool>? HadRealDevicesRead;

    // When connecting, before the rest of what the engine has.
    public async Task LoadAsync(IEngineApi api) => Show(await ReadAsync(api));

    // After OptionsChanged, such as the simulated lamps of another app: in the queue, behind the
    // changes of this app that wait.
    public void Reload() => _session.Read(OptionsReloadCall, ReadAsync, Show);

    private static async Task<OptionValues> ReadAsync(IEngineApi api) => new(
        await api.GetSimulatedDevicesAsync(),
        await api.GetHadRealDevicesAsync(),
        await api.GetFramesPerSecondAsync(),
        await api.GetPauseOnLockAndSleepAsync(),
        await api.GetPausedLightsAsync(),
        await api.GetSceneFadeAsync(),
        await api.GetCatchUpSceneTimesAsync(),
        await api.GetHotkeysAsync(),
        await api.GetAutoStartAsync(),
        await api.GetCustomColorsAsync());

    private void Show(OptionValues options)
    {
        _simulatedDevices.Show(options.SimulatedDevices);
        HadRealDevicesRead?.Invoke(this, options.HadRealDevices);
        _effectOptions.Show(options.FramesPerSecond, options.PauseOnLockAndSleep, options.PausedLights);
        _effectOptions.ShowSceneFade(options.SceneFade);
        _sceneTimes.Show(options.CatchUpSceneTimes);
        _hotkeys.Show(options.Hotkeys);
        _autoStart.Show(options.AutoStart);
        _customColors.Show(options.CustomColors);
    }

    // The engine shows or hides the simulated devices and reports the new list through
    // DevicesChanged, so the list of MainViewModel follows by itself.
    private void SendSimulatedDevices(SimulatedDevicesMode mode)
    {
        _session.Post(SimulatedDevicesCall, api => api.SetSimulatedDevicesAsync(mode));
    }

    // The engine saves the option itself; a pause that starts or ends by it comes through
    // PauseChanged.
    private void SendOption(string call, Func<IEngineApi, Task> send)
    {
        _session.Post(call, send);
    }

    // A change comes back through OptionsChanged. When Windows did not let it change, the switch
    // shows what the engine has again, with the message the tray menu gives too.
    private void SendAutoStart(bool enabled)
    {
        _session.PostOn(AutoStartCall, async connection =>
        {
            if (await connection.Api.SetAutoStartAsync(enabled))
            {
                return;
            }

            var current = await connection.Api.GetAutoStartAsync();
            _session.WhenCurrent(connection, () =>
            {
                _autoStart.Show(current);
                Message?.Invoke(this, Strings.AutoStartFailed);
            });
        });
    }

    // The options of the engine, read when connecting and after OptionsChanged.
    private sealed record OptionValues(
        SimulatedDevicesMode SimulatedDevices,
        bool HadRealDevices,
        int FramesPerSecond,
        bool PauseOnLockAndSleep,
        PausedLightsMode PausedLights,
        TimeSpan SceneFade,
        bool CatchUpSceneTimes,
        bool Hotkeys,
        bool AutoStart,
        IReadOnlyList<RgbColor> CustomColors);
}
