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
        Assert.Equal("Profiel 'Gamen' opgeslagen.", viewModel.StatusMessage);
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
        Assert.Equal("Profiel 'Nacht' geladen.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Delete_RemovesTheProfileFromTheList()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.AddRange(["Gamen", "Nacht"]));
        viewModel.Profiles.SelectedName = "Gamen";

        viewModel.Profiles.DeleteCommand.Execute(null);

        Assert.Equal(["profile delete Gamen"], Api.Calls);
        Assert.Equal(["Nacht"], viewModel.Profiles.Names);
        Assert.Equal("Profiel 'Gamen' verwijderd.", viewModel.StatusMessage);
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
