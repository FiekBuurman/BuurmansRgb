using System.Reflection;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// An effect that uses no color of the device, such as the rainbow (EffectIds.UsesDeviceColor): the
// Kleur page says so under the mixed color, and the mixed color goes nowhere: it only stays in the
// picker, for the own colors.
public sealed class MainViewModelDeviceColorTests
{
    private static readonly CustomEffect Disco = new("custom:disco", "Disco", [new CustomEffectStep(Red), new CustomEffectStep(Blue)]);

    // Every effect id in Contracts, so that a new one is offered here too.
    private static readonly string[] BuiltInEffects =
    [
        .. typeof(EffectIds).GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => (string)field.GetValue(null)!),
    ];

    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    // Every effect of the engine (EveryBuiltInEffect_IsInTheTheory): only the rainbow leaves the
    // color nowhere to go. The color loop, the effects with a list and an own effect color a tile or
    // a step, the others the device; all of them at once.
    [Theory]
    [InlineData(EffectIds.Static, false)]
    [InlineData(EffectIds.Breathing, false)]
    [InlineData(EffectIds.Rainbow, true)]
    [InlineData(EffectIds.Wave, false)]
    [InlineData(EffectIds.ColorLoop, false)]
    [InlineData(EffectIds.Twinkle, false)]
    [InlineData(EffectIds.Comet, false)]
    [InlineData(EffectIds.Scanner, false)]
    [InlineData(EffectIds.Fire, false)]
    [InlineData(EffectIds.PerLed, false)]
    [InlineData("custom:disco", false)]
    public async Task SwitchEffect_SaysWhetherTheColorIsUsed(string effectId, bool unused)
    {
        var viewModel = await ConnectAsync();

        viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == effectId);

        Assert.Equal(unused, viewModel.ColorPicker.IsUnused);
    }

    // A new effect in EffectIds needs a row above, so that what the picker does with it is said.
    [Fact]
    public void EveryBuiltInEffect_IsInTheTheory()
    {
        var rows = typeof(MainViewModelDeviceColorTests).GetMethod(nameof(SwitchEffect_SaysWhetherTheColorIsUsed))!
            .GetCustomAttributes<InlineDataAttribute>()
            .Select(row => (string)row.Data[0]!);

        Assert.Empty(BuiltInEffects.Except(rows));
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

    // Mixing sends nothing; the mixed color can still become an own color. After choosing Statisch
    // a mixed color goes on the device again.
    [Fact]
    public async Task Rainbow_MixedColorGoesOnlyToTheOwnColors()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Rainbow });

        viewModel.ColorPicker.HexColor = "#00FF00";

        Assert.True(viewModel.ColorPicker.HasUnappliedChange);
        Assert.Empty(Api.Calls);
        Assert.True(viewModel.CustomColors.AddCommand.CanExecute(null));
        viewModel.CustomColors.AddCommand.Execute(null);
        Assert.Equal(["custom colors #00FF00"], Api.Calls);

        viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == EffectIds.Static);
        Api.Calls.Clear();
        viewModel.ColorPicker.HexColor = "#FF00FF";

        Assert.False(viewModel.ColorPicker.IsUnused);
        Assert.Equal(["color all #FF00FF"], Api.Calls);
    }

    // "Hele huis" with the rainbow says so; a device with Statisch under it takes the color.
    [Fact]
    public async Task WholeHouseWithRainbow_AndADeviceWithStatic()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Rainbow });
        Assert.Null(viewModel.SelectedLamp);
        Assert.True(viewModel.ColorPicker.IsUnused);

        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        await Eventually.TrueAsync(() => viewModel.SelectedEffect?.Id == EffectIds.Static);
        Assert.False(viewModel.ColorPicker.IsUnused);
        viewModel.ColorPicker.HexColor = "#FF00FF";
        Assert.Contains("color fan #FF00FF", Api.Calls);
    }

    // A scene that puts the rainbow on from outside the app makes a mixed color go nowhere too.
    [Fact]
    public async Task FollowLighting_ToRainbow_MixedColorGoesNowhere()
    {
        var viewModel = await ConnectAsync();
        Assert.False(viewModel.ColorPicker.IsUnused);

        Api.Lighting[""] = Api.Lighting[""] with { EffectId = EffectIds.Rainbow };
        Api.RaiseLightingChanged();

        await Eventually.TrueAsync(() => viewModel.ColorPicker.IsUnused);
        Api.Calls.Clear();
        viewModel.ColorPicker.HexColor = "#FF00FF";
        Assert.Empty(Api.Calls);
    }

    // A scene with Statisch after mixing with the rainbow: the mixed color was not for the device,
    // so the picker shows the device, as when the user picks Statisch, and puts no color on that the
    // user may no longer know about.
    [Fact]
    public async Task FollowLighting_FromRainbowToStaticWithAMixedColor_ShowsTheDeviceColor()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Rainbow });
        viewModel.ColorPicker.HexColor = "#00FF00";

        Api.Lighting[""] = new(Blue, 0.3, EffectIds.Static);
        Api.RaiseLightingChanged();

        await Eventually.TrueAsync(() => viewModel.SelectedEffect?.Id == EffectIds.Static);
        Assert.Equal(Blue, viewModel.ColorPicker.Color);
        Assert.False(viewModel.ColorPicker.HasUnappliedChange);
        Assert.DoesNotContain(Api.Calls, call => call.StartsWith("color", StringComparison.Ordinal));
    }

    // A scene that keeps the rainbow keeps the mixed color too, for the own colors.
    [Fact]
    public async Task FollowLighting_RainbowStaysWithAMixedColor_KeepsIt()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.Rainbow });
        viewModel.ColorPicker.HexColor = "#00FF00";

        Api.Lighting[""] = new(Blue, 0.3, EffectIds.Rainbow);
        Api.RaiseLightingChanged();

        await Eventually.TrueAsync(() => viewModel.Brightness == 30.0);
        Assert.Equal(new RgbColor(0, 255, 0), viewModel.ColorPicker.Color);
        Assert.True(viewModel.ColorPicker.IsUnused);
        Assert.True(viewModel.ColorPicker.HasUnappliedChange);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api =>
        {
            api.Effects = BuiltInEffects;
            api.CustomEffects = [Disco];
            arrange?.Invoke(api);
        }));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
