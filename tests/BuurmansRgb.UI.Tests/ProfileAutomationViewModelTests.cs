using BuurmansRgb.Contracts;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class ProfileAutomationViewModelTests
{
    private readonly ProfileAutomationViewModel _automation = new(() => []);
    private readonly List<ProfileAutomationChange> _requests = [];

    public ProfileAutomationViewModelTests() => _automation.ApplyRequested += (_, change) => _requests.Add(change);

    [Fact]
    public void New_HasNoProfileAndCannotApply()
    {
        Assert.False(_automation.HasProfile);
        Assert.False(_automation.ApplyCommand.CanExecute(null));
        Assert.Equal("Vanzelf aanzetten", _automation.Header);
        Assert.Null(_automation.Error);
    }

    [Fact]
    public void Show_FillsInTheFieldsAndTheHeader()
    {
        _automation.Show("Nacht", new ProfileAutomation(new TimeOnly(7, 5), "eldenring.exe"));

        Assert.True(_automation.HasProfile);
        Assert.Equal("07:05", _automation.Times[0].Text);
        Assert.Equal("eldenring.exe", _automation.ProgramText);
        Assert.Equal("Vanzelf aanzetten: aan", _automation.Header);
        Assert.False(_automation.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void Show_ProfileWithoutAutomation_SaysOff()
    {
        _automation.Show("Werk", ProfileAutomation.None);

        Assert.Equal("Vanzelf aanzetten: uit", _automation.Header);
    }

    // A screen reader reads the error out when it changes, so not on every key that keeps it.
    [Fact]
    public void Error_IsOnlyRaisedWhenItChanges()
    {
        _automation.Show("Nacht", ProfileAutomation.None);
        var raised = 0;
        _automation.PropertyChanged += (_, e) => raised += e.PropertyName == nameof(ProfileAutomationViewModel.Error) ? 1 : 0;

        _automation.Times[0].Text = "2";
        _automation.Times[0].Text = "23";
        _automation.Times[0].Text = "23:";
        _automation.Times[0].Text = "23:00";

        Assert.Equal(2, raised);
    }

    [Theory]
    [InlineData("23:00", 23, 0)]
    [InlineData(" 7:30 ", 7, 30)]
    [InlineData("07.30", 7, 30)]
    public void Apply_ValidTime_AsksWithIt(string text, int hour, int minute)
    {
        _automation.Show("Nacht", ProfileAutomation.None);

        _automation.Times[0].Text = text;
        _automation.ApplyCommand.Execute(null);

        Assert.Equal([new ProfileAutomationChange("Nacht", new ProfileAutomation(new TimeOnly(hour, minute)))], _requests);
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("2300")]
    [InlineData("23:0")]
    [InlineData("half elf")]
    public void InvalidTime_SaysHowAndCannotApply(string text)
    {
        _automation.Show("Nacht", ProfileAutomation.None);

        _automation.Times[0].Text = text;

        Assert.Equal("Vul een tijd in zoals 23:00, of laat het leeg.", _automation.Error);
        Assert.True(_automation.HasError);
        Assert.False(_automation.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void Apply_ProgramWithoutExe_AsksWithTheFileName()
    {
        _automation.Show("Gamen", ProfileAutomation.None);

        _automation.ProgramText = "eldenring";
        _automation.ApplyCommand.Execute(null);

        Assert.Equal([new ProfileAutomationChange("Gamen", new ProfileAutomation(Program: "eldenring.exe"))], _requests);
    }

    [Fact]
    public void Show_ForegroundOnly_TicksIt()
    {
        _automation.Show("Film", new ProfileAutomation(Program: "vlc.exe", ForegroundOnly: true));
        Assert.True(_automation.ForegroundOnly);
        Assert.True(_automation.HasProgramText);

        _automation.Show("Gamen", new ProfileAutomation(Program: "eldenring.exe"));
        Assert.False(_automation.ForegroundOnly);
    }

    [Fact]
    public void Apply_ForegroundOnlyTicked_AsksWithIt()
    {
        _automation.Show("Film", new ProfileAutomation(Program: "vlc.exe"));
        Assert.False(_automation.ApplyCommand.CanExecute(null));

        _automation.ForegroundOnly = true;
        Assert.True(_automation.ApplyCommand.CanExecute(null));
        _automation.ApplyCommand.Execute(null);

        Assert.Equal([new ProfileAutomationChange("Film", new ProfileAutomation(Program: "vlc.exe", ForegroundOnly: true))], _requests);
    }

    // Without a program the choice means nothing: it is grey, and ticking it is no change.
    [Fact]
    public void ForegroundOnlyWithoutAProgram_IsGreyAndNoChange()
    {
        _automation.Show("Nacht", new ProfileAutomation(new TimeOnly(23, 0)));
        Assert.False(_automation.HasProgramText);

        _automation.ForegroundOnly = true;

        Assert.False(_automation.ApplyCommand.CanExecute(null));
        _automation.ProgramText = "vlc";
        Assert.True(_automation.HasProgramText);
        Assert.True(_automation.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void InvalidProgram_SaysHowAndCannotApply()
    {
        _automation.Show("Gamen", ProfileAutomation.None);

        _automation.ProgramText = "game?.exe";

        Assert.Equal("Vul de naam van een programma in zoals eldenring.exe, of laat het leeg.", _automation.Error);
        Assert.False(_automation.ApplyCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(@"C:\Users\Fiek\Desktop\Elden Ring.lnk")]
    [InlineData("start-game.bat")]
    public void ShortcutOrScript_SaysToPickTheExeAndCannotApply(string program)
    {
        _automation.Show("Gamen", ProfileAutomation.None);

        _automation.ProgramText = program;

        Assert.StartsWith("Dit is een snelkoppeling of script.", _automation.Error);
        Assert.False(_automation.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void Show_TicksTheDaysOfTheTime()
    {
        _automation.Show("Werk", new ProfileAutomation(new TimeOnly(9, 0), Days: WeekDays.Workdays));

        Assert.Equal([true, true, true, true, true, false, false], _automation.Times[0].Days.Select(day => day.IsChecked));
        Assert.Equal(["ma", "di", "wo", "do", "vr", "za", "zo"], _automation.Times[0].Days.Select(day => day.ShortName));
        Assert.Equal("maandag", _automation.Times[0].Days[0].ToString());
        Assert.True(_automation.Times[0].HasTime);
    }

    [Fact]
    public void Apply_OtherDays_AsksWithThem()
    {
        _automation.Show("Werk", new ProfileAutomation(new TimeOnly(9, 0)));

        _automation.Times[0].Days[5].IsChecked = false;
        _automation.Times[0].Days[6].IsChecked = false;
        _automation.ApplyCommand.Execute(null);

        Assert.Equal([new ProfileAutomationChange("Werk", new ProfileAutomation(new TimeOnly(9, 0), Days: WeekDays.Workdays))], _requests);
    }

    // A time on no day would never come.
    [Fact]
    public void NoDayTicked_SaysSoAndCannotApply()
    {
        _automation.Show("Werk", new ProfileAutomation(new TimeOnly(9, 0)));

        foreach (var day in _automation.Times[0].Days)
        {
            day.IsChecked = false;
        }

        Assert.Equal("Kies minstens één dag voor de tijd.", _automation.Error);
        Assert.False(_automation.ApplyCommand.CanExecute(null));
    }

    // Without a time the days count for nothing, so no day is fine then.
    [Fact]
    public void NoTime_DaysDoNotMatter()
    {
        _automation.Show("Gamen", new ProfileAutomation(Program: "eldenring.exe"));

        _automation.Times[0].Days[0].IsChecked = false;

        Assert.False(_automation.Times[0].HasTime);
        Assert.Null(_automation.Error);
        Assert.False(_automation.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void FindPrograms_ShowsTheProgramsWithAWindow()
    {
        var automation = new ProfileAutomationViewModel(() => ["eldenring.exe", "notepad.exe"]);
        Assert.False(automation.HasRunningPrograms);

        automation.FindProgramsCommand.Execute(null);

        Assert.Equal(["eldenring.exe", "notepad.exe"], automation.RunningPrograms);
        Assert.True(automation.HasRunningPrograms);
    }

    // From the list as it is, from "Bladeren" with its whole path: the field gets the file name.
    [Theory]
    [InlineData("eldenring.exe", "eldenring.exe")]
    [InlineData(@"C:\Games\ELDEN RING\Game\eldenring.exe", "eldenring.exe")]
    public void PickProgram_FillsInTheExeName(string picked, string expected)
    {
        _automation.Show("Gamen", ProfileAutomation.None);

        _automation.PickProgramCommand.Execute(picked);

        Assert.Equal(expected, _automation.ProgramText);
        Assert.True(_automation.ApplyCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(@"C:\Users\me\Desktop\Spel.lnk")]
    public void PickProgram_NothingOrNoProgram_LeavesTheField(string? picked)
    {
        _automation.Show("Gamen", new ProfileAutomation(Program: "eldenring.exe"));

        _automation.PickProgramCommand.Execute(picked);

        Assert.Equal("eldenring.exe", _automation.ProgramText);
    }

    [Fact]
    public void Apply_EmptyFields_TurnsItOff()
    {
        _automation.Show("Nacht", new ProfileAutomation(new TimeOnly(23, 0), "eldenring.exe"));

        _automation.Times[0].Text = "";
        _automation.ProgramText = "  ";
        _automation.ApplyCommand.Execute(null);

        Assert.Equal([new ProfileAutomationChange("Nacht", ProfileAutomation.None)], _requests);
    }

    // The same as saved, only written another way, is no change.
    [Fact]
    public void SameAsSavedInOtherWords_CannotApply()
    {
        _automation.Show("Nacht", new ProfileAutomation(new TimeOnly(7, 0), "eldenring.exe"));

        _automation.Times[0].Text = "7:00";
        _automation.ProgramText = "EldenRing";

        Assert.True(_automation.ApplyCommand.CanExecute(null));
        _automation.ProgramText = "eldenring";
        Assert.False(_automation.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void Clear_EmptiesTheFieldsAndHasNoProfile()
    {
        _automation.Show("Nacht", new ProfileAutomation(new TimeOnly(23, 0)));

        _automation.Clear();

        Assert.False(_automation.HasProfile);
        Assert.Equal("", _automation.Times[0].Text);
        Assert.Equal("Vanzelf aanzetten", _automation.Header);
    }

    [Fact]
    public void Show_MoreTimes_ShowsARowForEach()
    {
        _automation.Show("Ochtend", MorningAndWeekend);

        Assert.Equal(["07:00", "09:00"], _automation.Times.Select(row => row.Text));
        Assert.Equal([true, true, true, true, true, false, false], _automation.Times[0].Days.Select(day => day.IsChecked));
        Assert.Equal([false, false, false, false, false, true, true], _automation.Times[1].Days.Select(day => day.IsChecked));
        Assert.Equal(["Aanzetten om, tijd 1", "Aanzetten om, tijd 2"], _automation.Times.Select(row => row.TimeAccessibleName));
        Assert.Equal("Dagen van tijd 2", _automation.Times[1].DaysAccessibleName);
        Assert.Equal("Tijd 2 weghalen", _automation.Times[1].RemoveAccessibleName);
        Assert.Equal(["Tijd 1", "Tijd 2"], _automation.Times.Select(row => row.ToString()));
        Assert.False(_automation.ApplyCommand.CanExecute(null));
    }

    // There is always a field to type a time in.
    [Fact]
    public void Show_NoTime_ShowsOneEmptyRowOnEveryDay()
    {
        _automation.Show("Ochtend", MorningAndWeekend);

        _automation.Show("Gamen", new ProfileAutomation(Program: "eldenring.exe"));

        var row = Assert.Single(_automation.Times);
        Assert.Equal("", row.Text);
        Assert.All(row.Days, day => Assert.True(day.IsChecked));
    }

    [Fact]
    public void AddTime_ThenApply_AsksWithAllTimesFromEarlyToLate()
    {
        _automation.Show("Ochtend", new ProfileAutomation(new TimeOnly(9, 0), Days: WeekDays.Weekend));

        _automation.AddTimeCommand.Execute(null);
        Assert.Equal(2, _automation.Times.Count);
        Assert.Equal(2, _automation.Times[1].Number);
        Assert.False(_automation.ApplyCommand.CanExecute(null));
        _automation.Times[1].Text = "7:00";
        _automation.Times[1].Days[5].IsChecked = false;
        _automation.Times[1].Days[6].IsChecked = false;
        _automation.ApplyCommand.Execute(null);

        Assert.Equal([new ProfileAutomationChange("Ochtend", MorningAndWeekend)], _requests);
    }

    [Fact]
    public void AddTime_UpToTheMost()
    {
        _automation.Show("Uur", ProfileAutomation.None);

        while (_automation.AddTimeCommand.CanExecute(null))
        {
            _automation.AddTimeCommand.Execute(null);
        }

        Assert.Equal(ProfileAutomation.MaxTimes, _automation.Times.Count);
        _automation.RemoveTimeCommand.Execute(_automation.Times[^1]);
        Assert.True(_automation.AddTimeCommand.CanExecute(null));
    }

    [Fact]
    public void AddTime_WithoutAProfile_CannotAdd() => Assert.False(_automation.AddTimeCommand.CanExecute(null));

    [Fact]
    public void RemoveTime_ThenApply_AsksWithoutItAndNumbersTheRestAgain()
    {
        _automation.Show("Ochtend", MorningAndWeekend);

        _automation.RemoveTimeCommand.Execute(_automation.Times[0]);
        _automation.ApplyCommand.Execute(null);

        var row = Assert.Single(_automation.Times);
        Assert.Equal(1, row.Number);
        Assert.Equal("Tijd 1", row.ToString());
        Assert.Equal([new ProfileAutomationChange("Ochtend", new ProfileAutomation(new TimeOnly(9, 0), Days: WeekDays.Weekend))], _requests);
    }

    [Fact]
    public void RemoveTime_TheOnlyOne_EmptiesItsField()
    {
        _automation.Show("Nacht", new ProfileAutomation(new TimeOnly(23, 0), Days: WeekDays.Weekend));

        _automation.RemoveTimeCommand.Execute(_automation.Times[0]);

        var row = Assert.Single(_automation.Times);
        Assert.Equal("", row.Text);
        Assert.All(row.Days, day => Assert.True(day.IsChecked));
        Assert.False(_automation.RemoveTimeCommand.CanExecute(row));
        Assert.True(_automation.ApplyCommand.CanExecute(null));
    }

    // An empty row is no time: the same as saved.
    [Fact]
    public void EmptyRowAdded_CannotApply()
    {
        _automation.Show("Nacht", new ProfileAutomation(new TimeOnly(23, 0)));

        _automation.AddTimeCommand.Execute(null);

        Assert.Null(_automation.Error);
        Assert.False(_automation.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void InvalidOrDaylessSecondTime_SaysSoUntilItIsRemoved()
    {
        _automation.Show("Ochtend", MorningAndWeekend);

        _automation.Times[1].Text = "25:00";
        Assert.Equal("Tijd 2: vul een tijd in zoals 23:00, of laat het leeg.", _automation.Error);
        Assert.False(_automation.ApplyCommand.CanExecute(null));

        _automation.Times[1].Text = "09:00";
        _automation.Times[1].Days[5].IsChecked = false;
        _automation.Times[1].Days[6].IsChecked = false;
        Assert.Equal("Tijd 2: kies minstens één dag.", _automation.Error);

        var removed = _automation.Times[1];
        _automation.RemoveTimeCommand.Execute(removed);
        Assert.Null(_automation.Error);
        removed.Text = "half elf";
        Assert.Null(_automation.Error);
    }

    // The first time that is wrong, by the number of its field.
    [Fact]
    public void InvalidTimes_SayTheNumberOfTheFirst()
    {
        _automation.Show("Ochtend", MorningAndWeekend);
        _automation.AddTimeCommand.Execute(null);

        _automation.Times[2].Text = "half elf";
        Assert.Equal("Tijd 3: vul een tijd in zoals 23:00, of laat het leeg.", _automation.Error);

        _automation.Times[1].Text = "25:00";
        Assert.Equal("Tijd 2: vul een tijd in zoals 23:00, of laat het leeg.", _automation.Error);

        _automation.RemoveTimeCommand.Execute(_automation.Times[0]);
        Assert.Equal("Tijd 1: vul een tijd in zoals 23:00, of laat het leeg.", _automation.Error);
    }

    // As after "Toepassen": the same rows show what the engine saved, so the field with the focus
    // keeps it.
    [Fact]
    public void Show_AsManyTimes_KeepsTheRows()
    {
        _automation.Show("Ochtend", MorningAndWeekend);
        var rows = _automation.Times.ToList();

        _automation.Times[0].Text = "6:30";
        _automation.ApplyCommand.Execute(null);
        _automation.Show("Ochtend", _requests[0].Automation);

        Assert.Equal(rows.Count, _automation.Times.Count);
        Assert.All(rows.Zip(_automation.Times), pair => Assert.Same(pair.First, pair.Second));
        Assert.Equal(["06:30", "09:00"], _automation.Times.Select(row => row.Text));
        Assert.False(_automation.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void Show_FewerOrMoreTimes_KeepsTheFirstRows()
    {
        _automation.Show("Ochtend", MorningAndWeekend);
        var first = _automation.Times[0];

        _automation.Show("Nacht", new ProfileAutomation(new TimeOnly(23, 0)));
        Assert.Same(first, Assert.Single(_automation.Times));
        Assert.All(first.Days, day => Assert.True(day.IsChecked));

        _automation.Show("Ochtend", MorningAndWeekend);
        Assert.Same(first, _automation.Times[0]);
        Assert.Equal([false, false, false, false, false, true, true], _automation.Times[1].Days.Select(day => day.IsChecked));
        Assert.Equal(2, _automation.Times[1].Number);

        _automation.Times[1].Text = "25:00";
        Assert.True(_automation.HasError);
    }

    // From workdays to the weekend passes no day at all; a screen reader should not hear that.
    [Fact]
    public void Show_OtherDays_RaisesNoErrorOnTheWay()
    {
        _automation.Show("Werk", new ProfileAutomation(new TimeOnly(9, 0), Days: WeekDays.Workdays));
        var raised = 0;
        _automation.PropertyChanged += (_, e) => raised += e.PropertyName == nameof(ProfileAutomationViewModel.Error) ? 1 : 0;

        _automation.Show("Weekend", new ProfileAutomation(new TimeOnly(10, 0), Days: WeekDays.Weekend));

        Assert.Equal(0, raised);
        Assert.Null(_automation.Error);
    }

    // 07:00 on workdays and 09:00 in the weekend, as the engine saves it.
    private static ProfileAutomation MorningAndWeekend { get; } =
        new([new LoadTime(new TimeOnly(7, 0), WeekDays.Workdays), new LoadTime(new TimeOnly(9, 0), WeekDays.Weekend)]);
}
