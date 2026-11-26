using BuurmansRgb.Core;
using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Hotkeys;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Programs;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Ipc;

// The parts of the engine an EngineApi passes on to a connected UI. The first five it always
// needs; without one of the others (in tests) the api has nothing of that part, as it says per call.
// One instance serves every connection: each connection gets its own EngineApi of the same parts.
internal sealed record EngineServices
{
    public required LightingController Lighting { get; init; }

    public required ZoneLengthKeeper ZoneLengths { get; init; }

    public required ProfileLibrary Profiles { get; init; }

    public required SimulatedDevicesSwitch SimulatedDevices { get; init; }

    public required IServerStatusSource OpenRgb { get; init; }

    public SettingsSaveEvents? SaveEvents { get; init; }

    // The clock of the preview frames; the system clock unless a test steps it by hand.
    public TimeProvider Time { get; init; } = TimeProvider.System;

    public AutoPause? AutoPause { get; init; }

    public ProfileTimer? ProfileTimer { get; init; }

    public AllOnOffHotkeys? Hotkeys { get; init; }

    public CustomColorList? CustomColors { get; init; }

    public ZoneCounter? ZoneCounter { get; init; }

    public CustomEffectLibrary? CustomEffects { get; init; }

    public AutoStartSwitch? AutoStart { get; init; }

    // Restarts the engine (RestartAsync); null when nothing can restart it.
    public Action? Restart { get; init; }

    public OtherRgbSoftwareCheck? OtherRgbSoftware { get; init; }

    public ISettingsTransfer? SettingsTransfer { get; init; }
}
