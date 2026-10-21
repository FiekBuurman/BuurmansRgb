using BuurmansRgb.Contracts;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class ProfilesViewModelSceneTests
{
    private readonly ProfilesViewModel _profiles = new();
    private readonly List<string> _requests = [];

    public ProfilesViewModelSceneTests()
    {
        _profiles.SaveRequested += (_, name) => _requests.Add($"save {name}");
        _profiles.LoadRequested += (_, name) => _requests.Add($"load {name}");
        _profiles.Show(["Filmavond", "Klusmodus"]);
    }

    [Fact]
    public void Show_MakesACardOfEveryProfile()
    {
        Assert.Equal(["Filmavond", "Klusmodus"], _profiles.Cards.Select(card => card.Name));
        Assert.True(_profiles.HasScenes);
    }

    [Fact]
    public void Show_CardThatStays_KeepsWhatItLooksLike()
    {
        _profiles.ShowCard("Klusmodus", new LightingInfo(Orange, 1.0, EffectIds.Static), ProfileAutomation.None);

        _profiles.Show(["Feestje", "Klusmodus"]);

        Assert.Equal(["Feestje", "Klusmodus"], _profiles.Cards.Select(card => card.Name));
        Assert.Equal("Statisch · 100%", _profiles.Cards[1].Description);
        Assert.Equal(string.Empty, _profiles.Cards[0].Description);
    }

    [Fact]
    public void Show_NoProfiles_HasNoScenes()
    {
        _profiles.Show([]);

        Assert.Empty(_profiles.Cards);
        Assert.False(_profiles.HasScenes);
    }

    [Fact]
    public void LoadScene_AsksForThatSceneWithoutChoosingIt()
    {
        _profiles.LoadSceneCommand.Execute(_profiles.Cards[1]);

        Assert.Equal(["load Klusmodus"], _requests);
        Assert.Null(_profiles.SelectedName);
    }

    [Fact]
    public void Edit_ChoosesTheSceneAndOverwriteSavesUnderIt()
    {
        Assert.False(_profiles.OverwriteCommand.CanExecute(null));
        Assert.Null(_profiles.EditTitle);

        _profiles.EditCommand.Execute(_profiles.Cards[0]);
        _profiles.OverwriteCommand.Execute(null);

        Assert.Equal("Filmavond", _profiles.SelectedName);
        Assert.Equal("Filmavond aanpassen", _profiles.EditTitle);
        Assert.True(_profiles.HasSelection);
        Assert.Equal(["save Filmavond"], _requests);
    }

    [Fact]
    public void StartNew_ShowsAnEmptyNameAndSavingHidesItAgain()
    {
        _profiles.EditCommand.Execute(_profiles.Cards[0]);

        _profiles.StartNewCommand.Execute(null);
        Assert.True(_profiles.IsNaming);
        Assert.Equal(string.Empty, _profiles.Name);
        Assert.Equal("Opslaan", _profiles.SaveLabel);

        _profiles.Name = "Feestje";
        _profiles.SaveCommand.Execute(null);

        Assert.False(_profiles.IsNaming);
        Assert.Equal(["save Feestje"], _requests);
    }

    [Fact]
    public void CancelNew_HidesTheNameWithoutSaving()
    {
        _profiles.StartNewCommand.Execute(null);

        _profiles.CancelNewCommand.Execute(null);

        Assert.False(_profiles.IsNaming);
        Assert.Empty(_requests);
    }
}
