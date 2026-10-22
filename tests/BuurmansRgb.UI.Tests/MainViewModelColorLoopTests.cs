using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class MainViewModelColorLoopTests
{
    private static readonly ColorLoop RedBlue = new([new RgbColor(255, 0, 0), new RgbColor(0, 0, 255)], Spread: false);

    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task Connect_ShowsTheLoopOfTheEngine()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { Loop = RedBlue });

        Assert.Equal(RedBlue.Colors, viewModel.ColorLoopEditor.Swatches.Select(swatch => swatch.Color));
        Assert.False(viewModel.ColorLoopEditor.Spread);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task Connect_WithColorLoop_PickerShowsTheSelectedLoopColor()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.ColorLoop, Loop = RedBlue });

        Assert.True(viewModel.IsColorLoop);
        Assert.Equal(new RgbColor(255, 0, 0), viewModel.ColorPicker.Color);
    }

    [Fact]
    public async Task PickColor_WithColorLoop_ChangesTheSelectedLoopColorNotTheDevice()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.ColorLoop });
        viewModel.ColorLoopEditor.SelectCommand.Execute(1);

        viewModel.ColorPicker.Pick("#FF00FF");

        Assert.Equal(["loop all #FFFF00,#FF00FF,#0000FF spread"], Api.Calls);
    }

    // Without "In de loop zetten": the tile changes with the mixed color and the engine keeps the loop.
    [Fact]
    public async Task MixColor_WithColorLoop_PaintsTheSelectedLoopColorAtOnce()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.ColorLoop });
        viewModel.ColorLoopEditor.SelectCommand.Execute(1);

        viewModel.ColorPicker.HexColor = "#FF00FF";

        Assert.Equal(new RgbColor(255, 0, 255), viewModel.ColorLoopEditor.Swatches[1].Color);
        Assert.True(viewModel.ColorLoopEditor.Swatches[1].IsSelected);
        Assert.Equal(["loop all #FFFF00,#FF00FF,#0000FF spread"], Api.Calls);
        Assert.False(viewModel.ColorPicker.HasUnappliedChange);
    }

    [Fact]
    public async Task SlidersAndPreset_WithColorLoop_PaintTheSelectedLoopColorAtOnce()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.ColorLoop });
        viewModel.ColorLoopEditor.SelectCommand.Execute(2);

        viewModel.ColorPicker.Red = 255;
        viewModel.ColorPicker.SelectPresetCommand.Execute(new RgbColor(255, 154, 61));

        Assert.Equal(["loop all #FFFF00,#00FF00,#FF00FF spread", "loop all #FFFF00,#00FF00,#FF9A3D spread"], Api.Calls);
        Assert.Equal(new RgbColor(255, 154, 61), viewModel.ColorLoopEditor.Swatches[2].Color);
    }

    // "Op de strip zetten" is only for the color of the whole lamp.
    [Fact]
    public async Task SwitchEffect_ThePickerAppliesAtOnceOnlyForTheColorLoop()
    {
        var viewModel = await ConnectAsync();
        Assert.False(viewModel.ColorPicker.AppliesAtOnce);

        viewModel.SelectedEffect = EffectOption.Create(EffectIds.ColorLoop);
        Assert.True(viewModel.ColorPicker.AppliesAtOnce);

        viewModel.SelectedEffect = EffectOption.Create(EffectIds.Static);
        Assert.False(viewModel.ColorPicker.AppliesAtOnce);
    }

    // Another tile keeps the color that was mixed for the one before.
    [Fact]
    public async Task MixColorThenSelectAnother_KeepsTheMixedColorInTheLoop()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.ColorLoop });

        viewModel.ColorPicker.HexColor = "#FF00FF";
        viewModel.ColorLoopEditor.SelectCommand.Execute(1);

        Assert.Equal([new RgbColor(255, 0, 255), new RgbColor(0, 255, 0), new RgbColor(0, 0, 255)], viewModel.ColorLoopEditor.Swatches.Select(swatch => swatch.Color));
        Assert.Equal(new RgbColor(0, 255, 0), viewModel.ColorPicker.Color);
    }

    // A slider dragged while the engine is busy: the loops in between are replaced in the queue,
    // the last one wins, and the lamp goes on once.
    [Fact]
    public async Task DragSlider_WithColorLoop_SendsFewCallsAndTheLastColorWins()
    {
        var viewModel = await ConnectAsync(api => api.Lighting["fan"] = api.Lighting["fan"] with { EffectId = EffectIds.ColorLoop, On = false });
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        Api.Calls.Clear();
        var busy = new TaskCompletionSource();
        Api.SetGate = busy.Task;

        for (var blue = 1; blue <= 255; blue++)
        {
            viewModel.ColorPicker.Blue = blue;
        }

        Api.SetGate = null;
        busy.SetResult();
        await Eventually.TrueAsync(() => Api.Calls.Count >= 3);
        Assert.Equal(["loop fan #FFFF01,#00FF00,#0000FF spread", "on fan on", "loop fan #FFFFFF,#00FF00,#0000FF spread"], Api.Calls);
        Assert.Equal(RgbColor.White, viewModel.ColorLoopEditor.Swatches[0].Color);
        Assert.True(viewModel.SelectedLamp?.IsOn);
    }

    // The engine reports a loop of the drag while the rest is on its way: its answer comes after
    // newer colors were sent, so the tile and the picker keep the last color.
    [Fact]
    public async Task DragSlider_WithColorLoop_LightingChangedMeanwhile_KeepsTheLastColor()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.ColorLoop });
        var busy = new TaskCompletionSource();
        Api.SetGate = busy.Task;
        for (var blue = 1; blue <= 100; blue++)
        {
            viewModel.ColorPicker.Blue = blue;
        }

        Api.Lighting[""] = Api.Lighting[""] with { Loop = new ColorLoop([new RgbColor(255, 255, 1), Green, Blue]) };
        Api.RaiseLightingChanged();
        for (var blue = 101; blue <= 200; blue++)
        {
            viewModel.ColorPicker.Blue = blue;
        }

        Api.SetGate = null;
        busy.SetResult();
        await Eventually.TrueAsync(() => Api.Calls.Contains("loop all #FFFFC8,#00FF00,#0000FF spread"));
        Assert.Equal(new RgbColor(255, 255, 200), viewModel.ColorPicker.Color);
        Assert.Equal(new RgbColor(255, 255, 200), viewModel.ColorLoopEditor.Swatches[0].Color);
        Assert.False(viewModel.ColorPicker.HasUnappliedChange);
        Assert.True(viewModel.ColorPicker.AppliesAtOnce);
    }

    // The engine reports the loop the last step gave it: nothing goes back, nothing changes.
    [Fact]
    public async Task MixColor_WithColorLoop_LightingChangedAfterTheLastStep_SendsNothingBack()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.ColorLoop });
        viewModel.ColorPicker.Blue = 200;
        Api.Calls.Clear();

        Api.Lighting[""] = Api.Lighting[""] with { Loop = new ColorLoop([new RgbColor(255, 255, 200), Green, Blue]) };
        Api.RaiseLightingChanged();

        Assert.Empty(Api.Calls);
        Assert.Equal(new RgbColor(255, 255, 200), viewModel.ColorPicker.Color);
        Assert.Equal(new RgbColor(255, 255, 200), viewModel.ColorLoopEditor.Swatches[0].Color);
        Assert.False(viewModel.ColorPicker.HasUnappliedChange);
    }

    // Back from the LEDs, the picker colors the selected loop color at once again.
    [Fact]
    public async Task ClearLeds_WithColorLoop_ThePickerColorsTheLoopColorAgain()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.ColorLoop });
        viewModel.Preview.Devices.Single(preview => preview.DeviceId == "strip").ClickLed(0);

        await viewModel.ClearLedSelectionCommand.ExecuteAsync(null);

        Assert.True(viewModel.ColorPicker.AppliesAtOnce);
        Assert.Equal(new RgbColor(255, 255, 0), viewModel.ColorPicker.Color);
        viewModel.ColorPicker.Blue = 255;
        Assert.Equal(["loop all #FFFFFF,#00FF00,#0000FF spread"], Api.Calls);
    }

    // The other lamp has no color loop: its color waits for "Op de strip zetten" again.
    [Fact]
    public async Task SelectAnotherLamp_AfterMixingALoopColor_ShowsTheColorOfThatLamp()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.ColorLoop });
        viewModel.ColorPicker.Blue = 255;
        Api.Calls.Clear();

        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        await Eventually.TrueAsync(() => !viewModel.ColorPicker.AppliesAtOnce);
        Assert.Equal(new RgbColor(0, 0, 255), viewModel.ColorPicker.Color);
        Assert.False(viewModel.ColorPicker.HasUnappliedChange);
        Assert.DoesNotContain(Api.Calls, call => call.StartsWith("loop", StringComparison.Ordinal) || call.StartsWith("color", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SelectLoopColor_PickerShowsIt()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.ColorLoop });

        viewModel.ColorLoopEditor.SelectCommand.Execute(2);

        Assert.Equal(new RgbColor(0, 0, 255), viewModel.ColorPicker.Color);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task SwitchEffectAwayFromColorLoop_PickerShowsTheDeviceColorAgain()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.ColorLoop });

        viewModel.SelectedEffect = EffectOption.Create(EffectIds.Static);

        Assert.Equal(new RgbColor(255, 0, 0), viewModel.ColorPicker.Color);
        Assert.Equal(["effect all static"], Api.Calls);
    }

    [Fact]
    public async Task SwitchEffectToColorLoop_PickerShowsTheSelectedLoopColor()
    {
        var viewModel = await ConnectAsync();

        viewModel.SelectedEffect = EffectOption.Create(EffectIds.ColorLoop);

        Assert.True(viewModel.IsColorLoop);
        Assert.Equal(ColorLoop.Default.Colors[0], viewModel.ColorPicker.Color);
    }

    [Fact]
    public async Task PickColor_WithColorLoopForALampThatIsOff_SwitchesItOnAfterTheLoop()
    {
        var viewModel = await ConnectAsync(api => api.Lighting["fan"] = api.Lighting["fan"] with { EffectId = EffectIds.ColorLoop, On = false });
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        Api.Calls.Clear();

        viewModel.ColorPicker.Pick("#FF00FF");

        Assert.Equal(["loop fan #FF00FF,#00FF00,#0000FF spread", "on fan on"], Api.Calls);
        Assert.True(viewModel.SelectedLamp?.IsOn);
    }

    [Fact]
    public async Task AddColor_ForALampThatIsOff_LeavesItOff()
    {
        var viewModel = await ConnectAsync(api => api.Lighting["fan"] = api.Lighting["fan"] with { On = false });
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");
        Api.Calls.Clear();

        viewModel.ColorLoopEditor.AddColorCommand.Execute(null);

        Assert.Equal(["loop fan #FFFF00,#FFFF00,#00FF00,#0000FF spread"], Api.Calls);
        Assert.False(viewModel.SelectedLamp?.IsOn);
    }

    [Fact]
    public async Task AddColor_SendsTheLongerLoopForTheSelectedDevice()
    {
        var viewModel = await ConnectAsync();
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        viewModel.ColorLoopEditor.AddColorCommand.Execute(null);

        Assert.Equal(["flash fan", "loop fan #FFFF00,#FFFF00,#00FF00,#0000FF spread"], Api.Calls);
    }

    [Fact]
    public async Task Spread_Unchecked_SendsTogether()
    {
        var viewModel = await ConnectAsync();

        viewModel.ColorLoopEditor.Spread = false;

        Assert.Equal(["loop all #FFFF00,#00FF00,#0000FF together"], Api.Calls);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api =>
        {
            api.Effects = [EffectIds.Static, EffectIds.Rainbow, EffectIds.ColorLoop, EffectIds.PerLed];
            arrange?.Invoke(api);
        }));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
