using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class MainViewModelDirectionTests
{
    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task Connect_WithoutDirection_ShowsTheOrderOfTheLeds()
    {
        var viewModel = await ConnectAsync();

        Assert.False(viewModel.HasDirection);
        Assert.Equal(0.0, viewModel.Direction);
        Assert.Equal(Strings.DirectionAlongLeds, viewModel.DirectionText);
        Assert.False(viewModel.ClearDirectionCommand.CanExecute(null));
    }

    [Fact]
    public async Task Connect_WithDirection_ShowsItsDegrees()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { Direction = 135 });

        Assert.True(viewModel.HasDirection);
        Assert.Equal(135.0, viewModel.Direction);
        Assert.Equal("135°", viewModel.DirectionText);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task Direction_Changed_SendsWholeDegrees()
    {
        var viewModel = await ConnectAsync();

        viewModel.Direction = 44.6;

        Assert.True(viewModel.HasDirection);
        Assert.Equal(["direction all 45"], Api.Calls);
    }

    [Fact]
    public async Task Direction_ForOneDevice_SendsForThatDevice()
    {
        var viewModel = await ConnectAsync();
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        viewModel.Direction = 90;

        Assert.Equal(["flash fan", "direction fan 90"], Api.Calls);
    }

    [Fact]
    public async Task ClearDirection_SendsTheOrderOfTheLedsAgain()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { Direction = 200 });

        viewModel.ClearDirectionCommand.Execute(null);

        Assert.False(viewModel.HasDirection);
        Assert.Equal(0.0, viewModel.Direction);
        Assert.Equal(["direction all leds"], Api.Calls);
    }

    [Fact]
    public async Task ChooseDirection_WithoutDirection_SendsZeroDegrees()
    {
        var viewModel = await ConnectAsync();

        viewModel.ChooseDirectionCommand.Execute(null);

        Assert.True(viewModel.HasDirection);
        Assert.Equal("0°", viewModel.DirectionText);
        Assert.False(viewModel.ChooseDirectionCommand.CanExecute(null));
        Assert.Equal(["direction all 0"], Api.Calls);
    }

    [Fact]
    public async Task ChooseDirection_WithDirection_CannotRun()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { Direction = 200 });

        Assert.False(viewModel.ChooseDirectionCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(EffectIds.Rainbow, true, true)]
    [InlineData(EffectIds.Wave, true, true)]
    [InlineData(EffectIds.ColorLoop, true, true)]
    [InlineData(EffectIds.ColorLoop, false, false)]
    [InlineData(EffectIds.Static, true, false)]
    [InlineData(EffectIds.Comet, true, false)]
    [InlineData(EffectIds.PerLed, true, false)]
    public async Task IsDirectionVisible_OnlyForTheEffectsThatRunOverTheLeds(string effectId, bool spread, bool visible)
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { Loop = ColorLoop.Default with { Spread = spread } });

        viewModel.SelectedEffect = EffectOption.Create(effectId);

        Assert.Equal(visible, viewModel.IsDirectionVisible);
    }

    [Fact]
    public async Task Spread_Changed_TellsTheSliderToShow()
    {
        var viewModel = await ConnectAsync();
        viewModel.SelectedEffect = EffectOption.Create(EffectIds.ColorLoop);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.ColorLoopEditor.Spread = false;

        Assert.Contains(nameof(MainViewModel.IsDirectionVisible), changed);
        Assert.False(viewModel.IsDirectionVisible);
    }

    [Fact]
    public void DirectionRange_FollowsTheEngine()
    {
        Assert.Equal(EffectDirection.Min, MainViewModel.MinDirection);
        Assert.Equal(EffectDirection.Max, MainViewModel.MaxDirection);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, arrange ?? (_ => { })));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
