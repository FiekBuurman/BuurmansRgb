using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Hotkeys;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Programs;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

public sealed class EngineApiListenerTests
{
    [Fact]
    public void Dispose_StopsListeningToEveryPartItListenedTo()
    {
        var simulatedDevices = SwitchableDeviceProvider.For(new SimulatedDeviceProvider());
        using var lighting = new LightingController([simulatedDevices]);
        using var simulatedSwitch = new SimulatedDevicesSwitch(simulatedDevices, lighting);
        using var zoneLengths = new ZoneLengthKeeper(lighting);
        var profiles = new ProfileLibrary(new InMemoryProfileStore(), lighting);
        var openRgb = new FakeServerStatus();
        var saveEvents = new SettingsSaveEvents();
        using var autoPause = AutoPauses.WithFakes(lighting.SetPaused);
        using var timer = new ProfileTimer(profiles, new FakeClockEvents(), TimeProvider.System);
        var hotkeys = new AllOnOffHotkeys();
        var customColors = new CustomColorList();
        using var customEffects = new CustomEffectLibrary(new InMemoryCustomEffectStore(), lighting);
        var autoStart = new AutoStartSwitch(new FakeAutoStart());
        using var otherRgbSoftware = new OtherRgbSoftwareCheck(new FakeProcessSnapshot());
        object[] parts = [lighting, simulatedSwitch, profiles, openRgb, saveEvents, autoPause, timer, hotkeys, customColors, customEffects, autoStart, otherRgbSoftware];
        var before = EventListeners.CountEach(parts);
        var api = new EngineApi(new EngineServices
        {
            Lighting = lighting,
            ZoneLengths = zoneLengths,
            Profiles = profiles,
            SimulatedDevices = simulatedSwitch,
            OpenRgb = openRgb,
            SaveEvents = saveEvents,
            AutoPause = autoPause,
            ProfileTimer = timer,
            Hotkeys = hotkeys,
            CustomColors = customColors,
            CustomEffects = customEffects,
            AutoStart = autoStart,
            OtherRgbSoftware = otherRgbSoftware,
        });
        var listening = EventListeners.CountEach(parts);

        api.Dispose();

        Assert.All(before.Zip(listening), counts => Assert.True(counts.Second > counts.First));
        Assert.Equal(before, EventListeners.CountEach(parts));
    }
}
