using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

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

    [Fact]
    public async Task Disconnect_StopsFollowingTheOwnEffects()
    {
        var viewModel = await ConnectAsync();
        var api = Api;
        Assert.True(api.HasCustomEffectsSubscribers);

        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.False(api.HasCustomEffectsSubscribers);
    }

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
