using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// "Als laag boven het effect" from Kleur: the colors of the chosen LEDs go into the layer over the
// effect (SetOverlayAsync), and "Laag weghalen" takes them out again.
public sealed class MainViewModelLayerTests
{
    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task ColorPicked_AsALayer_SendsTheLayerAndKeepsTheEffect()
    {
        var viewModel = await ConnectAsync();
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "strip");
        viewModel.ClickLed("strip", 0);
        viewModel.ClickLed("strip", 1, extend: true);
        viewModel.LedEditor.PaintsLayer = true;

        viewModel.ColorPicker.Pick("#00FF00");

        // Choosing the strip lets it blink, so the user sees which one it is.
        Assert.Equal(["overlay strip 0 #00FF00,#00FF00"], Api.Calls.Where(call => !call.StartsWith("flash", StringComparison.Ordinal)));
        Assert.Equal(EffectIds.Static, viewModel.SelectedEffect?.Id);
        Assert.True(viewModel.RemoveLayerCommand.CanExecute(null));
    }

    // Per LED stays the way it was: the per-LED effect.
    [Fact]
    public async Task ColorPicked_PerLed_SendsTheLedColors()
    {
        var viewModel = await ConnectAsync();
        viewModel.ClickLed("strip", 0);

        viewModel.ColorPicker.Pick("#00FF00");

        Assert.Equal(["leds strip 0 #00FF00"], Api.Calls);
        Assert.False(viewModel.LedEditor.PaintsLayer);
    }

    // A key with a layer color shows the layer: the choice and its color, without sending.
    [Fact]
    public async Task ClickLed_WithALayerColor_ShowsTheLayer()
    {
        var viewModel = await ConnectAsync();
        Api.Overlays["strip"] = [null, new RgbColor(0, 0, 255), null];

        viewModel.ClickLed("strip", 1);

        Assert.True(viewModel.LedEditor.PaintsLayer);
        Assert.Equal(new RgbColor(0, 0, 255), viewModel.ColorPicker.Color);
        Assert.Empty(Api.Calls);
    }

    // Another choice shows the color the LED has there, and sends nothing.
    [Fact]
    public async Task AnotherChoice_ShowsTheColorThere()
    {
        var viewModel = await ConnectAsync();
        Api.Overlays["strip"] = [new RgbColor(0, 0, 255), null, null];
        viewModel.ClickLed("strip", 0);

        viewModel.LedEditor.PaintsPerLed = true;

        Assert.Equal(new RgbColor(255, 0, 0), viewModel.ColorPicker.Color);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task RemoveLayer_TakesTheLayerAwayFromTheChosenLeds()
    {
        var viewModel = await ConnectAsync();
        Api.Overlays["strip"] = [new RgbColor(0, 0, 255), new RgbColor(0, 0, 255), null];
        viewModel.ClickLed("strip", 0);
        viewModel.ClickLed("strip", 2, extend: true);

        Assert.True(viewModel.RemoveLayerCommand.CanExecute(null));
        viewModel.RemoveLayerCommand.Execute(null);

        Assert.Equal(["overlay strip 0 -,-,-"], Api.Calls);
        Assert.False(viewModel.RemoveLayerCommand.CanExecute(null));
        Assert.Equal(new RgbColor(255, 0, 0), viewModel.ColorPicker.Color);
    }

    [Fact]
    public async Task RemoveLayer_WithoutALayerOnTheChosenLeds_CannotRun()
    {
        var viewModel = await ConnectAsync();
        Api.Overlays["strip"] = [null, null, new RgbColor(0, 0, 255)];

        viewModel.ClickLed("strip", 0);

        Assert.False(viewModel.RemoveLayerCommand.CanExecute(null));
    }

    [Fact]
    public async Task FillGradient_AsALayer_SendsTheGradientInTheLayer()
    {
        var viewModel = await ConnectAsync();
        Api.Overlays["strip"] = [new RgbColor(255, 0, 0), null, new RgbColor(0, 0, 255)];
        viewModel.ClickLed("strip", 0);
        viewModel.ClickLed("strip", 2, extend: true);

        viewModel.FillGradientCommand.Execute(null);

        Assert.Equal(["overlay strip 0 #FF0000,#800080,#0000FF"], Api.Calls);
    }

    // As with the colors per LED, a lamp that is off goes on with its first layer color.
    [Fact]
    public async Task ColorPicked_AsALayer_OnALampThatIsOff_SwitchesItOn()
    {
        var viewModel = await ConnectAsync(api => api.Lighting["fan"] = api.Lighting["fan"] with { On = false });
        viewModel.ClickLed("fan", 0);
        viewModel.LedEditor.PaintsLayer = true;

        viewModel.ColorPicker.Pick("#00FF00");

        Assert.Equal(["overlay fan 0 #00FF00", "on fan on"], Api.Calls);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api =>
        {
            api.Effects = [EffectIds.Static, EffectIds.Rainbow, EffectIds.PerLed];
            arrange?.Invoke(api);
        }));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
