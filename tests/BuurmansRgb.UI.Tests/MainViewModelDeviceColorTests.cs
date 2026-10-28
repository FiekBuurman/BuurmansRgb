using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// An effect that uses no color of the device, such as the rainbow (EffectIds.UsesDeviceColor): the
// Kleur page says so under the mixed color, without "Op de strip zetten", and the mixed color only
// stays in the picker, for the own colors.
public sealed class MainViewModelDeviceColorTests
{
    private static readonly CustomEffect Disco = new("custom:disco", "Disco", [new CustomEffectStep(Red), new CustomEffectStep(Blue)]);

    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    // Every effect of the engine: only the rainbow leaves the color nowhere to go. The color loop,
    // the effects with a list and an own effect color a tile or a step at once, so they have no
    // button either, but no message; the others put the color on with the button.
    [Theory]
    [InlineData(EffectIds.Static, false, true)]
    [InlineData(EffectIds.Breathing, false, true)]
    [InlineData(EffectIds.Rainbow, true, false)]
    [InlineData(EffectIds.Wave, false, true)]
    [InlineData(EffectIds.ColorLoop, false, false)]
    [InlineData(EffectIds.Twinkle, false, false)]
    [InlineData(EffectIds.Comet, false, false)]
    [InlineData(EffectIds.Scanner, false, false)]
    [InlineData(EffectIds.Fire, false, false)]
    [InlineData(EffectIds.PerLed, false, true)]
    [InlineData("custom:disco", false, false)]
    public async Task SwitchEffect_SaysWhetherTheColorIsUsed(string effectId, bool unused, bool apply)
    {
        var viewModel = await ConnectAsync();

        viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == effectId);

        Assert.Equal(unused, viewModel.ColorPicker.IsUnused);
        Assert.Equal(apply, viewModel.ColorPicker.ShowsApply);
        Assert.Equal(apply, viewModel.ColorPicker.ApplyCommand.CanExecute(null));
    }

    // The message names the effect, so a later effect without a color says its own name.
    [Fact]
    public async Task Rainbow_TheMessageNamesTheEffect()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Rainbow });

        Assert.True(viewModel.ColorPicker.IsUnused);
        Assert.Equal(UiText.Format(Strings.DeviceColorUnused, "Regenboog"), viewModel.DeviceColorUnusedText);
        Assert.StartsWith("Regenboog gebruikt geen kleur", viewModel.DeviceColorUnusedText, StringComparison.Ordinal);
    }

    // Mixing sends nothing and says nothing about the device; the mixed color can still become an
    // own color, and choosing Statisch brings the button back.
    [Fact]
    public async Task Rainbow_MixedColorGoesOnlyToTheOwnColors()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Rainbow });

        viewModel.ColorPicker.HexColor = "#00FF00";

        Assert.True(viewModel.ColorPicker.HasUnappliedChange);
        Assert.False(viewModel.ColorPicker.ShowsUnappliedChange);
        Assert.Empty(Api.Calls);
        Assert.True(viewModel.CustomColors.AddCommand.CanExecute(null));
        viewModel.CustomColors.AddCommand.Execute(null);
        Assert.Equal(["custom colors #00FF00"], Api.Calls);

        viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == EffectIds.Static);

        Assert.False(viewModel.ColorPicker.IsUnused);
        Assert.True(viewModel.ColorPicker.ShowsApply);
    }

    // "Hele huis" with the rainbow says so; a device with Statisch under it has the button.
    [Fact]
    public async Task WholeHouseWithRainbow_AndADeviceWithStatic()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Rainbow });
        Assert.Null(viewModel.SelectedLamp);
        Assert.True(viewModel.ColorPicker.IsUnused);

        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        await Eventually.TrueAsync(() => viewModel.SelectedEffect?.Id == EffectIds.Static);
        Assert.False(viewModel.ColorPicker.IsUnused);
        Assert.True(viewModel.ColorPicker.ShowsApply);
    }

    // A scene that puts the rainbow on from outside the app takes the button away too.
    [Fact]
    public async Task FollowLighting_ToRainbow_TakesTheButtonAway()
    {
        var viewModel = await ConnectAsync();
        Assert.True(viewModel.ColorPicker.ShowsApply);

        Api.Lighting[""] = Api.Lighting[""] with { EffectId = EffectIds.Rainbow };
        Api.RaiseLightingChanged();

        await Eventually.TrueAsync(() => viewModel.ColorPicker.IsUnused);
        Assert.False(viewModel.ColorPicker.ShowsApply);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api =>
        {
            api.Effects = [EffectIds.Static, EffectIds.Breathing, EffectIds.Rainbow, EffectIds.Wave, EffectIds.ColorLoop, EffectIds.Twinkle, EffectIds.Comet, EffectIds.Scanner, EffectIds.Fire, EffectIds.PerLed];
            api.CustomEffects = [Disco];
            arrange?.Invoke(api);
        }));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
