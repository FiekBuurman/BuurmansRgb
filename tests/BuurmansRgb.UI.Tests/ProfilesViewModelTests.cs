using BuurmansRgb.Contracts;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class ProfilesViewModelTests
{
    private readonly ProfilesViewModel _profiles = new();
    private readonly List<string> _requests = [];
    private readonly List<string> _automationRequests = [];

    public ProfilesViewModelTests()
    {
        _profiles.SaveRequested += (_, name) => _requests.Add($"save {name}");
        _profiles.LoadRequested += (_, name) => _requests.Add($"load {name}");
        _profiles.DeleteRequested += (_, name) => _requests.Add($"delete {name}");
        _profiles.AutomationNeeded += (_, name) => _automationRequests.Add(name);
    }

    [Fact]
    public void New_NothingCanRun()
    {
        Assert.False(_profiles.SaveCommand.CanExecute(null));
        Assert.False(_profiles.LoadCommand.CanExecute(null));
        Assert.False(_profiles.DeleteCommand.CanExecute(null));
        Assert.Equal("Opslaan", _profiles.SaveLabel);
    }

    [Fact]
    public void Save_AsksWithTheTrimmedName()
    {
        _profiles.Name = "  Gamen ";

        _profiles.SaveCommand.Execute(null);

        Assert.Equal(["save Gamen"], _requests);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("Twee\nregels")]
    public void Save_InvalidName_CannotRun(string name)
    {
        _profiles.Name = name;

        Assert.False(_profiles.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Name_OfAnExistingProfileInOtherCase_SaysOverwrite()
    {
        _profiles.Show(["Gamen", "Nacht"]);

        _profiles.Name = "gamen";

        Assert.Equal("Overschrijven", _profiles.SaveLabel);
    }

    [Fact]
    public void SelectName_FillsInTheNameAndAllowsLoadAndDelete()
    {
        _profiles.Show(["Gamen", "Nacht"]);

        _profiles.SelectedName = "Nacht";

        Assert.Equal("Nacht", _profiles.Name);
        Assert.True(_profiles.LoadCommand.CanExecute(null));
        _profiles.LoadCommand.Execute(null);
        Assert.Equal(["load Nacht"], _requests);
    }

    [Fact]
    public void Show_KeepsTheSelectionWhenItIsStillThere()
    {
        _profiles.Show(["Gamen", "Nacht"]);
        _profiles.SelectedName = "Nacht";

        _profiles.Show(["Gamen", "Nacht", "Werk"]);

        Assert.Equal("Nacht", _profiles.SelectedName);
    }

    [Fact]
    public void Show_WithSelect_SelectsThatProfile()
    {
        _profiles.Show(["Gamen", "Nacht"], select: "nacht");

        Assert.Equal("Nacht", _profiles.SelectedName);
    }

    [Fact]
    public void Show_SelectedProfileIsGone_SelectsNothingAndKeepsTheName()
    {
        _profiles.Show(["Gamen", "Nacht"]);
        _profiles.SelectedName = "Nacht";

        _profiles.Show(["Gamen"]);

        Assert.Null(_profiles.SelectedName);
        Assert.Equal("Nacht", _profiles.Name);
        Assert.Equal("Opslaan", _profiles.SaveLabel);
    }

    [Theory]
    [InlineData(true, new[] { "delete Gamen" })]
    [InlineData(false, new string[0])]
    public void Delete_AsksForConfirmationFirst(bool confirmed, string[] expected)
    {
        string? asked = null;
        _profiles.ConfirmDelete = name =>
        {
            asked = name;
            return confirmed;
        };
        _profiles.Show(["Gamen"], select: "Gamen");

        _profiles.DeleteCommand.Execute(null);

        Assert.Equal("Gamen", asked);
        Assert.Equal(expected, _requests);
    }

    [Fact]
    public void SelectName_AsksForItsAutomation()
    {
        _profiles.Show(["Gamen", "Nacht"]);

        _profiles.SelectedName = "Nacht";

        Assert.Equal(["Nacht"], _automationRequests);
    }

    // After a save, load or new automation the list is shown again: the fields show what was saved.
    [Fact]
    public void Show_SameSelection_AsksForItsAutomationAgain()
    {
        _profiles.Show(["Gamen", "Nacht"], select: "Nacht");

        _profiles.Show(["Gamen", "Nacht"], select: "Nacht");

        Assert.Equal(["Nacht", "Nacht"], _automationRequests);
    }

    [Fact]
    public void SelectAnotherName_EmptiesTheFieldsOfTheFirstRightAway()
    {
        _profiles.Show(["Gamen", "Nacht"], select: "Nacht");
        _profiles.Automation.Show("Nacht", new ProfileAutomation(new TimeOnly(23, 0)));

        _profiles.SelectedName = "Gamen";

        Assert.Null(_profiles.Automation.ProfileName);
        Assert.Equal("", _profiles.Automation.LoadAtText);
    }

    [Fact]
    public void SelectedProfileIsGone_EmptiesTheFieldsWithoutAsking()
    {
        _profiles.Show(["Gamen", "Nacht"], select: "Nacht");
        _profiles.Automation.Show("Nacht", new ProfileAutomation(new TimeOnly(23, 0)));

        _profiles.Show(["Gamen"]);

        Assert.False(_profiles.Automation.HasProfile);
        Assert.Equal(["Nacht"], _automationRequests);
    }
}
