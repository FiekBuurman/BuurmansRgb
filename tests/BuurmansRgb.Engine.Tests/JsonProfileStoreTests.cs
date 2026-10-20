using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

public sealed class JsonProfileStoreTests : IDisposable
{
    private readonly TempDirectory _directory = new();
    private readonly JsonProfileStore _store;

    public JsonProfileStoreTests() => _store = new JsonProfileStore(_directory.File("profiles.json"));

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Load_NoFile_ReturnsNoProfiles() => Assert.Empty(_store.Load());

    [Fact]
    public void Save_ThenLoad_RoundTripsTheLighting()
    {
        var loop = new ColorLoop([Red, Green], Spread: false);
        var lighting = new LightingState(
            StaticLighting(Orange, 0.5) with { Loop = loop },
            new Dictionary<string, DeviceLighting> { ["fan"] = new(Blue, 0.25, EffectIds.Rainbow, 2.0) },
            new Dictionary<string, IReadOnlyList<RgbColor?>> { ["strip"] = [Red, null, Green] })
        {
            LedColorZoneLengths = new Dictionary<string, IReadOnlyList<int>> { ["strip"] = [1, 2] },
        };

        _store.Save([new Profile("Gamen", lighting), new Profile("Nacht", Uniform(StaticLighting(Red, 0.1)))]);

        var loaded = _store.Load();
        Assert.Equal(["Gamen", "Nacht"], loaded.Select(profile => profile.Name));
        var gamen = loaded[0].Lighting;
        Assert.Equal(lighting.Global, gamen.Global);
        Assert.Equal(lighting.Devices["fan"], gamen.Devices["fan"]);
        Assert.Equal(lighting.LedColors["strip"], gamen.LedColors["strip"]);
        Assert.Equal(lighting.LedColorZoneLengths["strip"], gamen.LedColorZoneLengths["strip"]);
        Assert.Equal(StaticLighting(Red, 0.1), loaded[1].Lighting.Global);
    }

    [Fact]
    public void Save_WritesTheNameAndHexColors()
    {
        _store.Save([new Profile("Gamen", Uniform(StaticLighting(Orange)))]);

        var json = File.ReadAllText(_directory.File("profiles.json"));
        Assert.Contains("\"name\": \"Gamen\"", json);
        Assert.Contains("\"color\": \"#FF8800\"", json);
    }

    [Fact]
    public void Save_WritesTheTimeAsHoursAndMinutes()
    {
        _store.Save([new Profile("Nacht", Uniform(StaticLighting(Red))) { Automation = new ProfileAutomation(new TimeOnly(7, 5)) }]);

        Assert.Contains("\"loadAt\": \"07:05\"", File.ReadAllText(_directory.File("profiles.json")));
        Assert.Equal(new TimeOnly(7, 5), Assert.Single(_store.Load()).Automation.LoadAt);
    }

    [Fact]
    public void Save_WritesTheProgram()
    {
        _store.Save([new Profile("Gamen", Uniform(StaticLighting(Red))) { Automation = new ProfileAutomation(Program: "eldenring.exe") }]);

        Assert.Contains("\"program\": \"eldenring.exe\"", File.ReadAllText(_directory.File("profiles.json")));
        Assert.Equal("eldenring.exe", Assert.Single(_store.Load()).Automation.Program);
    }

    [Fact]
    public void Save_WithoutAutomation_WritesNoTimeOrProgram()
    {
        _store.Save([new Profile("Gamen", Uniform(StaticLighting(Red)))]);

        var json = File.ReadAllText(_directory.File("profiles.json"));
        Assert.DoesNotContain("loadAt", json);
        Assert.DoesNotContain("program", json);
    }

    // The next save writes the profile without them, so the trace says what went.
    [Fact]
    public void Load_TimeOrProgramThatDoesNotRead_IsTraced()
    {
        File.WriteAllText(_directory.File("profiles.json"), """
            { "profiles": [
                { "name": "Tijd", "loadAt": "25:00" },
                { "name": "Getal", "loadAt": 2300 },
                { "name": "Spel", "program": "game?.exe" },
                { "name": "Leeg", "loadAt": "", "program": " " }
            ] }
            """);
        using var trace = new TraceCapture();

        _store.Load();

        var messages = trace.Messages.Where(message => message.StartsWith("profiles.json:", StringComparison.Ordinal)).ToList();
        Assert.Contains(messages, message => message.Contains("'Tijd' has loadAt 25:00", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("'Getal' has loadAt 2300", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("'Spel' has program game?.exe", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, message => message.Contains("'Leeg'", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_ProgramMissingOrInvalid_LeavesTheProgramOff()
    {
        File.WriteAllText(_directory.File("profiles.json"), """
            { "profiles": [
                { "name": "A", "loadAt": "23:00" },
                { "name": "B", "program": "game?.exe" },
                { "name": "C", "program": 42 },
                { "name": "D", "program": "eldenring" }
            ] }
            """);

        var profiles = _store.Load();

        Assert.Equal(new ProfileAutomation(new TimeOnly(23, 0)), profiles[0].Automation);
        Assert.Equal(ProfileAutomation.None, profiles[1].Automation);
        Assert.Equal(ProfileAutomation.None, profiles[2].Automation);
        Assert.Equal("eldenring.exe", profiles[3].Automation.Program);
    }

    // Files from before the automation have no "loadAt"; a value that does not read leaves the
    // time off but keeps the profile and the other profiles.
    [Fact]
    public void Load_TimeMissingOrInvalid_LeavesTheTimeOff()
    {
        File.WriteAllText(_directory.File("profiles.json"), """
            { "profiles": [
                { "name": "A", "color": "#FF0000" },
                { "name": "B", "loadAt": "25:00" },
                { "name": "C", "loadAt": 2300 },
                { "name": "D", "loadAt": { "hour": 23 } },
                { "name": "E", "loadAt": null },
                { "name": "F", "loadAt": " 7:30 " }
            ] }
            """);

        var profiles = _store.Load();

        Assert.Equal(["A", "B", "C", "D", "E", "F"], profiles.Select(profile => profile.Name));
        Assert.Equal(Red, profiles[0].Lighting.Global.Color);
        Assert.All(profiles.Take(5), profile => Assert.Equal(ProfileAutomation.None, profile.Automation));
        Assert.Equal(new TimeOnly(7, 30), profiles[5].Automation.LoadAt);
    }

    [Fact]
    public void Load_SkipsProfilesWithoutAValidOrWithADoubleName()
    {
        File.WriteAllText(_directory.File("profiles.json"), """
            { "profiles": [
                { "name": "Gamen", "color": "#FF0000" },
                { "color": "#00FF00" },
                { "name": "  " },
                { "name": "gamen", "color": "#0000FF" },
                null
            ] }
            """);

        var profile = Assert.Single(_store.Load());
        Assert.Equal("Gamen", profile.Name);
        Assert.Equal(Red, profile.Lighting.Global.Color);
    }

    // A hand-edited count that does not read leaves out the zone lengths of that device, not the file.
    [Fact]
    public void Load_LedColorZoneLengthsThatAreNotNumbers_KeepsTheProfiles()
    {
        File.WriteAllText(_directory.File("profiles.json"), """
            { "profiles": [ { "name": "Gamen", "color": "#FF0000", "ledColors": { "strip": ["#00FF00"] },
              "ledColorZoneLengths": { "strip": ["1"], "fan": [2, 3] } } ] }
            """);

        var profile = Assert.Single(_store.Load());

        Assert.False(File.Exists(_store.BrokenFilePath));
        Assert.Equal(Red, profile.Lighting.Global.Color);
        Assert.Equal([Green], profile.Lighting.LedColors["strip"]);
        Assert.Equal(["fan"], profile.Lighting.LedColorZoneLengths.Keys);
    }

    [Fact]
    public void Load_InvalidJson_MovesTheFileAsideAndReturnsNoProfiles()
    {
        File.WriteAllText(_directory.File("profiles.json"), "{ not json");

        Assert.Empty(_store.Load());
        Assert.False(File.Exists(_directory.File("profiles.json")));
        Assert.Equal("{ not json", File.ReadAllText(_store.BrokenFilePath));
    }

    [Fact]
    public void Load_FileInUse_Throws()
    {
        _store.Save([new Profile("Gamen", Uniform(StaticLighting(Orange)))]);
        using var locked = new FileStream(_directory.File("profiles.json"), FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.Throws<IOException>(() => _store.Load());
    }
}
