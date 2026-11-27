using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Hotkeys;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Programs;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

public sealed class EngineHostListenerTests
{
    [Fact]
    public async Task Dispose_StopsListeningToEveryPartItListenedTo()
    {
        var simulatedDevices = SwitchableDeviceProvider.For(new SimulatedDeviceProvider());
        using var lighting = new LightingController([simulatedDevices]);
        using var simulatedSwitch = new SimulatedDevicesSwitch(simulatedDevices, lighting);
        using var zoneLengths = new ZoneLengthKeeper(lighting);
        using var autoPause = AutoPauses.WithFakes(lighting.SetPaused);
        var profiles = new ProfileLibrary(new InMemoryProfileStore(), lighting);
        using var timer = new ProfileTimer(profiles, new FakeClockEvents(), TimeProvider.System);
        var hotkeys = new AllOnOffHotkeys();
        var customColors = new CustomColorList();
        var autoStart = new AutoStartSwitch(new FakeAutoStart());
        using var otherRgbSoftware = new OtherRgbSoftwareCheck(new FakeProcessSnapshot());
        var settingsFile = new FakeSettingsFileEvents();
        object[] parts = [lighting, simulatedSwitch, zoneLengths, autoPause, profiles, timer, hotkeys, customColors, autoStart, otherRgbSoftware, settingsFile];
        var before = EventListeners.CountEach(parts);
        var host = new EngineHost(
            new InMemorySettingsStore(),
            lighting,
            saveDelay: TimeSpan.FromHours(1),
            simulatedDevices: simulatedSwitch,
            zoneLengths: zoneLengths,
            autoPause: autoPause,
            profiles: profiles,
            profileTimer: timer,
            hotkeys: hotkeys,
            settingsFileEvents: settingsFile,
            customColors: customColors,
            autoStart: autoStart,
            otherRgbSoftware: otherRgbSoftware);
        await host.StartAsync(Ct);
        var started = EventListeners.CountEach(parts);

        host.Dispose();

        Assert.All(before.Zip(started), counts => Assert.True(counts.Second > counts.First));
        Assert.Equal(before, EventListeners.CountEach(parts));
    }
}
