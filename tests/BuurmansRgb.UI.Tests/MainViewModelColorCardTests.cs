using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// Kleur in the order of the effect (DESIGN.md § 9.2): the effect tiles choose the effect, and card 4
// shows what belongs to it, or with chosen LEDs the color for those LEDs.
public sealed class MainViewModelColorCardTests
{
    private static readonly CustomEffect Disco = new("custom:disco", "Disco", [new CustomEffectStep(Red, 1, 0), new CustomEffectStep(Blue, 1, 0)]);

    private static readonly CustomEffect Avond = new("custom:avond", "Avond", [new CustomEffectStep(Green, 1, 0)]);

    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    // Per effect the heading of card 4, and whether it has the mixing panel (besluit 2), which only
    // Regenboog and "Jouw effect" without a chosen own effect leave out.
    [Theory]
    [InlineData(EffectIds.Static, ColorCardContent.Color, true)]
    [InlineData(EffectIds.Breathing, ColorCardContent.Color, true)]
    [InlineData(EffectIds.Wave, ColorCardContent.Color, true)]
    [InlineData(EffectIds.Rainbow, ColorCardContent.NoColor, false)]
    [InlineData(EffectIds.ColorLoop, ColorCardContent.ColorLoop, true)]
    [InlineData(EffectIds.Twinkle, ColorCardContent.EffectColors, true)]
    [InlineData(EffectIds.Comet, ColorCardContent.EffectColors, true)]
    [InlineData(EffectIds.Scanner, ColorCardContent.EffectColors, true)]
    [InlineData(EffectIds.Fire, ColorCardContent.EffectColors, true)]
    [InlineData(EffectIds.PerLed, ColorCardContent.PerLed, true)]
    [InlineData("custom:disco", ColorCardContent.CustomEffect, true)]
    public async Task ColorCard_FollowsTheEffect(string effectId, ColorCardContent content, bool mixer)
    {
        var viewModel = await ConnectAsync(effectId);

        Assert.Equal(content, viewModel.ColorCard);
        Assert.Equal(mixer, viewModel.ShowsMixer);
        Assert.Equal(
            content switch
            {
                ColorCardContent.Color => Strings.ColorHeader,
                ColorCardContent.NoColor => Strings.NoColorHeader,
                ColorCardContent.ColorLoop => Strings.ColorLoopHeader,
                ColorCardContent.EffectColors => Strings.EffectColorsHeader,
                ColorCardContent.PerLed => Strings.LedColorHeader,
                _ => Strings.CustomEffectCardHeader,
            },
            viewModel.ColorCardHeader);
    }

    [Fact]
    public async Task NoColorText_NamesTheEffect()
    {
        var viewModel = await ConnectAsync(EffectIds.Rainbow);

        Assert.Equal("Regenboog maakt zijn eigen kleuren. Kies hieronder de helderheid, de snelheid en de richting.", viewModel.NoColorText);
    }

    // A later effect without a color and without a direction does not promise a direction.
    [Fact]
    public void NoColorSentence_NamesTheDirectionOnlyWhenThereIsOne()
    {
        Assert.Equal("Plasma maakt zijn eigen kleuren. Kies hieronder de helderheid en de snelheid.", MainViewModel.NoColorSentence("Plasma", hasDirection: false));
        Assert.Equal("Plasma maakt zijn eigen kleuren. Kies hieronder de helderheid, de snelheid en de richting.", MainViewModel.NoColorSentence("Plasma", hasDirection: true));
    }

    // A tile chooses its effect, as the effect choice did, and card 4 follows.
    [Fact]
    public async Task Tile_ChoosesTheEffectAndTheCardFollows()
    {
        var viewModel = await ConnectAsync(EffectIds.Static);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.EffectTiles.ChooseCommand.Execute(Tile(viewModel, EffectIds.Rainbow));

        Assert.Equal(EffectIds.Rainbow, viewModel.SelectedEffect?.Id);
        Assert.Equal(["effect all rainbow"], Api.Calls);
        Assert.True(Tile(viewModel, EffectIds.Rainbow).IsChosen);
        Assert.False(Tile(viewModel, EffectIds.Static).IsChosen);
        Assert.Contains(nameof(MainViewModel.ColorCardHeader), changed);
        Assert.Contains(nameof(MainViewModel.ShowsMixer), changed);
    }

    // With chosen LEDs card 4 is the color for the LEDs, whatever the effect; after Selectie opheffen
    // it shows again what belongs to the effect.
    [Fact]
    public async Task ChosenLeds_MakeTheCardTheColorForTheLeds()
    {
        var viewModel = await ConnectAsync(EffectIds.Rainbow);

        viewModel.Preview.Devices.Single(preview => preview.DeviceId == "strip").ClickLed(0, extend: false);
        Assert.Equal(ColorCardContent.Leds, viewModel.ColorCard);
        Assert.Equal(Strings.LedColorHeader, viewModel.ColorCardHeader);
        Assert.True(viewModel.ShowsMixer);

        await viewModel.ClearLedSelectionCommand.ExecuteAsync(null);
        Assert.Equal(ColorCardContent.NoColor, viewModel.ColorCard);
    }

    // Without own effects "Eigen effect" chooses nothing: the light keeps its effect and the tile of
    // that effect stays chosen; card 4 is "Jouw effect" without the mixing panel, until another effect.
    [Fact]
    public async Task EigenEffectWithoutOwnEffects_ShowsJouwEffectAndKeepsTheLight()
    {
        var viewModel = await ConnectAsync(EffectIds.Static);
        var custom = viewModel.EffectTiles.Tiles.Single(tile => tile.IsCustom);

        viewModel.EffectTiles.ChooseCommand.Execute(custom);

        Assert.Empty(Api.Calls);
        Assert.Equal(EffectIds.Static, viewModel.SelectedEffect?.Id);
        Assert.True(Tile(viewModel, EffectIds.Static).IsChosen);
        Assert.False(custom.IsChosen);
        Assert.Equal(ColorCardContent.CustomEffect, viewModel.ColorCard);
        Assert.Equal(Strings.CustomEffectCardHeader, viewModel.ColorCardHeader);
        Assert.False(viewModel.ShowsMixer);

        // The tile that stayed chosen takes the card back, without sending the effect again.
        viewModel.EffectTiles.ChooseCommand.Execute(Tile(viewModel, EffectIds.Static));
        Assert.Equal(ColorCardContent.Color, viewModel.ColorCard);
        Assert.Empty(Api.Calls);

        viewModel.EffectTiles.ChooseCommand.Execute(custom);
        viewModel.EffectTiles.ChooseCommand.Execute(Tile(viewModel, EffectIds.Rainbow));
        Assert.Equal(ColorCardContent.NoColor, viewModel.ColorCard);
    }

    // An own effect that comes while card 4 shows "Jouw effect" without own effects (another settings
    // app made one, or it was imported) does not choose itself (§ 9.3): the card stays with the
    // choice empty, and the light keeps its effect.
    [Fact]
    public async Task OwnEffectThatComes_WhileJouwEffectIsEmpty_KeepsTheCardAndTheLight()
    {
        var viewModel = await ConnectAsync(EffectIds.Static);
        var custom = viewModel.EffectTiles.Tiles.Single(tile => tile.IsCustom);
        viewModel.EffectTiles.ChooseCommand.Execute(custom);

        Api.CustomEffects = [Disco];
        Api.RaiseCustomEffectsChanged();

        Assert.Equal([Disco.Id], viewModel.EffectTiles.CustomEffects.Select(effect => effect.Id));
        Assert.Equal(ColorCardContent.CustomEffect, viewModel.ColorCard);
        Assert.False(viewModel.ShowsMixer);
        Assert.Null(viewModel.EffectTiles.SelectedCustomEffect);
        Assert.True(custom.CanChoose);
        Assert.False(custom.IsChosen);
        Assert.True(Tile(viewModel, EffectIds.Static).IsChosen);
        Assert.Equal(EffectIds.Static, viewModel.SelectedEffect?.Id);
        Assert.Empty(Api.Calls);
    }

    // Another device or a scene with the same effect shows the card of that effect again, not the
    // "Jouw effect" that was open for the device before.
    [Fact]
    public async Task JouwEffectWithoutOwnEffects_AnotherDeviceWithTheSameEffect_ShowsItsCard()
    {
        var viewModel = await ConnectAsync(EffectIds.Static);
        viewModel.EffectTiles.ChooseCommand.Execute(viewModel.EffectTiles.Tiles.Single(tile => tile.IsCustom));
        Assert.Equal(ColorCardContent.CustomEffect, viewModel.ColorCard);

        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "strip");

        Assert.Equal(EffectIds.Static, viewModel.SelectedEffect?.Id);
        Assert.Equal(ColorCardContent.Color, viewModel.ColorCard);
        Assert.True(viewModel.ShowsMixer);
    }

    [Fact]
    public async Task JouwEffectWithoutOwnEffects_ASceneWithTheSameEffect_ShowsItsCard()
    {
        var viewModel = await ConnectAsync(EffectIds.Static, api =>
        {
            api.Profiles.Add("Avond");
            api.ProfileLighting["Avond"] = api.Lighting[""] with { EffectId = EffectIds.Static };
        });
        viewModel.EffectTiles.ChooseCommand.Execute(viewModel.EffectTiles.Tiles.Single(tile => tile.IsCustom));

        viewModel.Profiles.LoadSceneCommand.Execute(viewModel.Profiles.Cards[0]);

        Assert.Equal(["profile load Avond"], Api.Calls);
        Assert.Equal(EffectIds.Static, viewModel.SelectedEffect?.Id);
        Assert.Equal(ColorCardContent.Color, viewModel.ColorCard);
    }

    // With own effects "Eigen effect" chooses the one used last, and the choice in card 4 another.
    [Fact]
    public async Task EigenEffect_ChoosesTheOwnEffectUsedLast()
    {
        var viewModel = await ConnectAsync(EffectIds.Static, Avond, Disco);
        var custom = viewModel.EffectTiles.Tiles.Single(tile => tile.IsCustom);

        viewModel.EffectTiles.ChooseCommand.Execute(custom);
        Assert.Equal(Avond.Id, viewModel.SelectedEffect?.Id);

        viewModel.EffectTiles.SelectedCustomEffect = viewModel.EffectTiles.CustomEffects.Single(effect => effect.Id == Disco.Id);
        viewModel.EffectTiles.ChooseCommand.Execute(Tile(viewModel, EffectIds.Static));
        viewModel.EffectTiles.ChooseCommand.Execute(custom);

        Assert.Equal(Disco.Id, viewModel.SelectedEffect?.Id);
        Assert.True(custom.IsChosen);
        Assert.Equal(["effect all custom:avond", "effect all custom:disco", "effect all static", "effect all custom:disco"], Api.Calls);
    }

    // "+ Eigen effect maken" from "Jouw effect" chooses the new effect: the card stays "Jouw effect",
    // now with the editor and the mixing panel for its step.
    [Fact]
    public async Task NewCustomEffect_FromJouwEffect_ChoosesIt()
    {
        var viewModel = await ConnectAsync(EffectIds.Rainbow);
        viewModel.EffectTiles.ChooseCommand.Execute(viewModel.EffectTiles.Tiles.Single(tile => tile.IsCustom));

        viewModel.NewCustomEffectCommand.Execute(null);

        Assert.True(viewModel.IsCustomEffect);
        Assert.Equal(ColorCardContent.CustomEffect, viewModel.ColorCard);
        Assert.True(viewModel.ShowsMixer);
        Assert.True(viewModel.EffectTiles.Tiles.Single(tile => tile.IsCustom).IsChosen);
        Assert.Same(viewModel.SelectedEffect, viewModel.EffectTiles.SelectedCustomEffect);
    }

    private static EffectTileItem Tile(MainViewModel viewModel, string effectId) =>
        viewModel.EffectTiles.Tiles.Single(tile => tile.Effect?.Id == effectId);

    private Task<MainViewModel> ConnectAsync(string effectId, params CustomEffect[] customEffects) =>
        ConnectAsync(effectId, _ => { }, customEffects);

    private async Task<MainViewModel> ConnectAsync(string effectId, Action<FakeEngineApi> arrange, params CustomEffect[] customEffects)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api =>
        {
            arrange(api);
            api.Effects =
            [
                EffectIds.Static, EffectIds.Breathing, EffectIds.Rainbow, EffectIds.Wave, EffectIds.ColorLoop,
                EffectIds.Twinkle, EffectIds.Comet, EffectIds.Scanner, EffectIds.Fire, EffectIds.PerLed,
            ];
            api.CustomEffects = customEffects.Length > 0 || !CustomEffects.IsCustom(effectId) ? [.. customEffects] : [Disco];
            api.Lighting[""] = api.Lighting[""] with { EffectId = effectId };
        }));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
