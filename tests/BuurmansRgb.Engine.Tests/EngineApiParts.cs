using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

// The five parts every EngineApi needs, on simulated devices, for a test class to keep as one field
// and dispose with the class. Services gives them for an api; a test adds what else it needs with
// "with { ... }".
internal sealed class EngineApiParts : IDisposable
{
    public EngineApiParts()
        : this(new InMemoryProfileStore())
    {
    }

    public EngineApiParts(InMemoryProfileStore profileStore)
    {
        ProfileStore = profileStore;
        Lighting = new LightingController([Simulated]);
        ZoneLengths = new ZoneLengthKeeper(Lighting);
        Profiles = new ProfileLibrary(ProfileStore, Lighting);
        SimulatedDevices = new SimulatedDevicesSwitch(Simulated, Lighting);
    }

    public SwitchableDeviceProvider Simulated { get; } = SwitchableDeviceProvider.For(new SimulatedDeviceProvider());

    public LightingController Lighting { get; }

    public InMemoryProfileStore ProfileStore { get; }

    public ProfileLibrary Profiles { get; }

    public SimulatedDevicesSwitch SimulatedDevices { get; }

    public ZoneLengthKeeper ZoneLengths { get; }

    public FakeServerStatus OpenRgb { get; } = new();

    public EngineServices Services => new()
    {
        Lighting = Lighting,
        ZoneLengths = ZoneLengths,
        Profiles = Profiles,
        SimulatedDevices = SimulatedDevices,
        OpenRgb = OpenRgb,
    };

    public void Dispose()
    {
        ZoneLengths.Dispose();
        SimulatedDevices.Dispose();
        Lighting.Dispose();
    }
}
