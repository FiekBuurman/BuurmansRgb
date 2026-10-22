using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The Kleur page follows a change of the lighting that came from outside the app, such as a scene
// from the tray menu or one that came on at its time.
public sealed class MainViewModelFollowLightingTests
{
    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task LightingChanged_AllDevicesSelected_ShowsTheNewLighting()
    {
        var viewModel = await ConnectAsync();

        Api.Lighting[""] = new(Green, 0.3, EffectIds.PerLed, Speed: 1.5);
        Api.RaiseLightingChanged();

        Assert.Equal(Green, viewModel.ColorPicker.Color);
        Assert.False(viewModel.ColorPicker.HasUnappliedChange);
        Assert.Equal(30.0, viewModel.Brightness);
        Assert.Equal(EffectIds.PerLed, viewModel.SelectedEffect?.Id);
        Assert.Equal(150.0, viewModel.Speed);
    }

    [Fact]
    public async Task LightingChanged_ALampSelected_ShowsItsNewLighting()
    {
        var viewModel = await ConnectAsync();
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        Api.Lighting["fan"] = new(Green, 0.2, EffectIds.Static);
        Api.RaiseLightingChanged();

        Assert.Equal(Green, viewModel.ColorPicker.Color);
        Assert.Equal(20.0, viewModel.Brightness);
    }

    // Showing the engine state is not a change of the user, so nothing goes back to the engine.
    [Fact]
    public async Task LightingChanged_SendsNothingBack()
    {
        var viewModel = await ConnectAsync();

        Api.Lighting[""] = new(Green, 0.3, EffectIds.PerLed);
        Api.RaiseLightingChanged();

        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task LightingChanged_WhileMixingAColor_KeepsTheMixedColor()
    {
        var viewModel = await ConnectAsync();
        viewModel.ColorPicker.Green = 255;

        Api.Lighting[""] = new(Blue, 0.3, EffectIds.Static);
        Api.RaiseLightingChanged();

        Assert.Equal(new RgbColor(255, 255, 0), viewModel.ColorPicker.Color);
        Assert.True(viewModel.ColorPicker.HasUnappliedChange);
        Assert.Equal(30.0, viewModel.Brightness);
    }

    // The picker now colors the loop at once, so its next step must not put the color that was
    // mixed for the lamp on the selected loop color.
    [Fact]
    public async Task LightingChanged_ToTheColorLoopWhileMixingAColor_ShowsTheLoopColor()
    {
        var viewModel = await ConnectAsync(api => api.Effects = [EffectIds.Static, EffectIds.ColorLoop]);
        viewModel.ColorPicker.Green = 255;

        Api.Lighting[""] = new(Blue, 0.3, EffectIds.ColorLoop) { Loop = new ColorLoop([Orange, Blue]) };
        Api.RaiseLightingChanged();

        Assert.True(viewModel.ColorPicker.AppliesAtOnce);
        Assert.Equal(Orange, viewModel.ColorPicker.Color);
        Assert.False(viewModel.ColorPicker.HasUnappliedChange);
        viewModel.ColorPicker.Blue = 255;
        Assert.Equal(["loop all #FF88FF,#0000FF spread"], Api.Calls);
        Assert.Equal(new RgbColor(255, 136, 255), viewModel.ColorLoopEditor.Swatches[0].Color);
    }

    [Fact]
    public async Task LightingChanged_FromTheColorLoopAfterMixing_ShowsTheColorOfTheLamp()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Effects = [EffectIds.Static, EffectIds.ColorLoop];
            api.Lighting[""] = new(Red, 0.8, EffectIds.ColorLoop) { Loop = new ColorLoop([Orange, Blue]) };
        });
        viewModel.ColorPicker.Blue = 255;

        Api.Lighting[""] = new(Green, 0.3, EffectIds.Static);
        Api.RaiseLightingChanged();

        Assert.False(viewModel.ColorPicker.AppliesAtOnce);
        Assert.Equal(Green, viewModel.ColorPicker.Color);
        Assert.False(viewModel.ColorPicker.HasUnappliedChange);
        Assert.Equal(["loop all #FF88FF,#0000FF spread"], Api.Calls);
    }

    // The user moved the slider while the lighting was on its way: their value must stay.
    [Fact]
    public async Task LightingChanged_UserChangesSomethingMeanwhile_KeepsTheChangeOfTheUser()
    {
        var viewModel = await ConnectAsync();
        var slowAnswer = new TaskCompletionSource();
        Api.LightingGate = slowAnswer.Task;
        Api.Lighting[""] = new(Green, 0.3, EffectIds.Static);
        Api.RaiseLightingChanged();

        viewModel.Brightness = 70;
        slowAnswer.SetResult();
        Api.LightingGate = null;

        await Eventually.TrueAsync(() => Api.Calls.Contains("brightness all 0.70"));
        Assert.Equal(70.0, viewModel.Brightness);
        Assert.Equal(new RgbColor(255, 0, 0), viewModel.ColorPicker.Color);
    }

    [Fact]
    public async Task LightingChanged_AnotherLampPickedMeanwhile_ShowsThatLamp()
    {
        var viewModel = await ConnectAsync();
        var slowAnswer = new TaskCompletionSource();
        Api.LightingGate = slowAnswer.Task;
        Api.Lighting[""] = new(Green, 0.3, EffectIds.Static);
        Api.RaiseLightingChanged();

        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        slowAnswer.SetResult();

        await Eventually.TrueAsync(() => viewModel.Brightness == 50.0);
        Assert.Equal(new RgbColor(0, 0, 255), viewModel.ColorPicker.Color);
    }

    [Fact]
    public async Task LightingChanged_ColorLoop_ShowsTheNewLoopAndKeepsTheSelectedColor()
    {
        var viewModel = await ConnectAsync(api => api.Effects = [EffectIds.Static, EffectIds.ColorLoop]);
        Api.Lighting[""] = new(Red, 0.8, EffectIds.ColorLoop) { Loop = new ColorLoop([Red, Green, Blue]) };
        Api.RaiseLightingChanged();
        viewModel.ColorLoopEditor.SelectCommand.Execute(1);

        Api.Lighting[""] = new(Red, 0.8, EffectIds.ColorLoop) { Loop = new ColorLoop([Red, Orange, Blue]) };
        Api.RaiseLightingChanged();

        Assert.Equal([Red, Orange, Blue], viewModel.ColorLoopEditor.Loop.Colors);
        Assert.Equal(1, viewModel.ColorLoopEditor.SelectedIndex);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, arrange ?? (_ => { })));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
