using BuurmansRgb.Contracts;
using BuurmansRgb.UI.ViewModels;
using StreamJsonRpc;

namespace BuurmansRgb.UI.Tests;

public sealed class MainViewModelProfileTests
{
    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task Connect_ShowsTheProfilesOfTheEngine()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.AddRange(["Gamen", "Nacht"]));

        Assert.Equal(["Gamen", "Nacht"], viewModel.Profiles.Names);
        Assert.Null(viewModel.Profiles.SelectedName);
    }

    [Fact]
    public async Task Save_SendsAfterTheWaitingChangesAndSelectsTheNewProfile()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.Add("Werk"));
        viewModel.Brightness = 40;
        viewModel.Profiles.Name = "Gamen";

        viewModel.Profiles.SaveCommand.Execute(null);

        Assert.Equal(["brightness all 0.40", "profile save Gamen"], Api.Calls);
        Assert.Equal(["Gamen", "Werk"], viewModel.Profiles.Names);
        Assert.Equal("Gamen", viewModel.Profiles.SelectedName);
        Assert.Equal("Scène 'Gamen' in elkaar gezet.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Load_ShowsTheLightingOfTheProfile()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.Add("Nacht");
            api.ProfileLighting["Nacht"] = new LightingInfo(Blue, 0.1, EffectIds.Rainbow, 0.5);
        });
        viewModel.Profiles.SelectedName = "Nacht";

        viewModel.Profiles.LoadCommand.Execute(null);

        Assert.Equal(["profile load Nacht"], Api.Calls);
        Assert.Equal(10, viewModel.Brightness);
        Assert.Equal(EffectIds.Rainbow, viewModel.SelectedEffect?.Id);
        Assert.Equal(50, viewModel.Speed);
        Assert.Equal("Scène 'Nacht' staat aan.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Delete_RemovesTheProfileFromTheList()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.AddRange(["Gamen", "Nacht"]));
        viewModel.Profiles.SelectedName = "Gamen";

        viewModel.Profiles.DeleteCommand.Execute(null);

        Assert.Equal(["profile delete Gamen"], Api.Calls);
        Assert.Equal(["Nacht"], viewModel.Profiles.Names);
        Assert.Equal("Scène 'Gamen' verwijderd.", viewModel.StatusMessage);
    }

    // Deleted elsewhere since the list showed it, such as in a second settings app.
    [Fact]
    public async Task Delete_SceneThatIsGone_SaysSoAndShowsTheListOfTheEngine()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.AddRange(["Gamen", "Nacht"]));
        viewModel.Profiles.SelectedName = "Gamen";
        Api.Profiles.Remove("Gamen");
        Api.SetException = new RemoteInvocationException("There is no profile 'Gamen'.", 0, errorData: null);

        viewModel.Profiles.DeleteCommand.Execute(null);

        Assert.Equal(["Nacht"], viewModel.Profiles.Names);
        Assert.Equal("Scène 'Gamen' bestaat niet meer.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ChangeRejected_GoesAfterTheNextChangeThatWorks()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.Add("Werk"));
        Api.SetException = new RemoteInvocationException("Unknown device.", 0, errorData: null);
        viewModel.Brightness = 10;
        Assert.Equal("De engine kon de wijziging niet verwerken.", viewModel.StatusMessage);

        Api.SetException = null;
        viewModel.Brightness = 20;

        Assert.Null(viewModel.StatusMessage);
    }

    [Fact]
    public async Task Save_RejectedByTheEngine_ShowsTheErrorAndKeepsTheList()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.Add("Werk"));
        Api.SetException = new RemoteInvocationException("Disk full", 0, errorData: null);
        viewModel.Profiles.Name = "Gamen";

        viewModel.Profiles.SaveCommand.Execute(null);

        Assert.Equal(["Werk"], viewModel.Profiles.Names);
        Assert.Equal("De engine kon de wijziging niet verwerken.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task SelectProfile_ShowsWhenItLoadsByItself()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.AddRange(["Gamen", "Nacht"]);
            api.ProfileAutomations["Nacht"] = new ProfileAutomation(new TimeOnly(23, 0), "eldenring.exe");
        });

        viewModel.Profiles.SelectedName = "Nacht";

        Assert.Equal("Nacht", viewModel.Profiles.Automation.ProfileName);
        Assert.Equal("23:00", viewModel.Profiles.Automation.LoadAtText);
        Assert.Equal("eldenring.exe", viewModel.Profiles.Automation.ProgramText);
    }

    [Fact]
    public async Task ApplyAutomation_SendsItAndShowsWhatTheEngineSaved()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.Add("Gamen"));
        viewModel.Profiles.SelectedName = "Gamen";

        viewModel.Profiles.Automation.ProgramText = "eldenring";
        viewModel.Profiles.Automation.ApplyCommand.Execute(null);

        Assert.Equal(["profile automation Gamen - eldenring.exe"], Api.Calls);
        Assert.Equal("eldenring.exe", viewModel.Profiles.Automation.ProgramText);
        Assert.Equal("Vanzelf aanzetten: aan", viewModel.Profiles.Automation.Header);
        Assert.False(viewModel.Profiles.Automation.ApplyCommand.CanExecute(null));
        Assert.Equal("Vanzelf aanzetten van 'Gamen' opgeslagen.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task DeleteProfile_EmptiesItsAutomation()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.Add("Nacht");
            api.ProfileAutomations["Nacht"] = new ProfileAutomation(new TimeOnly(23, 0));
        });
        viewModel.Profiles.SelectedName = "Nacht";

        viewModel.Profiles.DeleteCommand.Execute(null);

        Assert.False(viewModel.Profiles.Automation.HasProfile);
        Assert.Equal("", viewModel.Profiles.Automation.LoadAtText);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi> arrange)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api =>
        {
            api.Effects = [EffectIds.Static, EffectIds.Rainbow, EffectIds.PerLed];
            arrange(api);
        }));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
