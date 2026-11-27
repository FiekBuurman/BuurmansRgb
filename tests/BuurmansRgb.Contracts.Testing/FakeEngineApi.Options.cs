using System.Globalization;
using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts.Testing;

// The options of Instellingen; each one is stored, so the next Get gives what was set.
public sealed partial class FakeEngineApi
{
    public event EventHandler? OptionsChanged;

    public void RaiseOptionsChanged() => OptionsChanged?.Invoke(this, EventArgs.Empty);

    public SimulatedDevicesMode SimulatedDevices { get; set; } = SimulatedDevicesMode.Automatic;

    public Task<SimulatedDevicesMode> GetSimulatedDevicesAsync(CancellationToken ct = default) => Task.FromResult(SimulatedDevices);

    public async Task SetSimulatedDevicesAsync(SimulatedDevicesMode mode, CancellationToken ct = default)
    {
        await Record($"simulated {mode}");
        SimulatedDevices = mode;
    }

    public bool HadRealDevices { get; set; }

    public Task<bool> GetHadRealDevicesAsync(CancellationToken ct = default) => Task.FromResult(HadRealDevices);

    public int FramesPerSecond { get; set; } = 30;

    public Task<int> GetFramesPerSecondAsync(CancellationToken ct = default) => Task.FromResult(FramesPerSecond);

    public async Task SetFramesPerSecondAsync(int framesPerSecond, CancellationToken ct = default)
    {
        await Record($"fps {framesPerSecond}");
        FramesPerSecond = framesPerSecond;
    }

    public bool PauseOnLockAndSleep { get; set; } = true;

    public Task<bool> GetPauseOnLockAndSleepAsync(CancellationToken ct = default) => Task.FromResult(PauseOnLockAndSleep);

    public async Task SetPauseOnLockAndSleepAsync(bool enabled, CancellationToken ct = default)
    {
        await Record($"pause on lock {enabled}");
        PauseOnLockAndSleep = enabled;
    }

    public PausedLightsMode PausedLights { get; set; }

    public Task<PausedLightsMode> GetPausedLightsAsync(CancellationToken ct = default) => Task.FromResult(PausedLights);

    public async Task SetPausedLightsAsync(PausedLightsMode mode, CancellationToken ct = default)
    {
        await Record($"paused lights {mode}");
        PausedLights = mode;
    }

    public TimeSpan SceneFade { get; set; } = Contracts.SceneFade.Default;

    public Task<TimeSpan> GetSceneFadeAsync(CancellationToken ct = default) => Task.FromResult(SceneFade);

    public async Task SetSceneFadeAsync(TimeSpan fade, CancellationToken ct = default)
    {
        await Record($"scene fade {fade.TotalSeconds.ToString(CultureInfo.InvariantCulture)}");
        SceneFade = fade;
    }

    public bool CatchUpSceneTimes { get; set; }

    public Task<bool> GetCatchUpSceneTimesAsync(CancellationToken ct = default) => Task.FromResult(CatchUpSceneTimes);

    public async Task SetCatchUpSceneTimesAsync(bool enabled, CancellationToken ct = default)
    {
        await Record($"catch up {enabled}");
        CatchUpSceneTimes = enabled;
    }

    public bool Hotkeys { get; set; } = true;

    public Task<bool> GetHotkeysAsync(CancellationToken ct = default) => Task.FromResult(Hotkeys);

    public async Task SetHotkeysAsync(bool enabled, CancellationToken ct = default)
    {
        await Record($"hotkeys {enabled}");
        Hotkeys = enabled;
    }

    // Off, as the engine in a test has it; never the registry of this pc.
    public bool AutoStart { get; set; }

    // False lets SetAutoStartAsync fail as when Windows does not let it change.
    public bool AutoStartChangeable { get; set; } = true;

    public Task<bool> GetAutoStartAsync(CancellationToken ct = default) => Task.FromResult(AutoStart);

    public async Task<bool> SetAutoStartAsync(bool enabled, CancellationToken ct = default)
    {
        await Record($"autostart {enabled}");
        if (!AutoStartChangeable)
        {
            return false;
        }

        AutoStart = enabled;
        return true;
    }

    public List<RgbColor> CustomColors { get; set; } = [];

    public Task<IReadOnlyList<RgbColor>> GetCustomColorsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RgbColor>>([.. CustomColors]);

    public async Task SetCustomColorsAsync(IReadOnlyList<RgbColor> colors, CancellationToken ct = default)
    {
        await Record($"custom colors {string.Join(" ", colors.Select(color => color.ToHex()))}");
        CustomColors = [.. colors];
    }
}
