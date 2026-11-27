using BuurmansRgb.Contracts;
using BuurmansRgb.Contracts.Testing;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using StreamJsonRpc;

namespace BuurmansRgb.UI.Tests;

// The scenes on Scènes and the times of "Alles uit", without MainViewModel: a session on a fake
// engine, and the view models that the coordinator shows the scenes in.
public sealed class SceneCoordinatorTests
{
    private readonly FakeEngineConnection _connection = new();
    private readonly ProfilesViewModel _profiles = new();
    private readonly WholeHouseTimesViewModel _wholeHouseTimes = new();
    private readonly SceneCoordinator _scenes;
    private readonly List<string> _messages = [];
    private int _messagesWhenLoaded = -1;

    public SceneCoordinatorTests()
    {
        var session = new EngineSession(new UiThread(), _ => { }, _ => { });
        _scenes = new SceneCoordinator(session, _profiles, _wholeHouseTimes);
        _scenes.Message += (_, message) => _messages.Add(message);
        _scenes.Loaded += (_, _) => _messagesWhenLoaded = _messages.Count;
        session.Attach(_connection);
        Api.Profiles.AddRange(["Gamen", "Nacht"]);
    }

    private FakeEngineApi Api => _connection.FakeApi;

    [Fact]
    public async Task LoadAsync_ShowsTheScenesTheirCardsAndTheTimes()
    {
        Api.ActiveProfile = "Nacht";
        Api.WholeHouseTimes = new WholeHouseTimes(OffScene: "Nacht");

        await _scenes.LoadAsync(Api);

        Assert.Equal(["Gamen", "Nacht"], _profiles.Names);
        Assert.Equal(["Nacht"], _profiles.Cards.Where(card => card.IsActive).Select(card => card.Name));
        Assert.Equal("Nacht", _wholeHouseTimes.OffScene);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task Save_SelectsTheNewSceneAndSaysSo()
    {
        await _scenes.LoadAsync(Api);
        _profiles.Name = "Werk";

        _profiles.SaveCommand.Execute(null);

        Assert.Equal(["profile save Werk"], Api.Calls);
        Assert.Equal(["Gamen", "Nacht", "Werk"], _profiles.Names);
        Assert.Equal("Werk", _profiles.SelectedName);
        Assert.Equal([UiText.Format(Strings.ProfileSaved, "Werk")], _messages);
        Assert.Equal(-1, _messagesWhenLoaded);
    }

    // MainViewModel shows the lighting of the scene once the list and the message show it.
    [Fact]
    public async Task Load_SaysLoadedAfterTheMessage()
    {
        await _scenes.LoadAsync(Api);
        _profiles.SelectedName = "Nacht";

        _profiles.LoadCommand.Execute(null);

        Assert.Equal(["profile load Nacht"], Api.Calls);
        Assert.Equal([UiText.Format(Strings.ProfileLoaded, "Nacht")], _messages);
        Assert.Equal(1, _messagesWhenLoaded);
    }

    // Deleted elsewhere since the list showed it, such as in a second settings app.
    [Fact]
    public async Task Delete_SceneThatIsGone_SaysSoAndShowsTheListOfTheEngine()
    {
        await _scenes.LoadAsync(Api);
        _profiles.SelectedName = "Gamen";
        Api.Profiles.Remove("Gamen");
        Api.SetException = new RemoteInvocationException("There is no profile 'Gamen'.", 0, errorData: null);

        _profiles.DeleteCommand.Execute(null);

        Assert.Equal(["Nacht"], _profiles.Names);
        Assert.Equal([UiText.Format(Strings.ProfileGone, "Gamen")], _messages);
    }

    // Another settings app saved a scene: the list follows and the selection stays.
    [Fact]
    public async Task Reload_FollowsTheScenesOfTheEngine()
    {
        await _scenes.LoadAsync(Api);
        _profiles.SelectedName = "Nacht";
        Api.Profiles.Add("Werk");

        _scenes.Reload();

        Assert.Equal(["Gamen", "Nacht", "Werk"], _profiles.Names);
        Assert.Equal("Nacht", _profiles.SelectedName);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task ShowActiveAsync_ShowsTheSceneThatIsOnNow()
    {
        await _scenes.LoadAsync(Api);
        Api.ActiveProfile = "Gamen";

        await _scenes.ShowActiveAsync();

        Assert.Equal(["Gamen"], _profiles.Cards.Where(card => card.IsActive).Select(card => card.Name));
    }

    // An imported scene is chosen, so its block "… aanpassen" shows the devices it got.
    [Fact]
    public async Task ShowImported_ChoosesTheImportedScene()
    {
        await _scenes.LoadAsync(Api);
        Api.Profiles.Add("Werk");

        _scenes.ShowImported(new ShareImportResult(ShareImportOutcome.Imported, SharedKind.Scene, "Werk"));

        Assert.Equal(["Gamen", "Nacht", "Werk"], _profiles.Names);
        Assert.Equal("Werk", _profiles.SelectedName);
    }
}
