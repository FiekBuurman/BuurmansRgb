using BuurmansRgb.Contracts;
using BuurmansRgb.Contracts.Testing;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// Choosing LEDs in the preview and coloring them, without MainViewModel: a session on a fake
// engine, a preview and lamps of its devices (a strip of three LEDs and a fan of two).
public sealed class LedEditCoordinatorTests
{
    private static readonly RgbColor Blue = new(0, 0, 255);

    private readonly FakeEngineConnection _connection = new();
    private readonly EngineSession _session;
    private readonly PreviewViewModel _preview = new();
    private readonly LampsViewModel _lamps = new();
    private readonly LedEditorViewModel _editor = new();
    private readonly LedEditCoordinator _ledEdit;
    private readonly List<string> _ledsSent = [];
    private IReadOnlyList<DeviceInfo> _devices;
    private int _changesSent;
    private int _selectionsShown;
    private int _selectionsCleared;

    public LedEditCoordinatorTests()
    {
        _session = new EngineSession(new UiThread(), _ => { }, _ => { });
        _devices = Api.Devices;
        _preview.ShowDevices(_devices);
        _lamps.ShowDevices(_devices.Select(device => DeviceItem.For(device)));
        _ledEdit = new LedEditCoordinator(_session, _editor, _preview, _lamps, () => _devices, () =>
        {
            _selectionsCleared++;
            return Task.CompletedTask;
        });
        _ledEdit.ChangeSent += (_, _) => _changesSent++;
        _ledEdit.LedsSent += (_, deviceId) => _ledsSent.Add(deviceId);
        _ledEdit.SelectionShown += (_, _) => _selectionsShown++;
        _session.Attach(_connection);
    }

    private FakeEngineApi Api => _connection.FakeApi;

    [Fact]
    public void ClickLed_StartsTheEditWithTheStoredColorsAndShowsIt()
    {
        Api.LedColors["fan"] = [new RgbColor(1, 2, 3), new RgbColor(9, 8, 7)];

        Preview("fan").ClickLed(1);

        Assert.Equal(new LedSelection("fan", "Fan", 1, 1), _editor.Selection);
        Assert.Equal(new RgbColor(9, 8, 7), _editor.AnchorColor);
        Assert.Equal((1, 1), Preview("fan").ShownSelection());
        Assert.Equal(1, _selectionsShown);
        Assert.Equal(1, Api.LedColorsCalls);
        Assert.Empty(Api.Calls);
    }

    // A second click while the stored colors are on their way waits for the same colors, and makes
    // a range from the LED the first one chose.
    [Fact]
    public async Task ClickLed_TwiceBeforeTheColorsCame_FetchesThemOnce()
    {
        var gate = new TaskCompletionSource();
        Api.LedColorsGate = gate.Task;

        Preview("strip").ClickLed(0);
        Preview("strip").ClickLed(2, extend: true);
        gate.SetResult();

        await Eventually.TrueAsync(() => _editor.Selection == new LedSelection("strip", "LED strip", 0, 2));
        Assert.Equal(1, Api.LedColorsCalls);
    }

    [Fact]
    public void ClickLed_WithoutConnection_StartsNothing()
    {
        _session.Detach();

        Preview("strip").ClickLed(0);

        Assert.False(_editor.HasSelection);
        Assert.Equal(0, Api.LedColorsCalls);
    }

    // The device went while its colors were fetched.
    [Fact]
    public void ClickLed_DeviceGone_StartsNothing()
    {
        _devices = [];

        Preview("strip").ClickLed(0);

        Assert.False(_editor.HasSelection);
        Assert.Equal(0, _selectionsShown);
    }

    // Ctrl+click on the last chosen LED goes as "Selectie opheffen" does.
    [Fact]
    public void ClickLed_TakingOutTheLastLed_ClearsTheSelection()
    {
        Preview("strip").ClickLed(1);

        Preview("strip").ClickLed(1, toggle: true);

        Assert.False(_editor.HasSelection);
        Assert.Equal((-1, -1), Preview("strip").ShownSelection());
        Assert.Equal(1, _selectionsCleared);
    }

    [Fact]
    public void TryPaint_WithoutChosenLeds_IsFalse()
    {
        Assert.False(_ledEdit.TryPaint(Blue));

        Assert.Empty(Api.Calls);
        Assert.Equal(0, _changesSent);
    }

    [Fact]
    public void TryPaint_PerLed_SendsTheLedsAndSwitchesTheLampOn()
    {
        Preview("strip").ClickLed(0);
        Preview("strip").ClickLed(1, extend: true);
        Lamp("strip").IsOn = false;

        Assert.True(_ledEdit.TryPaint(Blue));

        Assert.Equal(["leds strip 0 #0000FF,#0000FF"], Api.Calls);
        Assert.Equal(["strip"], _ledsSent);
        Assert.Equal(1, _changesSent);
        Assert.True(Lamp("strip").IsOn);
    }

    // The layer keeps the effect of the device, so no LedsSent.
    [Fact]
    public void TryPaint_AsALayer_SendsTheLayer()
    {
        Preview("fan").ClickLed(0);
        _editor.PaintsLayer = true;
        Lamp("fan").IsOn = false;

        Assert.True(_ledEdit.TryPaint(Blue));

        Assert.Equal(["overlay fan 0 #0000FF"], Api.Calls);
        Assert.Empty(_ledsSent);
        Assert.Equal(1, _changesSent);
        Assert.True(Lamp("fan").IsOn);
    }

    [Fact]
    public void TryPaint_WithoutConnection_SendsNothing()
    {
        Preview("strip").ClickLed(0);
        _session.Detach();

        Assert.True(_ledEdit.TryPaint(Blue));

        Assert.Empty(Api.Calls);
        Assert.Empty(_ledsSent);
        Assert.Equal(0, _changesSent);
    }

    [Fact]
    public void FillGradient_SendsTheGradientOverTheChosenLeds()
    {
        Api.LedColors["strip"] = [new RgbColor(255, 0, 0), new RgbColor(0, 255, 0), Blue];
        Preview("strip").ClickLed(0);
        Preview("strip").ClickLed(2, extend: true);

        _ledEdit.FillGradient();

        Assert.Equal(["leds strip 0 #FF0000,#800080,#0000FF"], Api.Calls);
        Assert.Equal(["strip"], _ledsSent);
    }

    // Taking the layer away leaves a lamp that is off as it is.
    [Fact]
    public void RemoveLayer_TakesTheLayerAwayAndLeavesTheLamp()
    {
        Api.Overlays["strip"] = [Blue, Blue, null];
        Preview("strip").ClickLed(0);
        Preview("strip").ClickLed(2, extend: true);
        Lamp("strip").IsOn = false;

        _ledEdit.RemoveLayer();

        Assert.Equal(["overlay strip 0 -,-,-"], Api.Calls);
        Assert.False(Lamp("strip").IsOn);
        Assert.Empty(_ledsSent);
    }

    // "Selectie opheffen" in the preview: back to coloring the selected device.
    [Fact]
    public void SelectionCleared_InThePreview_EndsTheEditAndShowsTheSelectedDevice()
    {
        Preview("strip").ClickLed(0);

        ((IPreviewActions)_preview).ClearLedSelection();

        Assert.False(_editor.HasSelection);
        Assert.Equal((-1, -1), Preview("strip").ShownSelection());
        Assert.Equal(1, _selectionsCleared);
    }

    [Fact]
    public void EndIfDeviceChanged_SameDevice_KeepsTheEdit()
    {
        Preview("strip").ClickLed(0);

        _ledEdit.EndIfDeviceChanged(_devices);

        Assert.True(_editor.HasSelection);
    }

    // A zone of the strip was resized, or the strip went: the colors no longer fit.
    [Theory]
    [InlineData(5)]
    [InlineData(0)]
    public void EndIfDeviceChanged_OtherLedCountOrGone_EndsTheEdit(int stripLeds)
    {
        Preview("strip").ClickLed(0);
        IReadOnlyList<DeviceInfo> devices = stripLeds == 0
            ? [.. _devices.Where(device => device.Id != "strip")]
            : [.. _devices.Select(device => device.Id == "strip" ? device with { LedCount = stripLeds } : device)];

        _ledEdit.EndIfDeviceChanged(devices);

        Assert.False(_editor.HasSelection);
        Assert.Equal((-1, -1), Preview("strip").ShownSelection());
        Assert.Equal(0, _selectionsCleared);
    }

    private DevicePreviewViewModel Preview(string deviceId) => _preview.Devices.Single(preview => preview.DeviceId == deviceId);

    private LampItem Lamp(string deviceId) => _lamps.Lamps.Single(lamp => lamp.DeviceId == deviceId);
}
