using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The own colors of comet, scanner, twinkle and fire on the Kleur page: the same tiles as the color
// loop, and the color picker colors the selected one at once.
public sealed class MainViewModelEffectColorsTests
{
    // The lamp color of "Hele huis" in the fake engine.
    private static readonly RgbColor LampColor = new(255, 0, 0);

    private static readonly EffectColors RedBlueComet = EffectColors.None.With(EffectIds.Comet, [Red, Blue]);

    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task Connect_WithComet_ShowsItsColorsAndThePickerTheFirst()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Comet, EffectColors = RedBlueComet });

        Assert.True(viewModel.HasEffectColors);
        Assert.False(viewModel.IsColorLoop);
        Assert.Equal([Red, Blue], viewModel.EffectColorsEditor.Swatches.Select(swatch => swatch.Color));
        Assert.True(viewModel.EffectColorsEditor.Swatches[0].IsSelected);
        Assert.Equal(Red, viewModel.ColorPicker.Color);
        Assert.True(viewModel.ColorPicker.AppliesAtOnce);
        Assert.Empty(Api.Calls);
    }

    // Without a list of its own the comet, the scanner and the twinkle have the lamp color, the fire
    // its warm colors; an engine that sends no lists at all is the same.
    [Theory]
    [InlineData(EffectIds.Comet)]
    [InlineData(EffectIds.Scanner)]
    [InlineData(EffectIds.Twinkle)]
    [InlineData(EffectIds.Fire)]
    public async Task Connect_WithoutAList_ShowsTheDefaultColors(string effectId)
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = effectId, EffectColors = null });

        Assert.Equal(EffectColors.Default(effectId, LampColor), viewModel.EffectColorsEditor.Colors);
        Assert.Equal(EffectColors.Default(effectId, LampColor)[0], viewModel.ColorPicker.Color);
    }

    // Without "Op het apparaat zetten": the tile changes with the mixed color and the engine gets the list.
    [Fact]
    public async Task MixColor_WithComet_PaintsTheSelectedColorAtOnce()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Comet, EffectColors = RedBlueComet });
        viewModel.EffectColorsEditor.SelectCommand.Execute(1);

        viewModel.ColorPicker.HexColor = "#FF00FF";

        Assert.Equal(new RgbColor(255, 0, 255), viewModel.EffectColorsEditor.Swatches[1].Color);
        Assert.True(viewModel.EffectColorsEditor.Swatches[1].IsSelected);
        Assert.Equal(["effect-colors all comet #FF0000,#FF00FF"], Api.Calls);
        Assert.False(viewModel.ColorPicker.HasUnappliedChange);
    }

    [Fact]
    public async Task SelectColor_PickerShowsIt()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Comet, EffectColors = RedBlueComet });

        viewModel.EffectColorsEditor.SelectCommand.Execute(1);

        Assert.Equal(Blue, viewModel.ColorPicker.Color);
        Assert.Empty(Api.Calls);
    }

    // The list of the fire starts with its warm colors; + adds a copy after the selected one, and −
    // goes down to one color, not to none.
    [Fact]
    public async Task AddAndRemoveColor_SendTheListOfTheEffect()
    {
        var viewModel = await ConnectAsync(api => api.Lighting["fan"] = api.Lighting["fan"] with { EffectId = EffectIds.Scanner });
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        Api.Calls.Clear();

        viewModel.EffectColorsEditor.AddColorCommand.Execute(null);
        viewModel.EffectColorsEditor.RemoveColorCommand.Execute(null);

        Assert.Equal(["effect-colors fan scanner #0000FF,#0000FF", "effect-colors fan scanner #0000FF"], Api.Calls);
        Assert.False(viewModel.EffectColorsEditor.RemoveColorCommand.CanExecute(null));
    }

    [Fact]
    public async Task AddColor_UpToTwelve()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Fire });

        for (var i = EffectColors.DefaultFire.Count; i < EffectColors.MaxColors; i++)
        {
            viewModel.EffectColorsEditor.AddColorCommand.Execute(null);
        }

        Assert.Equal(EffectColors.MaxColors, viewModel.EffectColorsEditor.Swatches.Count);
        Assert.False(viewModel.EffectColorsEditor.AddColorCommand.CanExecute(null));
    }

    // "Op het apparaat zetten" is only for the color of the whole lamp.
    [Theory]
    [InlineData(EffectIds.Static, false)]
    [InlineData(EffectIds.Rainbow, false)]
    [InlineData(EffectIds.Twinkle, true)]
    [InlineData(EffectIds.Comet, true)]
    [InlineData(EffectIds.Scanner, true)]
    [InlineData(EffectIds.Fire, true)]
    public async Task SwitchEffect_ThePickerAppliesAtOnceOnlyForAList(string effectId, bool atOnce)
    {
        var viewModel = await ConnectAsync();

        viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == effectId);

        Assert.Equal(atOnce, viewModel.HasEffectColors);
        Assert.Equal(atOnce, viewModel.ColorPicker.AppliesAtOnce);
    }

    // Each effect has its own list: the comet keeps what was mixed for it while the fire got
    // colors of its own, also before the engine says so; back on the comet its first color is
    // selected.
    [Fact]
    public async Task SwitchEffect_EachKeepsItsOwnList()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Comet, EffectColors = RedBlueComet });
        viewModel.EffectColorsEditor.SelectCommand.Execute(1);
        viewModel.ColorPicker.Mix("#00FF00");

        viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == EffectIds.Fire);
        Assert.Equal(EffectColors.DefaultFire, viewModel.EffectColorsEditor.Colors);
        Assert.Equal(EffectColors.DefaultFire[0], viewModel.ColorPicker.Color);
        viewModel.ColorPicker.Mix("#0000FF");

        viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == EffectIds.Comet);

        Assert.Equal([Red, Green], viewModel.EffectColorsEditor.Colors);
        Assert.True(viewModel.EffectColorsEditor.Swatches[0].IsSelected);
        Assert.Equal(Red, viewModel.ColorPicker.Color);
        Assert.Equal(
            ["effect-colors all comet #FF0000,#00FF00", "effect all fire", "effect-colors all fire #0000FF,#FF1800,#FF6400,#FFBE1E", "effect all comet"],
            Api.Calls);
    }

    // Away from a list the picker shows the lamp color again, which the list did not change.
    [Fact]
    public async Task SwitchEffectAwayFromAList_PickerShowsTheLampColorAgain()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Comet });
        viewModel.ColorPicker.Mix("#00FF00");

        viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == EffectIds.Static);

        Assert.Equal(LampColor, viewModel.ColorPicker.Color);
        Assert.False(viewModel.ColorPicker.AppliesAtOnce);
        Assert.Equal(["effect-colors all comet #00FF00", "effect all static"], Api.Calls);
    }

    // A list of the comet that still waits does not replace one of the fire, nor the other way round.
    [Fact]
    public async Task ListsOfTwoEffects_WhileTheEngineIsBusy_BothArrive()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Comet });
        var busy = new TaskCompletionSource();
        Api.SetGate = busy.Task;

        viewModel.ColorPicker.Mix("#00FF00");
        viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == EffectIds.Fire);
        viewModel.ColorPicker.Mix("#0000FF");
        Api.SetGate = null;
        busy.SetResult();

        await Eventually.TrueAsync(() => Api.Calls.Count >= 3);
        Assert.Contains("effect-colors all comet #00FF00", Api.Calls);
        Assert.Contains("effect-colors all fire #0000FF,#FF1800,#FF6400,#FFBE1E", Api.Calls);
    }

    // A slider dragged while the engine is busy: the lists in between are replaced in the queue, the
    // last one wins, and the lamp goes on once.
    [Fact]
    public async Task DragSlider_WithTwinkle_SendsFewCallsAndTheLastColorWins()
    {
        var viewModel = await ConnectAsync(api => api.Lighting["fan"] = api.Lighting["fan"] with { EffectId = EffectIds.Twinkle, On = false });
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        Api.Calls.Clear();
        var busy = new TaskCompletionSource();
        Api.SetGate = busy.Task;

        for (var red = 1; red <= 255; red++)
        {
            viewModel.ColorPicker.Red = red;
        }

        Api.SetGate = null;
        busy.SetResult();
        await Eventually.TrueAsync(() => Api.Calls.Count >= 3);
        Assert.Equal(["effect-colors fan twinkle #0100FF", "on fan on", "effect-colors fan twinkle #FF00FF"], Api.Calls);
        Assert.True(viewModel.SelectedLamp?.IsOn);
    }

    // The engine reports another list, such as from a scene: the tiles and the picker follow.
    [Fact]
    public async Task LightingChanged_WithAnotherList_ShowsIt()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Scanner });

        Api.Lighting[""] = Api.Lighting[""] with { EffectColors = EffectColors.None.With(EffectIds.Scanner, [Green, Blue]) };
        Api.RaiseLightingChanged();

        await Eventually.TrueAsync(() => viewModel.EffectColorsEditor.Colors.SequenceEqual([Green, Blue]));
        Assert.Equal(Green, viewModel.ColorPicker.Color);
        Assert.Empty(Api.Calls);
    }

    // Chosen LEDs go first: the picker colors them, not the list of the effect.
    [Fact]
    public async Task PickColor_WithLedsSelected_ColorsTheLedsNotTheList()
    {
        var viewModel = await ConnectAsync(api => api.Lighting["fan"] = api.Lighting["fan"] with { EffectId = EffectIds.Comet });
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        await Eventually.TrueAsync(() => viewModel.HasEffectColors);
        Api.Calls.Clear();
        viewModel.Preview.Devices.Single(preview => preview.DeviceId == "fan").ClickLed(0);
        await Eventually.TrueAsync(() => viewModel.LedEditor.HasSelection);

        viewModel.ColorPicker.Mix("#00FF00");

        Assert.Equal(["leds fan 0 #00FF00"], Api.Calls);
        Assert.Equal([Blue], viewModel.EffectColorsEditor.Colors);
    }

    // Back from the LEDs, the picker colors the selected color of the list at once again.
    [Fact]
    public async Task ClearLeds_WithComet_ThePickerColorsTheListAgain()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Comet, EffectColors = RedBlueComet });
        viewModel.Preview.Devices.Single(preview => preview.DeviceId == "strip").ClickLed(0);

        await viewModel.ClearLedSelectionCommand.ExecuteAsync(null);

        Assert.True(viewModel.ColorPicker.AppliesAtOnce);
        Assert.Equal(Red, viewModel.ColorPicker.Color);
        viewModel.ColorPicker.Blue = 255;
        Assert.Equal(["effect-colors all comet #FF00FF,#0000FF"], Api.Calls);
    }

    // "Standaardkleuren" with the comet: the tiles show the lamp color again, the picker too, and
    // the engine removes the list; with nothing left to reset the button is off.
    [Fact]
    public async Task ResetEffectColors_WithComet_ShowsTheLampColorAgain()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Comet, EffectColors = RedBlueComet.With(EffectIds.Fire, [Green]) });
        viewModel.EffectColorsEditor.SelectCommand.Execute(1);
        Assert.True(viewModel.ResetEffectColorsCommand.CanExecute(null));

        viewModel.ResetEffectColorsCommand.Execute(null);

        Assert.Equal([LampColor], viewModel.EffectColorsEditor.Colors);
        Assert.True(viewModel.EffectColorsEditor.Swatches[0].IsSelected);
        Assert.Equal(LampColor, viewModel.ColorPicker.Color);
        Assert.Equal(["effect-colors all comet default"], Api.Calls);
        Assert.False(viewModel.ResetEffectColorsCommand.CanExecute(null));
        viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == EffectIds.Fire);
        Assert.Equal([Green], viewModel.EffectColorsEditor.Colors);
        Assert.True(viewModel.ResetEffectColorsCommand.CanExecute(null));
    }

    // The fire burns in its four warm colors again, here for one lamp.
    [Fact]
    public async Task ResetEffectColors_WithFire_ShowsTheWarmColorsAgain()
    {
        var viewModel = await ConnectAsync(api => api.Lighting["fan"] = api.Lighting["fan"] with { EffectId = EffectIds.Fire, EffectColors = EffectColors.None.With(EffectIds.Fire, [Blue]) });
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        Api.Calls.Clear();

        viewModel.ResetEffectColorsCommand.Execute(null);

        Assert.Equal(EffectColors.DefaultFire, viewModel.EffectColorsEditor.Colors);
        Assert.Equal(EffectColors.DefaultFire[0], viewModel.ColorPicker.Color);
        Assert.Equal(["effect-colors fan fire default"], Api.Calls);
    }

    // Only a list of its own can go: not the default, and not with an effect without a list.
    [Fact]
    public async Task ResetEffectColors_OnlyWithAListOfItsOwn()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Scanner });
        Assert.False(viewModel.ResetEffectColorsCommand.CanExecute(null));

        viewModel.ColorPicker.Mix("#00FF00");
        Assert.True(viewModel.ResetEffectColorsCommand.CanExecute(null));

        viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == EffectIds.Static);
        Assert.False(viewModel.ResetEffectColorsCommand.CanExecute(null));

        Api.Lighting[""] = Api.Lighting[""] with { EffectId = EffectIds.Scanner, EffectColors = EffectColors.None.With(EffectIds.Scanner, [Blue]) };
        Api.RaiseLightingChanged();
        await Eventually.TrueAsync(() => viewModel.ResetEffectColorsCommand.CanExecute(null));
    }

    // A list mixed while the engine is busy does not come after the reset: the reset replaces it.
    [Fact]
    public async Task ResetEffectColors_WhileAListWaits_ReplacesIt()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Twinkle });
        var busy = new TaskCompletionSource();
        Api.SetGate = busy.Task;
        viewModel.ColorPicker.Mix("#00FF00");
        viewModel.ColorPicker.Mix("#0000FF");

        viewModel.ResetEffectColorsCommand.Execute(null);
        Api.SetGate = null;
        busy.SetResult();

        await Eventually.TrueAsync(() => Api.Calls.Count >= 2);
        Assert.Equal(["effect-colors all twinkle #00FF00", "effect-colors all twinkle default"], Api.Calls);
        Assert.Equal([LampColor], viewModel.EffectColorsEditor.Colors);
    }

    // What the colors do differs per effect, in Dutch.
    [Fact]
    public async Task EffectColorsHint_SaysWhatTheColorsDoInTheEffect()
    {
        var viewModel = await ConnectAsync();
        var hints = new List<string>();

        foreach (var effectId in new[] { EffectIds.Twinkle, EffectIds.Comet, EffectIds.Scanner, EffectIds.Fire })
        {
            viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == effectId);
            hints.Add(viewModel.EffectColorsHint);
        }

        Assert.All(hints, hint => Assert.False(string.IsNullOrWhiteSpace(hint)));
        Assert.Equal(4, hints.Distinct().Count());
        Assert.Contains("ronde", hints[1], StringComparison.Ordinal);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api =>
        {
            api.Effects = [EffectIds.Static, EffectIds.Rainbow, EffectIds.ColorLoop, EffectIds.Twinkle, EffectIds.Comet, EffectIds.Scanner, EffectIds.Fire, EffectIds.PerLed];
            arrange?.Invoke(api);
        }));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
