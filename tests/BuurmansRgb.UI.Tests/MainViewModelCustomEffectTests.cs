using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using StreamJsonRpc;

namespace BuurmansRgb.UI.Tests;

// The own effects in the effect choice on Kleur, and "+ Eigen effect maken".
public sealed class MainViewModelCustomEffectTests
{
    private static readonly CustomEffect Disco = new("custom:disco", "Disco", [new CustomEffectStep(Red, 1, 0), new CustomEffectStep(Blue, 1, 0)]);

    private static readonly CustomEffect Avond = new("custom:avond", "Avond", [new CustomEffectStep([Red, Green, Blue], 2, 3)]);

    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task Connect_ListsTheOwnEffectsUnderTheirNamesAfterTheOthers()
    {
        var viewModel = await ConnectAsync(api => api.CustomEffects = [Avond, Disco]);

        Assert.Equal([Strings.Effect_static, Strings.Effect_rainbow, "Avond", "Disco"], viewModel.Effects.Select(effect => effect.DisplayName));
        Assert.Equal([false, false, true, true], viewModel.Effects.Select(effect => effect.IsCustom));
    }

    // The speed counts for an own effect, as it may get more steps any moment.
    [Fact]
    public async Task Connect_LightingWithAnOwnEffect_ChoosesIt()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.CustomEffects = [Disco];
            api.Lighting[""] = api.Lighting[""] with { EffectId = Disco.Id };
        });

        Assert.Equal(Disco.Id, viewModel.SelectedEffect?.Id);
        Assert.True(viewModel.IsSpeedEnabled);
        Assert.Empty(Api.Calls);
    }

    // Two steps, the lamp color and black, under the first free name; then it is the chosen effect.
    [Fact]
    public async Task NewCustomEffect_SavesItAndChoosesIt()
    {
        var viewModel = await ConnectAsync(api => api.CustomEffects = [Disco with { Name = "Eigen effect 1" }]);

        viewModel.NewCustomEffectCommand.Execute(null);

        Assert.Equal(["custom-effect save custom:1 Eigen effect 2 #FF0000/1/1 #000000/1/1", "effect all custom:1"], Api.Calls);
        Assert.Equal("Eigen effect 2", viewModel.SelectedEffect?.DisplayName);
        Assert.Equal(["Eigen effect 1", "Eigen effect 2"], viewModel.Effects.Where(effect => effect.IsCustom).Select(effect => effect.DisplayName));
    }

    [Fact]
    public async Task NewCustomEffect_TheMostThereAre_CannotRun()
    {
        var viewModel = await ConnectAsync(api => api.CustomEffects =
            [.. Enumerable.Range(0, CustomEffects.MaxEffects).Select(i => Disco with { Id = $"custom:{i}", Name = $"Effect {i:00}" })]);

        Assert.False(viewModel.NewCustomEffectCommand.CanExecute(null));
    }

    // Another settings app renamed one and made another; the chosen effect stays chosen.
    [Fact]
    public async Task CustomEffectsChanged_ShowsTheNewList()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.CustomEffects = [Disco];
            api.Lighting[""] = api.Lighting[""] with { EffectId = Disco.Id };
        });

        Api.CustomEffects = [Avond, Disco with { Name = "Feest" }];
        Api.RaiseCustomEffectsChanged();

        Assert.Equal(["Avond", "Feest"], viewModel.Effects.Where(effect => effect.IsCustom).Select(effect => effect.DisplayName));
        Assert.Equal("Feest", viewModel.SelectedEffect?.DisplayName);
        Assert.Empty(Api.Calls);
    }

    // The engine cannot read effects.json yet, as another program holds it: the app connects
    // without own effects, and shows them when the engine tells it has read the file.
    [Fact]
    public async Task Connect_OwnEffectsOutOfReach_ConnectsAndShowsThemLater()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.CustomEffects = [Disco];
            api.GetCustomEffectsException = new RemoteInvocationException("in use", 0, errorData: null);
        });
        Assert.DoesNotContain(viewModel.Effects, effect => effect.IsCustom);

        Api.GetCustomEffectsException = null;
        Api.RaiseCustomEffectsChanged();

        await Eventually.TrueAsync(() => viewModel.Effects.Any(effect => effect.IsCustom));
        Assert.Equal(["Disco"], viewModel.Effects.Where(effect => effect.IsCustom).Select(effect => effect.DisplayName));
        Assert.Empty(Api.Calls);
    }

    // The editor shows the chosen effect, and the picker its first step, at once.
    [Fact]
    public async Task Connect_WithAnOwnEffect_ShowsItInTheEditor()
    {
        var viewModel = await ConnectWithDiscoAsync();

        Assert.True(viewModel.IsCustomEffect);
        Assert.Equal("Disco", viewModel.CustomEffectEditor.Name);
        Assert.Equal(Red, viewModel.ColorPicker.Color);
        Assert.True(viewModel.ColorPicker.AppliesAtOnce);
    }

    // Without "Op de strip zetten": the step changes with the mixed color, and the engine gets it.
    [Fact]
    public async Task MixColor_WithAnOwnEffect_PaintsTheSelectedStep()
    {
        var viewModel = await ConnectWithDiscoAsync();
        viewModel.CustomEffectEditor.SelectStepCommand.Execute(1);

        viewModel.ColorPicker.HexColor = "#FF00FF";

        Assert.Equal(["custom-effect save custom:disco Disco #FF0000/1/0 #FF00FF/1/0"], Api.Calls);
        Assert.Equal(new RgbColor(255, 0, 255), viewModel.CustomEffectEditor.Steps[1].Colors[0]);
    }

    [Fact]
    public async Task SelectStep_ShowsItsColorInThePicker()
    {
        var viewModel = await ConnectWithDiscoAsync();

        viewModel.CustomEffectEditor.SelectStepCommand.Execute(1);

        Assert.Equal(Blue, viewModel.ColorPicker.Color);
        Assert.Empty(Api.Calls);
    }

    // A lamp that is off goes on with a color for a step, as with any other color.
    [Fact]
    public async Task MixColor_LampOff_SwitchesItOnAfterTheStep()
    {
        var viewModel = await ConnectWithDiscoAsync(api => api.Lighting["fan"] = api.Lighting["fan"] with { EffectId = Disco.Id, On = false });
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        Api.Calls.Clear();

        viewModel.ColorPicker.HexColor = "#00FF00";

        Assert.Equal(["custom-effect save custom:disco Disco #00FF00/1/0 #0000FF/1/0", "on fan on"], Api.Calls);
    }

    // A new name shows in the effect choice at once.
    [Fact]
    public async Task Rename_ShowsInTheEffectChoiceAndGoesToTheEngine()
    {
        var viewModel = await ConnectWithDiscoAsync();

        viewModel.CustomEffectEditor.Name = "Feest";

        Assert.Equal("Feest", viewModel.SelectedEffect?.DisplayName);
        Assert.Equal(["custom-effect save custom:disco Feest #FF0000/1/0 #0000FF/1/0"], Api.Calls);
    }

    [Fact]
    public async Task Rename_ToTheNameOfAnotherEffect_IsRefused()
    {
        var viewModel = await ConnectWithDiscoAsync(api => api.CustomEffects = [Avond, Disco]);

        viewModel.CustomEffectEditor.Name = "avond";

        Assert.Equal(UiText.Format(Strings.CustomEffectNameTaken, "avond"), viewModel.CustomEffectEditor.NameError);
        Assert.Empty(Api.Calls);
    }

    // The engine gives the device static; the choice here goes there at once.
    [Fact]
    public async Task Delete_ChoosesStaticAndDeletesItInTheEngine()
    {
        var viewModel = await ConnectWithDiscoAsync();

        viewModel.CustomEffectEditor.DeleteCommand.Execute(null);

        Assert.Equal(["custom-effect delete custom:disco"], Api.Calls);
        Assert.Equal(EffectIds.Static, viewModel.SelectedEffect?.Id);
        Assert.False(viewModel.IsCustomEffect);
        Assert.DoesNotContain(viewModel.Effects, effect => effect.IsCustom);
        Assert.Equal(UiText.Format(Strings.CustomEffectDeleted, "Disco"), viewModel.StatusMessage);
    }

    // The colors the per-LED effect gives the LEDs of the lamp become the pattern of the step.
    [Fact]
    public async Task TakeLedPattern_MakesTheStepAPatternOfTheLeds()
    {
        var viewModel = await ConnectWithDiscoAsync(api => api.LedColors["strip"] = [Red, Green, Blue]);
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "strip");
        Api.Calls.Clear();

        await viewModel.TakeLedPatternCommand.ExecuteAsync(null);

        Assert.Equal(["custom-effect save custom:disco Disco 3leds/1/0 #0000FF/1/0"], Api.Calls);
        Assert.Equal(Red, viewModel.ColorPicker.Color);
    }

    [Fact]
    public async Task TakeLedPattern_ForTheWholeHouse_CannotRun()
    {
        var viewModel = await ConnectWithDiscoAsync();

        Assert.False(viewModel.TakeLedPatternCommand.CanExecute(null));
    }

    // Dragging the slider sends the newest time only once the engine is free again.
    [Fact]
    public async Task HoldSeconds_GoesToTheEngine()
    {
        var viewModel = await ConnectWithDiscoAsync();

        viewModel.CustomEffectEditor.HoldSeconds = 2.5;

        Assert.Equal(["custom-effect save custom:disco Disco #FF0000/2.5/0 #0000FF/1/0"], Api.Calls);
    }

    // A list the engine sends while a change of this app is on its way does not bring back the old one.
    [Fact]
    public async Task CustomEffectsChanged_WhileAChangeWaits_KeepsTheChange()
    {
        var viewModel = await ConnectWithDiscoAsync();
        var gate = new TaskCompletionSource();
        Api.SetGate = gate.Task;

        viewModel.CustomEffectEditor.Name = "Feest";
        Api.RaiseCustomEffectsChanged();
        gate.SetResult();
        await Eventually.TrueAsync(() => Api.Calls.Count == 1);

        Assert.Equal("Feest", viewModel.SelectedEffect?.DisplayName);
        Assert.Equal("Feest", viewModel.CustomEffectEditor.Name);
    }

    // The engine refused the new name, which another settings app took meanwhile: the app shows the
    // effects as the engine keeps them, also though a list it sent was dropped for the change.
    [Fact]
    public async Task Rename_RefusedByTheEngine_ShowsTheEffectsOfTheEngine()
    {
        var viewModel = await ConnectWithDiscoAsync();
        Api.CustomEffects = [Disco, Avond with { Name = "Feest" }];
        Api.SetException = new RemoteInvocationException("There is already an own effect named 'Feest'.", 0, errorData: null);
        var gate = new TaskCompletionSource();
        Api.SetGate = gate.Task;

        viewModel.CustomEffectEditor.Name = "Feest";
        Api.RaiseCustomEffectsChanged();
        gate.SetResult();

        await Eventually.TrueAsync(() => viewModel.CustomEffectEditor.Name == "Disco");
        Assert.Equal(["Disco", "Feest"], viewModel.Effects.Where(effect => effect.IsCustom).Select(effect => effect.DisplayName));
        Assert.Equal("Disco", viewModel.SelectedEffect?.DisplayName);
    }

    // Another settings app made the last one there can be meanwhile.
    [Fact]
    public async Task NewCustomEffect_RefusedByTheEngine_ShowsTheEffectsOfTheEngine()
    {
        var viewModel = await ConnectWithDiscoAsync();
        Api.CustomEffects = [Avond, Disco];
        Api.SetException = new RemoteInvocationException("There are at most 32 own effects.", 0, errorData: null);

        viewModel.NewCustomEffectCommand.Execute(null);

        await Eventually.TrueAsync(() => viewModel.Effects.Count(effect => effect.IsCustom) == 2);
        Assert.Equal(["Avond", "Disco"], viewModel.Effects.Where(effect => effect.IsCustom).Select(effect => effect.DisplayName));
        Assert.Equal(Strings.ChangeRejected, viewModel.StatusMessage);
    }

    // The effects of the engine cannot be fetched either: the refusal is still what the app tells.
    [Fact]
    public async Task NewCustomEffect_RefusedAndReloadFails_TellsTheRefusal()
    {
        var viewModel = await ConnectWithDiscoAsync();
        Api.SetException = new RemoteInvocationException("There are at most 32 own effects.", 0, errorData: null);
        Api.GetCustomEffectsException = new IOException("broken pipe");

        viewModel.NewCustomEffectCommand.Execute(null);

        await Eventually.TrueAsync(() => viewModel.StatusMessage == Strings.ChangeRejected);
        Assert.Equal(["Disco"], viewModel.Effects.Where(effect => effect.IsCustom).Select(effect => effect.DisplayName));
    }

    // Another settings app deleted it already, and made another.
    [Fact]
    public async Task Delete_RefusedByTheEngine_ShowsTheEffectsOfTheEngine()
    {
        var viewModel = await ConnectWithDiscoAsync();
        Api.CustomEffects = [Avond];
        Api.SetException = new RemoteInvocationException("There is no own effect 'custom:disco'.", 0, errorData: null);

        viewModel.CustomEffectEditor.DeleteCommand.Execute(null);

        await Eventually.TrueAsync(() => viewModel.Effects.Any(effect => effect.IsCustom));
        Assert.Equal(["Avond"], viewModel.Effects.Where(effect => effect.IsCustom).Select(effect => effect.DisplayName));
        Assert.Equal(Strings.ChangeRejected, viewModel.StatusMessage);
    }

    [Fact]
    public async Task Disconnect_StopsFollowingTheOwnEffects()
    {
        var viewModel = await ConnectAsync();
        var api = Api;
        Assert.True(api.HasCustomEffectsSubscribers);

        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.False(api.HasCustomEffectsSubscribers);
    }

    // "Hele huis" shows Disco.
    private Task<MainViewModel> ConnectWithDiscoAsync(Action<FakeEngineApi>? arrange = null) => ConnectAsync(api =>
    {
        api.CustomEffects = [Disco];
        api.Lighting[""] = api.Lighting[""] with { EffectId = Disco.Id };
        arrange?.Invoke(api);
    });

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api =>
        {
            api.Effects = [EffectIds.Static, EffectIds.Rainbow];
            arrange?.Invoke(api);
        }));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
