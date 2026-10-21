using BuurmansRgb.Contracts;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class ProfileAutomationViewModelTests
{
    private readonly ProfileAutomationViewModel _automation = new();
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
        Assert.Equal("07:05", _automation.LoadAtText);
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

        _automation.LoadAtText = "2";
        _automation.LoadAtText = "23";
        _automation.LoadAtText = "23:";
        _automation.LoadAtText = "23:00";

        Assert.Equal(2, raised);
    }

    [Theory]
    [InlineData("23:00", 23, 0)]
    [InlineData(" 7:30 ", 7, 30)]
    [InlineData("07.30", 7, 30)]
    public void Apply_ValidTime_AsksWithIt(string text, int hour, int minute)
    {
        _automation.Show("Nacht", ProfileAutomation.None);

        _automation.LoadAtText = text;
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

        _automation.LoadAtText = text;

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

        Assert.Equal([true, true, true, true, true, false, false], _automation.Days.Select(day => day.IsChecked));
        Assert.Equal(["ma", "di", "wo", "do", "vr", "za", "zo"], _automation.Days.Select(day => day.ShortName));
        Assert.Equal("maandag", _automation.Days[0].ToString());
        Assert.True(_automation.HasLoadAt);
    }

    [Fact]
    public void Apply_OtherDays_AsksWithThem()
    {
        _automation.Show("Werk", new ProfileAutomation(new TimeOnly(9, 0)));

        _automation.Days[5].IsChecked = false;
        _automation.Days[6].IsChecked = false;
        _automation.ApplyCommand.Execute(null);

        Assert.Equal([new ProfileAutomationChange("Werk", new ProfileAutomation(new TimeOnly(9, 0), Days: WeekDays.Workdays))], _requests);
    }

    // A time on no day would never come.
    [Fact]
    public void NoDayTicked_SaysSoAndCannotApply()
    {
        _automation.Show("Werk", new ProfileAutomation(new TimeOnly(9, 0)));

        foreach (var day in _automation.Days)
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

        _automation.Days[0].IsChecked = false;

        Assert.False(_automation.HasLoadAt);
        Assert.Null(_automation.Error);
        Assert.False(_automation.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void Apply_EmptyFields_TurnsItOff()
    {
        _automation.Show("Nacht", new ProfileAutomation(new TimeOnly(23, 0), "eldenring.exe"));

        _automation.LoadAtText = "";
        _automation.ProgramText = "  ";
        _automation.ApplyCommand.Execute(null);

        Assert.Equal([new ProfileAutomationChange("Nacht", ProfileAutomation.None)], _requests);
    }

    // The same as saved, only written another way, is no change.
    [Fact]
    public void SameAsSavedInOtherWords_CannotApply()
    {
        _automation.Show("Nacht", new ProfileAutomation(new TimeOnly(7, 0), "eldenring.exe"));

        _automation.LoadAtText = "7:00";
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
        Assert.Equal("", _automation.LoadAtText);
        Assert.Equal("Vanzelf aanzetten", _automation.Header);
    }
}
