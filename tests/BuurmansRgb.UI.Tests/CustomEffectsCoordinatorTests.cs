using BuurmansRgb.Contracts;
using BuurmansRgb.Contracts.Testing;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using StreamJsonRpc;

namespace BuurmansRgb.UI.Tests;

// The own effects and the effects of the engine for the effect choice on Kleur, without
// MainViewModel: a session on a fake engine, and the editor the coordinator makes.
public sealed class CustomEffectsCoordinatorTests
{
    private static readonly CustomEffect Disco = TestEffects.Disco;

    private static readonly CustomEffect Avond = new("custom:avond", "Avond", [new CustomEffectStep([Red, Green, Blue], 2, 3)]);

    private readonly FakeEngineConnection _connection = new();
    private readonly CustomEffectsCoordinator _effects;
    private readonly List<string> _messages = [];
    private readonly List<string> _deleted = [];
    private readonly List<string> _created = [];
    private EffectOption? _selected;
    private int _effectsChanged;

    public CustomEffectsCoordinatorTests()
    {
        var session = new EngineSession(new UiThread(), _ => { }, _ => { });
        _effects = new CustomEffectsCoordinator(session, () => _selected);
        _effects.Message += (_, message) => _messages.Add(message);
        _effects.Deleted += (_, id) => _deleted.Add(id);
        _effects.Created += (_, id) => _created.Add(id);
        _effects.EffectsChanged += (_, _) => _effectsChanged++;
        session.Attach(_connection);
        Api.Effects = [EffectIds.Static, EffectIds.Rainbow];
        Api.CustomEffects = [Avond, Disco];
    }

    private FakeEngineApi Api => _connection.FakeApi;

    private IEnumerable<string> OwnNames => _effects.Options.Where(option => option.IsCustom).Select(option => option.DisplayName);

    [Fact]
    public async Task LoadAsync_GivesTheEffectsOfTheEngineAndThenTheOwnOnes()
    {
        await _effects.LoadAsync(Api);

        Assert.Equal([Strings.Effect_static, Strings.Effect_rainbow, "Avond", "Disco"], _effects.Options.Select(option => option.DisplayName));
        Assert.Equal([0, 0, 1, 2], _effects.Options.Select(option => option.Steps));
        Assert.Equal(1, _effectsChanged);
        Assert.Empty(Api.Calls);
    }

    // The engine cannot read effects.json yet: the app connects without own effects.
    [Fact]
    public async Task LoadAsync_OwnEffectsOutOfReach_GivesTheEffectsOfTheEngineOnly()
    {
        Api.GetCustomEffectsException = new RemoteInvocationException("in use", 0, errorData: null);

        await _effects.LoadAsync(Api);

        Assert.Equal([EffectIds.Static, EffectIds.Rainbow], _effects.Options.Select(option => option.Id));
    }

    // True only the first time, so MainViewModel lets the picker follow the step of another effect.
    [Fact]
    public async Task ShowInEditor_OwnEffect_ShowsItAndTellsWhenItIsAnother()
    {
        await _effects.LoadAsync(Api);

        Assert.True(_effects.ShowInEditor(EffectOption.For(Disco)));
        Assert.False(_effects.ShowInEditor(EffectOption.For(Disco)));
        Assert.Equal("Disco", _effects.Editor.Name);
        Assert.False(_effects.ShowInEditor(EffectOption.Create(EffectIds.Rainbow)));
        Assert.Equal("Disco", _effects.Editor.Name);
    }

    [Fact]
    public async Task Rename_SendsTheEffectAndShowsTheNewName()
    {
        await ShowDiscoAsync();

        _effects.Editor.Name = "Feest";

        Assert.Equal(["custom-effect save custom:disco Feest #FF0000/1/0 #0000FF/1/0"], Api.Calls);
        Assert.Equal(["Avond", "Feest"], OwnNames);
        Assert.Equal(2, _effectsChanged);
    }

    // Another own effect, or an effect of the engine, already has the name in the effect choice.
    [Theory]
    [InlineData("custom:disco", "avond", true)]
    [InlineData("custom:disco", "REGENBOOG", true)]
    [InlineData("custom:disco", "Disco", false)]
    [InlineData("custom:disco", "Feest", false)]
    [InlineData("", "Disco", true)]
    public async Task IsNameTaken_TellsWhetherTheEffectChoiceHasTheName(string id, string name, bool taken)
    {
        await _effects.LoadAsync(Api);

        Assert.Equal(taken, _effects.IsNameTaken(id, name));
    }

    // MainViewModel then chooses static, as the engine gives the device.
    [Fact]
    public async Task Delete_SaysDeletedAndDeletesItInTheEngine()
    {
        await ShowDiscoAsync();

        _effects.Editor.DeleteCommand.Execute(null);

        Assert.Equal(["custom-effect delete custom:disco"], Api.Calls);
        Assert.Equal(["custom:disco"], _deleted);
        Assert.Equal(["Avond"], OwnNames);
        Assert.Equal([UiText.Format(Strings.CustomEffectDeleted, "Disco")], _messages);
    }

    // Two steps, the color and black, under the first free name; then Created names it.
    [Fact]
    public async Task Create_SavesItUnderTheFirstFreeNameAndSaysCreated()
    {
        Api.CustomEffects = [Disco with { Name = "Eigen effect 1" }];
        await _effects.LoadAsync(Api);

        _effects.Create(Red);

        Assert.Equal(["custom-effect save custom:1 Eigen effect 2 #FF0000/1/1 #000000/1/1"], Api.Calls);
        Assert.Equal(["custom:1"], _created);
        Assert.Equal(["Eigen effect 1", "Eigen effect 2"], OwnNames);
    }

    [Fact]
    public async Task CanCreate_TheMostThereAre_IsFalse()
    {
        Api.CustomEffects = [.. Enumerable.Range(0, CustomEffects.MaxEffects).Select(i => Disco with { Id = $"custom:{i}", Name = $"Effect {i:00}" })];
        await _effects.LoadAsync(Api);

        Assert.False(_effects.CanCreate);
    }

    // Another settings app renamed one and made another.
    [Fact]
    public async Task Reload_ShowsTheEffectsOfTheEngine()
    {
        await _effects.LoadAsync(Api);
        Api.CustomEffects = [Avond, Disco with { Name = "Feest" }];

        _effects.Reload();

        Assert.Equal(["Avond", "Feest"], OwnNames);
        Assert.Equal(2, _effectsChanged);
        Assert.Empty(Api.Calls);
    }

    // A list the engine sends while a change of this app is on its way does not bring back the old one.
    [Fact]
    public async Task Reload_WhileAChangeWaits_KeepsTheChange()
    {
        await ShowDiscoAsync();
        var gate = new TaskCompletionSource();
        Api.SetGate = gate.Task;

        _effects.Editor.Name = "Feest";
        _effects.Reload();
        gate.SetResult();
        await Eventually.TrueAsync(() => Api.Calls.Count == 1);

        Assert.Equal(["Avond", "Feest"], OwnNames);
    }

    // The engine refused the new name, which another settings app took meanwhile: the effects as
    // the engine keeps them come back.
    [Fact]
    public async Task Rename_RefusedByTheEngine_ShowsTheEffectsOfTheEngine()
    {
        await ShowDiscoAsync();
        Api.CustomEffects = [Disco, Avond with { Name = "Feest" }];
        Api.SetException = new RemoteInvocationException("There is already an own effect named 'Feest'.", 0, errorData: null);

        _effects.Editor.Name = "Feest";

        await Eventually.TrueAsync(() => OwnNames.SequenceEqual(["Disco", "Feest"]));
        Assert.Equal([Disco.Id, "custom:avond"], _effects.Options.Where(option => option.IsCustom).Select(option => option.Id));
    }

    // The colors the per-LED effect gives the LEDs of the device become the pattern of the step.
    [Fact]
    public async Task TakeLedPatternAsync_MakesTheStepAPatternOfTheLeds()
    {
        await ShowDiscoAsync();
        Api.LedColors["strip"] = [Red, Green, Blue];

        await _effects.TakeLedPatternAsync("strip");

        Assert.Equal(["custom-effect save custom:disco Disco 3leds/1/0 #0000FF/1/0"], Api.Calls);
    }

    // Another effect was chosen meanwhile: the pattern goes nowhere.
    [Fact]
    public async Task TakeLedPatternAsync_NoOwnEffectChosen_ChangesNothing()
    {
        await ShowDiscoAsync();
        Api.LedColors["strip"] = [Red, Green, Blue];
        _selected = EffectOption.Create(EffectIds.Static);

        await _effects.TakeLedPatternAsync("strip");

        Assert.Empty(Api.Calls);
    }

    private async Task ShowDiscoAsync()
    {
        await _effects.LoadAsync(Api);
        _selected = EffectOption.For(Disco);
        _effects.ShowInEditor(_selected);
    }
}
