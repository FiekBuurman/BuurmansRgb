namespace BuurmansRgb.Contracts.Tests;

public sealed class ProfileAutomationTests
{
    [Fact]
    public void OneTime_IsAListOfOne() =>
        Assert.Equal(
            new ProfileAutomation([new LoadTime(new TimeOnly(9, 0), WeekDays.Workdays)], "eldenring.exe"),
            new ProfileAutomation(new TimeOnly(9, 0), "eldenring.exe", WeekDays.Workdays));

    [Fact]
    public void None_HasNoTimesAndNoProgram()
    {
        Assert.Empty(ProfileAutomation.None.Times);
        Assert.Null(ProfileAutomation.None.Program);
        Assert.Equal(ProfileAutomation.None, new ProfileAutomation(Times: null));
    }

    // A list changed afterwards does not change the automation.
    [Fact]
    public void New_CopiesTheTimes()
    {
        List<LoadTime> times = [new LoadTime(new TimeOnly(7, 0))];
        var automation = new ProfileAutomation(times);

        times.Add(new LoadTime(new TimeOnly(9, 0)));

        Assert.Single(automation.Times);
    }

    // A record compares a list by reference; the times compare by what they hold.
    [Fact]
    public void Equals_ComparesTheTimesInOrder()
    {
        var morning = new LoadTime(new TimeOnly(7, 0), WeekDays.Workdays);
        var weekend = new LoadTime(new TimeOnly(9, 0), WeekDays.Weekend);

        Assert.Equal(new ProfileAutomation([morning, weekend]), new ProfileAutomation([morning with { }, weekend with { }]));
        Assert.Equal(new ProfileAutomation([morning, weekend]).GetHashCode(), new ProfileAutomation([morning with { }, weekend with { }]).GetHashCode());
        Assert.NotEqual(new ProfileAutomation([morning, weekend]), new ProfileAutomation([weekend, morning]));
        Assert.NotEqual(new ProfileAutomation([morning]), new ProfileAutomation([morning, weekend]));
        Assert.NotEqual(new ProfileAutomation([morning]), new ProfileAutomation([morning], "eldenring.exe"));
    }

    [Fact]
    public void Normalize_PutsTheTimesFromEarlyToLateAndDropsTheSecondsAndDoubles()
    {
        var automation = new ProfileAutomation(
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
        var automation = new ProfileAutomation(
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
            new ProfileAutomation(new TimeOnly(7, 0)),
            new ProfileAutomation([new LoadTime(new TimeOnly(7, 0), WeekDays.None), new LoadTime(new TimeOnly(7, 0), (WeekDays)128)]).Normalize());

    [Fact]
    public void Normalize_TheMostTimes_IsValidAndOneMoreThrows()
    {
        LoadTime[] times = [.. Enumerable.Range(0, ProfileAutomation.MaxTimes + 1).Select(hour => new LoadTime(new TimeOnly(hour, 0)))];

        Assert.Equal(ProfileAutomation.MaxTimes, new ProfileAutomation(times[..^1]).Normalize().Times.Count);
        Assert.Throws<ArgumentException>(() => new ProfileAutomation(times).Normalize());
    }

    // The same time twice counts once, also against the most.
    [Fact]
    public void Normalize_DoublesDoNotCountAgainstTheMost()
    {
        var times = Enumerable.Repeat(new LoadTime(new TimeOnly(7, 0)), ProfileAutomation.MaxTimes + 1).ToList();

        Assert.Single(new ProfileAutomation(times).Normalize().Times);
    }

    [Fact]
    public void ForegroundOnly_IsOffUnlessSaid()
    {
        Assert.False(ProfileAutomation.None.ForegroundOnly);
        Assert.False(new ProfileAutomation(Program: "eldenring.exe").ForegroundOnly);
        Assert.NotEqual(new ProfileAutomation(Program: "eldenring.exe"), new ProfileAutomation(Program: "eldenring.exe", ForegroundOnly: true));
        Assert.Equal(
            new ProfileAutomation(Program: "eldenring.exe", ForegroundOnly: true).GetHashCode(),
            new ProfileAutomation(Program: "eldenring.exe", ForegroundOnly: true).GetHashCode());
    }

    [Fact]
    public void Normalize_KeepsForegroundOnlyWithAProgram() =>
        Assert.Equal(
            new ProfileAutomation(Program: "eldenring.exe", ForegroundOnly: true),
            new ProfileAutomation(Program: "eldenring", ForegroundOnly: true).Normalize());

    // Without a program there is nothing to be in the foreground.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Normalize_ForegroundOnlyWithoutAProgram_IsOff(string? program) =>
        Assert.False(new ProfileAutomation([new LoadTime(new TimeOnly(7, 0))], program, ForegroundOnly: true).Normalize().ForegroundOnly);

    // As the pipe can give it.
    [Fact]
    public void Normalize_MissingTime_Throws() =>
        Assert.Throws<ArgumentException>(() => new ProfileAutomation([null!]).Normalize());
}
