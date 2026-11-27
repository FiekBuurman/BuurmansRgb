namespace BuurmansRgb.Contracts.Tests;

public sealed class SceneAutomationTests
{
    [Fact]
    public void OneTime_IsAListOfOne() =>
        Assert.Equal(
            new SceneAutomation([new LoadTime(new TimeOnly(9, 0), WeekDays.Workdays)], ["eldenring.exe"]),
            new SceneAutomation(new TimeOnly(9, 0), "eldenring.exe", WeekDays.Workdays));

    [Fact]
    public void None_HasNoTimesAndNoProgram()
    {
        Assert.Empty(SceneAutomation.None.Times);
        Assert.Empty(SceneAutomation.None.Programs);
        Assert.Equal(SceneAutomation.None, new SceneAutomation(Times: null));
    }

    // Times and null for the programs is one constructor, not two to choose from.
    [Fact]
    public void TimesWithoutPrograms_HasNoProgram()
    {
        LoadTime[] times = [new LoadTime(new TimeOnly(7, 0))];

        Assert.Equal(new SceneAutomation(times), new SceneAutomation(times, null));
    }

    // A list changed afterwards does not change the automation.
    [Fact]
    public void New_CopiesTheTimes()
    {
        List<LoadTime> times = [new LoadTime(new TimeOnly(7, 0))];
        var automation = new SceneAutomation(times);

        times.Add(new LoadTime(new TimeOnly(9, 0)));

        Assert.Single(automation.Times);
    }

    // A record compares a list by reference; the times compare by what they hold.
    [Fact]
    public void Equals_ComparesTheTimesInOrder()
    {
        var morning = new LoadTime(new TimeOnly(7, 0), WeekDays.Workdays);
        var weekend = new LoadTime(new TimeOnly(9, 0), WeekDays.Weekend);

        Assert.Equal(new SceneAutomation([morning, weekend]), new SceneAutomation([morning with { }, weekend with { }]));
        Assert.Equal(new SceneAutomation([morning, weekend]).GetHashCode(), new SceneAutomation([morning with { }, weekend with { }]).GetHashCode());
        Assert.NotEqual(new SceneAutomation([morning, weekend]), new SceneAutomation([weekend, morning]));
        Assert.NotEqual(new SceneAutomation([morning]), new SceneAutomation([morning, weekend]));
        Assert.NotEqual(new SceneAutomation([morning]), new SceneAutomation([morning], ["eldenring.exe"]));
    }

    [Fact]
    public void Normalize_PutsTheTimesFromEarlyToLateAndDropsTheSecondsAndDoubles()
    {
        var automation = new SceneAutomation(
        [
            new LoadTime(new TimeOnly(23, 0, 59)),
            new LoadTime(new TimeOnly(9, 0), WeekDays.Weekend),
            new LoadTime(new TimeOnly(7, 0, 30), WeekDays.Workdays),
            new LoadTime(new TimeOnly(23, 0)),
        ]).Normalize();

        Assert.Equal(
            [new LoadTime(new TimeOnly(7, 0), WeekDays.Workdays), new LoadTime(new TimeOnly(9, 0), WeekDays.Weekend), new LoadTime(new TimeOnly(23, 0))],
            automation.Times);
    }

    // One time of day once, on the days of all of them.
    [Fact]
    public void Normalize_SameTimeOfDay_IsOneTimeOnAllItsDays()
    {
        var automation = new SceneAutomation(
        [
            new LoadTime(new TimeOnly(7, 0), WeekDays.Workdays),
            new LoadTime(new TimeOnly(9, 0), WeekDays.Saturday),
            new LoadTime(new TimeOnly(7, 0, 30), WeekDays.Monday | WeekDays.Saturday),
            new LoadTime(new TimeOnly(9, 0), WeekDays.Sunday),
        ]).Normalize();

        Assert.Equal(
            [new LoadTime(new TimeOnly(7, 0), WeekDays.Workdays | WeekDays.Saturday), new LoadTime(new TimeOnly(9, 0), WeekDays.Weekend)],
            automation.Times);
    }

    [Fact]
    public void Normalize_NoKnownDays_IsEveryDay() =>
        Assert.Equal(
            new SceneAutomation(new TimeOnly(7, 0)),
            new SceneAutomation([new LoadTime(new TimeOnly(7, 0), WeekDays.None), new LoadTime(new TimeOnly(7, 0), (WeekDays)128)]).Normalize());

    [Fact]
    public void Normalize_TheMostTimes_IsValidAndOneMoreThrows()
    {
        LoadTime[] times = [.. Enumerable.Range(0, SceneAutomation.MaxTimes + 1).Select(hour => new LoadTime(new TimeOnly(hour, 0)))];

        Assert.Equal(SceneAutomation.MaxTimes, new SceneAutomation(times[..^1]).Normalize().Times.Count);
        Assert.Throws<ArgumentException>(() => new SceneAutomation(times).Normalize());
    }

    // The same time twice counts once, also against the most.
    [Fact]
    public void Normalize_DoublesDoNotCountAgainstTheMost()
    {
        var times = Enumerable.Repeat(new LoadTime(new TimeOnly(7, 0)), SceneAutomation.MaxTimes + 1).ToList();

        Assert.Single(new SceneAutomation(times).Normalize().Times);
    }

    [Fact]
    public void ForegroundOnly_IsOffUnlessSaid()
    {
        Assert.False(SceneAutomation.None.ForegroundOnly);
        Assert.False(new SceneAutomation(Program: "eldenring.exe").ForegroundOnly);
        Assert.NotEqual(new SceneAutomation(Program: "eldenring.exe"), new SceneAutomation(Program: "eldenring.exe", ForegroundOnly: true));
        Assert.Equal(
            new SceneAutomation(Program: "eldenring.exe", ForegroundOnly: true).GetHashCode(),
            new SceneAutomation(Program: "eldenring.exe", ForegroundOnly: true).GetHashCode());
    }

    [Fact]
    public void Normalize_KeepsForegroundOnlyWithAProgram() =>
        Assert.Equal(
            new SceneAutomation(Program: "eldenring.exe", ForegroundOnly: true),
            new SceneAutomation(Program: "eldenring", ForegroundOnly: true).Normalize());

    // Without a program there is nothing to be in the foreground.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Normalize_ForegroundOnlyWithoutAProgram_IsOff(string? program) =>
        Assert.False(new SceneAutomation([new LoadTime(new TimeOnly(7, 0))], program is null ? null : [program], ForegroundOnly: true).Normalize().ForegroundOnly);

    // In the order given, each as ProgramNames gives it, without empty ones and doubles in any
    // case; ForegroundOnly counts for all of them.
    [Fact]
    public void Normalize_Programs_KeepsTheOrderAndDropsEmptyOnesAndDoubles() =>
        Assert.Equal(
            new SceneAutomation(Times: null, Programs: ["valorant.exe", "cs2.exe", "r5apex.exe"], ForegroundOnly: true),
            new SceneAutomation(Times: null, Programs: [" valorant ", "", "cs2.exe", "  ", "VALORANT.EXE", @"C:\Games\r5apex.exe", "cs2", null!], ForegroundOnly: true).Normalize());

    [Fact]
    public void Normalize_Programs_OneInvalidThrows() =>
        Assert.Throws<ArgumentException>(() => new SceneAutomation(Times: null, Programs: ["cs2.exe", "game?.exe"]).Normalize());

    [Fact]
    public void Normalize_TheMostPrograms_IsValidAndOneMoreThrows()
    {
        string[] programs = [.. Enumerable.Range(0, SceneAutomation.MaxPrograms + 1).Select(i => $"game{i}.exe")];

        Assert.Equal(SceneAutomation.MaxPrograms, new SceneAutomation(Times: null, Programs: programs[..^1]).Normalize().Programs.Count);
        Assert.Throws<ArgumentException>(() => new SceneAutomation(Times: null, Programs: programs).Normalize());
    }

    // Doubles do not count against the most, as with the times.
    [Fact]
    public void Normalize_DoubleProgramsDoNotCountAgainstTheMost() =>
        Assert.Single(new SceneAutomation(Times: null, Programs: [.. Enumerable.Repeat("cs2.exe", SceneAutomation.MaxPrograms + 1)]).Normalize().Programs);

    // The order counts, as with the times, and so does the spelling the user gave.
    [Fact]
    public void Equals_ComparesTheProgramsInOrder()
    {
        Assert.Equal(new SceneAutomation(Times: null, Programs: ["a.exe", "b.exe"]), new SceneAutomation(Times: null, Programs: ["a.exe", "b.exe"]));
        Assert.Equal(new SceneAutomation(Times: null, Programs: ["a.exe", "b.exe"]).GetHashCode(), new SceneAutomation(Times: null, Programs: ["a.exe", "b.exe"]).GetHashCode());
        Assert.NotEqual(new SceneAutomation(Times: null, Programs: ["a.exe", "b.exe"]), new SceneAutomation(Times: null, Programs: ["b.exe", "a.exe"]));
        Assert.NotEqual(new SceneAutomation(Program: "a.exe"), new SceneAutomation(Times: null, Programs: ["a.exe", "b.exe"]));
        Assert.NotEqual(new SceneAutomation(Program: "a.exe"), new SceneAutomation(Program: "A.exe"));
    }

    // As the pipe can give it.
    [Fact]
    public void Normalize_MissingTime_Throws() =>
        Assert.Throws<ArgumentException>(() => new SceneAutomation([null!]).Normalize());
}
