using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Programs;

// The RGB software that may drive the same LEDs as OpenRGB, by the exe names of its processes: the
// one place for this list (docs/installatie.md, "Andere RGB-software afsluiten", explains it to
// the user, with the sources). Most names come from sites with process information, not from the
// makers, so some may be off or missing; a name that is wrong only never matches. Software with
// a service is found by that, so also while its window is closed. In the order the app shows them.
internal static class OtherRgbSoftwareList
{
    private const string ExeExtension = ".exe";

    public static IReadOnlyList<Entry> All { get; } =
    [
        // Officially only the service is known, SignalRgbService ("SignalRGB" or "VortxEngine" in
        // services.msc); that its exe has the same name, and the name of the app itself, are
        // assumptions, not confirmed.
        new("signalrgb", "SignalRGB", ["SignalRgbService.exe", "SignalRgb.exe", "VortxEngine.exe"]),

        // The service and its helper for low-level access (file.net, not official); iCUE.exe is
        // the app itself.
        new("icue", "Corsair iCUE", ["Corsair.Service.exe", "CueLLAccessService.exe", "iCUE.exe"]),

        // Synapse 3 (partly official, Razer support) and Chroma; the names of Synapse 4 are not
        // known.
        new("razer", "Razer Synapse", ["RazerCentralService.exe", "Razer Synapse Service.exe", "Razer Synapse Service Process.exe", "RzSDKService.exe", "RzSDKServer.exe"]),

        // Aura's LightingService and ROG Live Service (not official); the name of the service of
        // Armoury Crate itself is not known.
        new("armourycrate", "ASUS Armoury Crate", ["LightingService.exe", "ROGLiveService.exe"]),

        // MSI Center and Mystic Light (not official).
        new("mysticlight", "MSI Mystic Light", ["LEDKeeper2.exe", "Mystic_Light_Service.exe", "mysticlight2_service.exe", "LightKeeperService.exe"]),

        // No process or service name of RGB Fusion or Gigabyte Control Center was found; this is a
        // guess after the name of the program, not confirmed.
        new("rgbfusion", "Gigabyte RGB Fusion", ["RGBFusion.exe"]),

        // A second OpenRGB next to the service: see BesideService.
        new(OtherRgbSoftware.OpenRgbId, "OpenRGB", ["OpenRGB.exe"], BesideService: true),
    ];

    // By the process name as Process.ProcessName gives it, without ".exe"; Windows does not tell
    // file names apart by case. After All, which it reads.
    private static readonly Dictionary<string, int> ByProcess = CreateLookup();

    // The software among processes, each once, in the order of All. A process of an entry with
    // BesideService only counts outside the session of the services, and only while a process of
    // the same name runs there too.
    public static IReadOnlyList<OtherRgbSoftware> FindIn(IReadOnlyList<RunningProcess> processes)
    {
        var inServiceSession = new bool[All.Count];
        var inUserSession = new bool[All.Count];
        foreach (var process in processes)
        {
            if (!ByProcess.TryGetValue(process.Name, out var index))
            {
                continue;
            }

            if (process.SessionId == RunningProcess.ServiceSessionId)
            {
                inServiceSession[index] = true;
            }
            else
            {
                inUserSession[index] = true;
            }
        }

        return [.. All.Where((entry, i) => entry.BesideService ? inServiceSession[i] && inUserSession[i] : inServiceSession[i] || inUserSession[i]).Select(entry => entry.Software)];
    }

    public static bool IsKnown(string id) => All.Any(entry => entry.Id == id);

    private static Dictionary<string, int> CreateLookup()
    {
        var lookup = new Dictionary<string, int>(ProgramNames.Comparer);
        for (var i = 0; i < All.Count; i++)
        {
            foreach (var process in All[i].Processes)
            {
                lookup.Add(process.EndsWith(ExeExtension, StringComparison.OrdinalIgnoreCase) ? process[..^ExeExtension.Length] : process, i);
            }
        }

        return lookup;
    }

    // One program: its id (OtherRgbSoftware.Id), its name and the exe names of its processes.
    // BesideService is for OpenRGB: the installer runs it as the service OpenRGB, as LocalSystem
    // in session 0, and the engine connects to it. An OpenRGB.exe in the session of a user next to
    // that is a second one, such as the OpenRGB app with its own server or the app as a client of
    // the service; both can set colors. The session comes with the process list, so telling them
    // apart costs nothing extra. Without a service (OpenRGB started by hand, as in
    // docs/ontwikkelaars.md) the one OpenRGB is the server the engine uses, and nothing is found.
    internal sealed record Entry(string Id, string Name, IReadOnlyList<string> Processes, bool BesideService = false)
    {
        public OtherRgbSoftware Software { get; } = new(Id, Name);
    }
}
