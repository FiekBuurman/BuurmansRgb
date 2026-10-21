using BuurmansRgb.Contracts;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class MainViewModelSceneTests
{
    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task Connect_ShowsWhatEverySceneLooksLikeAndWhenItGoesOn()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.AddRange(["Filmavond", "Nacht"]);
            api.ProfileLighting["Filmavond"] = new LightingInfo(Orange, 0.4, EffectIds.Breathing);
            api.ProfileAutomations["Nacht"] = new ProfileAutomation(new TimeOnly(23, 0));
        });

        Assert.Equal(["Ademen · 40%", "Elke dag om 23:00"], viewModel.Profiles.Cards.Select(card => card.Description));
        Assert.All(viewModel.Profiles.Cards[0].Dots, dot => Assert.Equal(Orange, dot.Color));
    }

    [Fact]
    public async Task Connect_SceneThatIsGone_IsSkippedWithoutAMessage()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.AddRange(["Filmavond", "Nacht"]);
            api.GoneProfiles.Add("Filmavond");
            api.ProfileAutomations["Nacht"] = new ProfileAutomation(new TimeOnly(23, 0));
        });

        Assert.Equal(["", "Elke dag om 23:00"], viewModel.Profiles.Cards.Select(card => card.Description));
        Assert.Null(viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoadScene_PutsItOnAndShowsTheLightingOfIt()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.Add("Nacht");
            api.ProfileLighting["Nacht"] = new LightingInfo(Blue, 0.1, EffectIds.Rainbow, 0.5);
        });

        viewModel.Profiles.LoadSceneCommand.Execute(viewModel.Profiles.Cards[0]);

        Assert.Equal(["profile load Nacht"], Api.Calls);
        Assert.Equal(10, viewModel.Brightness);
        Assert.Equal("Nacht", viewModel.Profiles.SelectedName);
    }

    [Fact]
    public async Task Connect_TheSceneThatIsOnIsActive()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.AddRange(["Filmavond", "Nacht"]);
            api.ActiveProfile = "Nacht";
        });

        Assert.Equal([false, true], viewModel.Profiles.Cards.Select(card => card.IsActive));
    }

    [Fact]
    public async Task LoadScene_MakesItsCardActive()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.AddRange(["Filmavond", "Nacht"]));

        viewModel.Profiles.LoadSceneCommand.Execute(viewModel.Profiles.Cards[0]);

        Assert.Equal([true, false], viewModel.Profiles.Cards.Select(card => card.IsActive));
    }

    // A scene from the tray menu, or any other change: the engine reports LightingChanged.
    [Fact]
    public async Task LightingChanged_FollowsTheSceneThatIsOn()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.AddRange(["Filmavond", "Nacht"]));

        Api.ActiveProfile = "Filmavond";
        Api.RaiseLightingChanged();
        Assert.Equal([true, false], viewModel.Profiles.Cards.Select(card => card.IsActive));

        Api.ActiveProfile = null;
        Api.RaiseLightingChanged();
        Assert.All(viewModel.Profiles.Cards, card => Assert.False(card.IsActive));
    }

    [Fact]
    public async Task DeleteTheActiveScene_NoCardIsActive()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.AddRange(["Filmavond", "Nacht"]);
            api.ActiveProfile = "Nacht";
        });
        viewModel.Profiles.SelectedName = "Nacht";

        viewModel.Profiles.DeleteCommand.Execute(null);

        Assert.Null(Api.ActiveProfile);
        Assert.All(viewModel.Profiles.Cards, card => Assert.False(card.IsActive));
    }

    [Fact]
    public async Task NewScene_SavedGetsACardThatShowsTheLighting()
    {
        var viewModel = await ConnectAsync(_ => { });
        viewModel.Profiles.StartNewCommand.Execute(null);
        viewModel.Profiles.Name = "Klusmodus";

        viewModel.Profiles.SaveCommand.Execute(null);

        var card = Assert.Single(viewModel.Profiles.Cards);
        Assert.Equal("Klusmodus", card.Name);
        Assert.Equal("Statisch · 100%", card.Description);
    }

    [Fact]
    public async Task Automation_Applied_ShowsOnTheCard()
    {
        var viewModel = await ConnectAsync(api => api.Profiles.Add("Gamen"));
        viewModel.Profiles.EditCommand.Execute(viewModel.Profiles.Cards[0]);
        viewModel.Profiles.Automation.ProgramText = "eldenring.exe";

        viewModel.Profiles.Automation.ApplyCommand.Execute(null);

        Assert.Equal("Bij eldenring.exe", viewModel.Profiles.Cards[0].Description);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi> arrange)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, arrange));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
