using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// "Uit om" and "Aan om" on Apparaten go to the engine and follow what it saved, also a change of the
// times of "Alles uit" on Scènes: one place where they are kept.
public sealed class MainViewModelWholeHouseTimesTests
{
    private static readonly LoadTime ElevenPm = new(new TimeOnly(23, 0));

    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task Connect_ShowsTheTimesOfTheEngine()
    {
        var viewModel = await ConnectAsync(api => api.WholeHouseTimes = new WholeHouseTimes([ElevenPm], OffScene: "Alles uit"));

        Assert.True(viewModel.WholeHouseTimes.IsAvailable);
        Assert.True(viewModel.WholeHouseTimes.Off.IsOn);
        Assert.False(viewModel.WholeHouseTimes.On.IsOn);
        Assert.Equal("Alles uit", viewModel.WholeHouseTimes.OffScene);
    }

    [Fact]
    public async Task SwitchOn_SendsIt_AndSaysItIsSaved()
    {
        var viewModel = await ConnectAsync(_ => { });

        viewModel.WholeHouseTimes.On.IsOn = true;

        Assert.Equal(["whole house off - on 07:00"], Api.Calls);
        Assert.Equal(Strings.WholeHouseTimesSaved, viewModel.StatusMessage);
        Assert.True(viewModel.WholeHouseTimes.On.IsOn);
    }

    // The times of "Alles uit" changed on Scènes, or in another settings app: Apparaten follows.
    [Fact]
    public async Task ProfilesChanged_ShowsTheNewTimes()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.Add("Alles uit");
            api.WholeHouseTimes = new WholeHouseTimes(OffScene: "Alles uit");
        });

        Api.WholeHouseTimes = new WholeHouseTimes([new LoadTime(new TimeOnly(22, 0))], OffScene: "Alles uit");
        Api.RaiseProfilesChanged();

        Assert.True(viewModel.WholeHouseTimes.Off.IsOn);
        Assert.Equal("22:00", viewModel.WholeHouseTimes.Off.Text);
    }

    // And the other way round: "Alles uit" open on Scènes shows the time set on Apparaten.
    [Fact]
    public async Task UitOm_ChangesTheTimesOfAllesUitOpenOnScenes()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Profiles.Add("Alles uit");
            api.WholeHouseTimes = new WholeHouseTimes(OffScene: "Alles uit");
        });
        viewModel.Profiles.EditCommand.Execute(viewModel.Profiles.Cards[0]);
        Assert.Equal("Alles uit", viewModel.Profiles.Automation.ProfileName);

        // As the engine does: the times of Uit om are those of the scene.
        Api.ProfileAutomations["Alles uit"] = new ProfileAutomation([ElevenPm]);
        viewModel.WholeHouseTimes.Off.IsOn = true;

        Assert.Equal("23:00", Assert.Single(viewModel.Profiles.Automation.Times).Text);
    }

    [Fact]
    public async Task Disconnected_TheTimesAreGrey()
    {
        var viewModel = await ConnectAsync(_ => { });

        _connector.Connection.Disconnect();

        Assert.False(viewModel.WholeHouseTimes.IsAvailable);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi> arrange)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, arrange));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
