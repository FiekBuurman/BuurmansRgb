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

        viewModel.ColorPicker.HexColor = "#FF00FF";

        Assert.Equal(["loop all #FFFF00,#FF00FF,#0000FF spread"], Api.Calls);
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
    public async Task AddColor_SendsTheLongerLoopForTheSelectedDevice()
    {
        var viewModel = await ConnectAsync();
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        viewModel.ColorLoopEditor.AddColorCommand.Execute(null);

        Assert.Equal(["loop fan #FFFF00,#FFFF00,#00FF00,#0000FF spread"], Api.Calls);
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
