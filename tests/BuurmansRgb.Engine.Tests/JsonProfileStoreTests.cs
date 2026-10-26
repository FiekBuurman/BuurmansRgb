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
            StaticLighting(Orange, 0.5) with { Loop = loop, EffectColors = EffectColors.None.With(EffectIds.Fire, [Blue, Green]) },
            new Dictionary<string, DeviceLighting> { ["fan"] = new(Blue, 0.25, EffectIds.Comet, 2.0) { EffectColors = EffectColors.None.With(EffectIds.Comet, [Red]) } },
            new Dictionary<string, LedColorEntry> { ["strip"] = new([Red, null, Green], [1, 2]) });

        _store.Save([new Profile("Gamen", lighting), new Profile("Nacht", Uniform(StaticLighting(Red, 0.1)))]);

        var loaded = _store.Load();
        Assert.Equal(["Gamen", "Nacht"], loaded.Select(profile => profile.Name));
        var gamen = loaded[0].Lighting;
        Assert.Equal(lighting.Global, gamen.Global);
        Assert.Equal(lighting.Devices["fan"], gamen.Devices["fan"]);
        Assert.Equal(lighting.LedColors["strip"].Colors, gamen.LedColors["strip"].Colors);
        Assert.Equal(lighting.LedColors["strip"].ZoneLengths!, gamen.LedColors["strip"].ZoneLengths!);
        Assert.Equal(StaticLighting(Red, 0.1), loaded[1].Lighting.Global);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTwelveColors()
    {
        var lighting = Uniform(new DeviceLighting(Orange, 1.0, EffectIds.ColorLoop)
        {
            Loop = new ColorLoop(Twelve),
            EffectColors = EffectColors.None.With(EffectIds.Twinkle, Twelve),
        });

        _store.Save([new Profile("Kermis", lighting)]);

        Assert.Equal(lighting.Global, Assert.Single(_store.Load()).Lighting.Global);
    }

    // A scene from before the lists: every effect has its default colors.
    [Fact]
    public void Load_SceneWithoutEffectColors_GivesEveryEffectItsDefault()
    {
        File.WriteAllText(_directory.File("profiles.json"), """{ "profiles": [ { "name": "Kampvuur", "effect": "fire" } ] }""");

        var scene = Assert.Single(_store.Load());

        Assert.Equal(EffectIds.Fire, scene.Lighting.Global.EffectId);
        Assert.Equal(EffectColors.None, scene.Lighting.Global.EffectColors);
    }

    [Fact]
    public void Load_EffectColorsThatDoNotFit_AreLeftOutAndTheSceneStays()
    {
        File.WriteAllText(_directory.File("profiles.json"), """
            { "profiles": [ { "name": "Gif", "effect": "fire", "effectColors": { "fire": ["#003000", "groen", "#30FF30"], "static": ["#FF0000"], "comet": [] } } ] }
            """);

        var scene = Assert.Single(_store.Load());

        Assert.Equal(EffectColors.None.With(EffectIds.Fire, [new RgbColor(0, 48, 0), new RgbColor(48, 255, 48)]), scene.Lighting.Global.EffectColors);
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
    public void Load_ColorThatDoesNotRead_KeepsTheProfileAndTheOtherProfiles()
    {
        File.WriteAllText(_directory.File("profiles.json"), """
            {
              "profiles": [
                { "name": "Gamen", "color": "purple", "brightness": 0.5, "loadAt": "23:00" },
                { "name": "Nacht", "color": "#FF0000" }
              ]
            }
            """);

        var loaded = _store.Load();

        Assert.Equal(["Gamen", "Nacht"], loaded.Select(profile => profile.Name));
        Assert.Equal(DeviceLighting.Default.Color, loaded[0].Lighting.Global.Color);
        Assert.Equal(0.5, loaded[0].Lighting.Global.Brightness);
        Assert.Equal(new TimeOnly(23, 0), loaded[0].Automation.Times[0].At);
        Assert.Equal(Red, loaded[1].Lighting.Global.Color);
        Assert.False(File.Exists(_store.BrokenFilePath));
    }

    [Fact]
    public void Load_ValueOfTheWrongType_KeepsTheProfileAndTheOtherProfiles()
    {
        File.WriteAllText(_directory.File("profiles.json"), """
            {
              "profiles": [
                { "name": "Gamen", "color": "#FF0000", "brightness": "hoog", "on": "ja" },
                42,
                { "name": "Nacht", "color": "#FF0000", "speed": [1] }
              ]
            }
            """);

        var loaded = _store.Load();

        Assert.Equal(["Gamen", "Nacht"], loaded.Select(profile => profile.Name));
        Assert.Equal(Red, loaded[0].Lighting.Global.Color);
        Assert.Equal(DeviceLighting.Default.Brightness, loaded[0].Lighting.Global.Brightness);
        Assert.Equal(DeviceLighting.Default.On, loaded[0].Lighting.Global.On);
        Assert.Equal(DeviceLighting.Default.Speed, loaded[1].Lighting.Global.Speed);
        Assert.False(File.Exists(_store.BrokenFilePath));
    }

    // The same name twice in one scene, one of them of the wrong type: aside, as before.
    [Fact]
    public void Load_NameTwiceWithAValueOfTheWrongType_MovesTheFileAside()
    {
        File.WriteAllText(_directory.File("profiles.json"), """{ "profiles": [ { "name": "Gamen", "on": true, "on": "ja" } ] }""");

        Assert.Empty(_store.Load());
        Assert.True(File.Exists(_store.BrokenFilePath));
    }

    [Fact]
    public void Save_WritesTheTimeAsHoursAndMinutes()
    {
        _store.Save([new Profile("Nacht", Uniform(StaticLighting(Red))) { Automation = new ProfileAutomation(new TimeOnly(7, 5)) }]);

        Assert.Contains("\"loadAt\": \"07:05\"", File.ReadAllText(_directory.File("profiles.json")));
        Assert.Equal(new TimeOnly(7, 5), Assert.Single(_store.Load()).Automation.Times[0].At);
    }

    [Fact]
    public void Save_TimeOnSomeDays_WritesThemAndReadsThemBack()
    {
        var automation = new ProfileAutomation(new TimeOnly(9, 0), Days: WeekDays.Workdays);
        _store.Save([new Profile("Werk", Uniform(StaticLighting(Red))) { Automation = automation }]);

        Assert.Contains("\"loadOn\": \"mon,tue,wed,thu,fri\"", File.ReadAllText(_directory.File("profiles.json")));
        Assert.Equal(automation, Assert.Single(_store.Load()).Automation);
    }

    // Every day is how it was before there were days: no "loadOn" in the file.
    [Fact]
    public void Save_TimeOnEveryDay_WritesNoDays()
    {
        _store.Save([new Profile("Nacht", Uniform(StaticLighting(Red))) { Automation = new ProfileAutomation(new TimeOnly(23, 0)) }]);

        Assert.DoesNotContain("loadOn", File.ReadAllText(_directory.File("profiles.json")));
    }

    // One time stays as before, so an older engine still reads the file.
    [Fact]
    public void Save_OneTime_WritesNoLoadTimes()
    {
        _store.Save([new Profile("Werk", Uniform(StaticLighting(Red))) { Automation = new ProfileAutomation(new TimeOnly(9, 0), Days: WeekDays.Workdays) }]);

        Assert.DoesNotContain("loadTimes", File.ReadAllText(_directory.File("profiles.json")));
    }

    [Fact]
    public void Save_MoreTimes_WritesThemAsLoadTimesAndReadsThemBack()
    {
        var automation = new ProfileAutomation([new LoadTime(new TimeOnly(7, 0), WeekDays.Workdays), new LoadTime(new TimeOnly(9, 0), WeekDays.Weekend), new LoadTime(new TimeOnly(23, 0))]);
        _store.Save([new Profile("Ochtend", Uniform(StaticLighting(Red))) { Automation = automation }]);

        var json = File.ReadAllText(_directory.File("profiles.json"));
        Assert.DoesNotContain("loadAt", json);
        Assert.DoesNotContain("loadOn", json);
        Assert.Contains("\"at\": \"07:00\"", json);
        Assert.Contains("\"on\": \"mon,tue,wed,thu,fri\"", json);
        Assert.Contains("\"on\": \"sat,sun\"", json);
        Assert.Equal(automation, Assert.Single(_store.Load()).Automation);
    }

    // Edited by hand: "loadAt" and "loadTimes" both count, from early to late.
    [Fact]
    public void Load_LoadAtAndLoadTimes_CountsAllOfThem()
    {
        File.WriteAllText(_directory.File("profiles.json"), """
            { "profiles": [ { "name": "Ochtend", "loadAt": "23:00", "loadTimes": [ { "at": "7:00", "on": "ma,di" }, { "at": "09:00", "on": "sat,sun" } ] } ] }
            """);

        var automation = Assert.Single(_store.Load()).Automation;

        Assert.Equal(
            [new LoadTime(new TimeOnly(7, 0), WeekDays.Monday | WeekDays.Tuesday), new LoadTime(new TimeOnly(9, 0), WeekDays.Weekend), new LoadTime(new TimeOnly(23, 0))],
            automation.Times);
    }

    // A time that does not read goes; days that do not read leave that time on every day; the
    // same time twice counts once.
    [Fact]
    public void Load_LoadTimesThatDoNotRead_AreLeftOutAndTraced()
    {
        File.WriteAllText(_directory.File("profiles.json"), """
            { "profiles": [ { "name": "Ochtend", "program": "eldenring.exe", "loadTimes": [
                { "at": "25:00" },
                42,
                null,
                { "on": "mon" },
                { "at": 700 },
                { "at": "7:30", "on": "someday" },
                { "at": "09:00", "on": "sat,sun" },
                { "at": "09:00", "on": "sat,sun" }
            ] } ] }
            """);
        using var trace = new TraceCapture();

        var automation = Assert.Single(_store.Load()).Automation;

        Assert.Equal([new LoadTime(new TimeOnly(7, 30)), new LoadTime(new TimeOnly(9, 0), WeekDays.Weekend)], automation.Times);
        Assert.Equal("eldenring.exe", automation.Program);
        var messages = trace.Messages.Where(message => message.StartsWith("profiles.json:", StringComparison.Ordinal)).ToList();
        Assert.Contains(messages, message => message.Contains("'Ochtend' has loadTimes[0].at 25:00", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("'Ochtend' has loadTimes[4].at 700", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("'Ochtend' has loadTimes[5].on someday", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\"07:00\"")]
    [InlineData("{ \"at\": \"07:00\" }")]
    [InlineData("42")]
    public void Load_LoadTimesNotAList_IsLeftOutAndKeepsTheRest(string loadTimes)
    {
        File.WriteAllText(_directory.File("profiles.json"), $$"""{ "profiles": [ { "name": "Ochtend", "color": "#FF0000", "loadAt": "23:00", "loadTimes": {{loadTimes}} } ] }""");

        var profile = Assert.Single(_store.Load());

        Assert.Equal(Red, profile.Lighting.Global.Color);
        Assert.Equal(new ProfileAutomation(new TimeOnly(23, 0)), profile.Automation);
    }

    [Fact]
    public void Load_MoreThanTheMostTimes_KeepsTheFirst()
    {
        var times = string.Join(", ", Enumerable.Range(0, ProfileAutomation.MaxTimes + 2).Select(hour => $$"""{ "at": "{{hour + 10}}:00" }"""));
        File.WriteAllText(_directory.File("profiles.json"), $$"""{ "profiles": [ { "name": "Uur", "loadTimes": [ {{times}} ] } ] }""");
        using var trace = new TraceCapture();

        var automation = Assert.Single(_store.Load()).Automation;

        Assert.Equal(Enumerable.Range(10, ProfileAutomation.MaxTimes).Select(hour => new LoadTime(new TimeOnly(hour, 0))), automation.Times);
        Assert.Contains(trace.Messages, message => message.Contains("'Uur' has 10 times", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\"sat, sun\"", WeekDays.Weekend)]
    [InlineData("\"ma,wo,VR\"", WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday)]
    [InlineData("\"\"", WeekDays.Every)]
    [InlineData("\"someday\"", WeekDays.Every)]
    [InlineData("\"mon,someday\"", WeekDays.Every)]
    [InlineData("[\"mon\"]", WeekDays.Every)]
    public void Load_DaysWrittenByHand_AreRead_OrLeaveEveryDay(string loadOn, WeekDays expected)
    {
        File.WriteAllText(_directory.File("profiles.json"), $$"""{ "profiles": [ { "name": "Werk", "loadAt": "09:00", "loadOn": {{loadOn}} } ] }""");

        var automation = Assert.Single(_store.Load()).Automation;

        Assert.Equal(new TimeOnly(9, 0), Assert.Single(automation.Times).At);
        Assert.Equal(expected, automation.Times[0].Days);
    }

    [Fact]
    public void Save_WritesTheProgram()
    {
        _store.Save([new Profile("Gamen", Uniform(StaticLighting(Red))) { Automation = new ProfileAutomation(Program: "eldenring.exe") }]);

        Assert.Contains("\"program\": \"eldenring.exe\"", File.ReadAllText(_directory.File("profiles.json")));
        Assert.Equal("eldenring.exe", Assert.Single(_store.Load()).Automation.Program);
    }

    [Fact]
    public void Save_ForegroundOnly_WritesItNextToTheProgram()
    {
        _store.Save([new Profile("Gamen", Uniform(StaticLighting(Red))) { Automation = new ProfileAutomation(Program: "eldenring.exe", ForegroundOnly: true) }]);

        Assert.Contains("\"foregroundOnly\": true", File.ReadAllText(_directory.File("profiles.json")));
        Assert.Equal(new ProfileAutomation(Program: "eldenring.exe", ForegroundOnly: true), Assert.Single(_store.Load()).Automation);
    }

    // Off is what an older file means without it, so it is not written.
    [Fact]
    public void Save_ProgramWithoutForegroundOnly_WritesNoForegroundOnly()
    {
        _store.Save([new Profile("Gamen", Uniform(StaticLighting(Red))) { Automation = new ProfileAutomation(Program: "eldenring.exe") }]);

        Assert.DoesNotContain("foregroundOnly", File.ReadAllText(_directory.File("profiles.json")));
    }

    [Fact]
    public void Save_WithoutAutomation_WritesNoTimeOrProgram()
    {
        _store.Save([new Profile("Gamen", Uniform(StaticLighting(Red)))]);

        var json = File.ReadAllText(_directory.File("profiles.json"));
        Assert.DoesNotContain("loadAt", json);
        Assert.DoesNotContain("program", json);
        Assert.DoesNotContain("foregroundOnly", json);
    }

    // Missing, false or of the wrong type is off; without a program it means nothing.
    [Fact]
    public void Load_ForegroundOnly_IsOnOnlyWithTrueAndAProgram()
    {
        File.WriteAllText(_directory.File("profiles.json"), """
            { "profiles": [
                { "name": "A", "program": "a.exe", "foregroundOnly": true },
                { "name": "B", "program": "b.exe" },
                { "name": "C", "program": "c.exe", "foregroundOnly": false },
                { "name": "D", "program": "d.exe", "foregroundOnly": "yes" },
                { "name": "E", "loadAt": "23:00", "foregroundOnly": true },
                { "name": "F", "program": "game?.exe", "foregroundOnly": true }
            ] }
            """);

        var profiles = _store.Load();

        Assert.Equal(new ProfileAutomation(Program: "a.exe", ForegroundOnly: true), profiles[0].Automation);
        Assert.Equal(new ProfileAutomation(Program: "b.exe"), profiles[1].Automation);
        Assert.Equal(new ProfileAutomation(Program: "c.exe"), profiles[2].Automation);
        Assert.Equal(new ProfileAutomation(Program: "d.exe"), profiles[3].Automation);
        Assert.Equal(new ProfileAutomation(new TimeOnly(23, 0)), profiles[4].Automation);
        Assert.Equal(ProfileAutomation.None, profiles[5].Automation);
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
        Assert.Equal(new TimeOnly(7, 30), profiles[5].Automation.Times[0].At);
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
        Assert.Equal([Green], profile.Lighting.LedColors["strip"].Colors);
        Assert.Null(profile.Lighting.LedColors["strip"].ZoneLengths);
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
