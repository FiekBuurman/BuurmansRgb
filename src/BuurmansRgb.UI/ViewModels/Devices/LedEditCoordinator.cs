using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.ViewModels;

// Choosing LEDs in the preview on Kleur and coloring them, on the device itself or in the layer
// over its effect: a click, a range or Ctrl+click starts or changes the edit of the LED editor, and
// each color goes to the engine. A coordinator: it calls the engine of the session itself, and
// tells MainViewModel when it sent a change for the lighting (ChangeSent), sent colors of LEDs to a
// device (LedsSent), and showed the chosen LEDs (SelectionShown). Used on the UI thread.
internal sealed class LedEditCoordinator
{
    // The kinds of call in the call queue; with the device and the chosen LEDs they make the key
    // that lets a newer call replace one that still waits.
    private const string LedsCall = "leds";
    private const string LayerCall = "layer";

    private readonly EngineSession _session;
    private readonly LedEditorViewModel _editor;
    private readonly PreviewViewModel _preview;
    private readonly LampsViewModel _lamps;
    private readonly Func<IReadOnlyList<DeviceInfo>> _devices;
    private readonly Func<Task> _showSelection;

    // The LED edit that waits for the stored colors of its device; see EditLedsAsync.
    private StartingEdit? _startingEdit;

    // devices are the devices of the engine, whose names and number of LEDs an edit takes;
    // showSelection shows the selected device again once the edit ends ("Selectie opheffen").
    public LedEditCoordinator(
        EngineSession session,
        LedEditorViewModel editor,
        PreviewViewModel preview,
        LampsViewModel lamps,
        Func<IReadOnlyList<DeviceInfo>> devices,
        Func<Task> showSelection)
    {
        _session = session;
        _editor = editor;
        _preview = preview;
        _lamps = lamps;
        _devices = devices;
        _showSelection = showSelection;
    }

    // A change for the lighting of a device went into the queue, so lighting fetched before it
    // came does not move a slider back.
    public event EventHandler? ChangeSent;

    // Colors of chosen LEDs went to the device with this id, which the engine switches to the
    // per-LED effect.
    public event EventHandler<string>? LedsSent;

    // The chosen LEDs show in the preview; the picker shows the color of the Anchor.
    public event EventHandler? SelectionShown;

    // A mixed color for the chosen LEDs, in the layer when the colors go there. False without
    // chosen LEDs, so the color is for something else.
    public bool TryPaint(RgbColor color)
    {
        if (!_editor.HasSelection)
        {
            return false;
        }

        if (_editor.PaintsLayer)
        {
            SendLayer(_editor.PaintLayer(color));
        }
        else
        {
            SendLeds(_editor.Paint(color));
        }

        return true;
    }

    // In the layer too, when the colors go there.
    public void FillGradient()
    {
        if (_editor.PaintsLayer)
        {
            SendLayer(_editor.FillLayerGradient());
        }
        else
        {
            SendLeds(_editor.FillGradient());
        }
    }

    // "Laag weghalen": the chosen LEDs show the effect again.
    public void RemoveLayer() => SendLayer(_editor.RemoveLayer(), switchOn: false);

    // The edit ends: no LEDs are chosen, in the editor or the preview.
    public void ClearSelection()
    {
        _editor.Clear();
        _preview.ShowLedSelection(null);
    }

    // Back to coloring the selected device.
    public async Task ClearSelectionAsync()
    {
        ClearSelection();
        await _showSelection();
    }

    // The edit ends when its device is gone or got another number of LEDs (a zone that was
    // resized): the colors the editor holds no longer fit the device.
    public void EndIfDeviceChanged(IReadOnlyList<DeviceInfo> devices)
    {
        if (_editor.Selection is { } selection
            && devices.FirstOrDefault(device => device.Id == selection.DeviceId)?.LedCount != _editor.LedCount)
        {
            ClearSelection();
        }
    }

    // A click on an LED in the preview (PreviewActionsCoordinator). A range that the keyboard
    // starts on a device without a selection begins at its Anchor. A Ctrl+click on a device without
    // a selection chooses that LED, as a click does; on a device with one, it puts the LED in or
    // takes it out.
    public Task ClickLedAsync(LedClick click) => click.Toggle
        ? EditLedsAsync(click.DeviceId, click.Anchor >= 0 ? click.Anchor : click.Index, editor => editor.Toggle(click.Index, click.Extend), selectOnStart: click.Extend)
        : EditLedsAsync(click.DeviceId, click.Anchor >= 0 ? click.Anchor : click.Index, editor => editor.Select(click.Index, click.Extend));

    // A click on the name of a zone in the preview: all its LEDs.
    public Task SelectLedsAsync(LedSelection leds) =>
        EditLedsAsync(leds.DeviceId, leds.First, editor => editor.SelectRange(leds.First, leds.Last));

    // The stored colors are fetched when editing starts on a device, at the LED firstIndex; clicks
    // on the same device reuse them, so they also hold the edits that are still on their way to
    // the engine. A click or key on the same device before they came waits for the same colors
    // and starts at the same LED, so two quick Shift+arrows give one range from where the first
    // one came. select then picks the LEDs; without selectOnStart not when this call started the
    // edit, which already picked the LED at firstIndex. When select took out the last chosen LED
    // (Ctrl+click), it goes as "Selectie opheffen" does.
    private async Task EditLedsAsync(string deviceId, int firstIndex, Action<LedEditorViewModel> select, bool selectOnStart = true)
    {
        var started = false;
        if (_session.Current is not { } connection)
        {
            return;
        }

        if (!_editor.IsEditing(deviceId))
        {
            if (_startingEdit is not { } starting || starting.DeviceId != deviceId || !ReferenceEquals(starting.Connection, connection))
            {
                starting = new(connection, deviceId, firstIndex, _session.TryCallAsync(connection, api => api.GetLedColorsAsync(deviceId)), _session.TryCallAsync(connection, api => api.GetOverlayAsync(deviceId)));
                _startingEdit = starting;
            }

            var (ok, colors) = await starting.Colors;
            var (layerOk, layer) = await starting.Layer;
            if (ReferenceEquals(_startingEdit, starting))
            {
                _startingEdit = null;
            }

            if (!ok || !layerOk || !_session.IsCurrent(connection) || _devices().FirstOrDefault(device => device.Id == deviceId) is not { } device)
            {
                return;
            }

            // The first click or key that waited for the colors started already.
            if (!_editor.IsEditing(deviceId))
            {
                _editor.Start(device.Id, device.Name, colors, starting.FirstIndex, layer);
                started = true;
            }
        }

        var hadSelection = _editor.HasSelection;
        if (selectOnStart || !started)
        {
            select(_editor);
        }

        if (hadSelection && !_editor.HasSelection)
        {
            await ClearSelectionAsync();
            return;
        }

        _preview.ShowLedSelection(_editor.Selection);
        SelectionShown?.Invoke(this, EventArgs.Empty);
    }

    // The engine also switches the device to the per-LED effect (LedsSent). A lamp that is off goes
    // on after its colors, as with a color for the whole device. Each run of chosen LEDs is its own
    // engine call, so LEDs between them that are not chosen are never sent (Ctrl+click); the runs of
    // one edit share one place in the queue, so a dragged slider still sends only its latest value,
    // however many runs there are.
    private void SendLeds(IReadOnlyList<LedEdit>? edits)
    {
        if (edits is not [var first, ..] || !PostRuns(LedsCall, first.DeviceId, edits, (api, edit) => api.SetLedColorsAsync(edit.DeviceId, edit.FirstLed, edit.Colors)))
        {
            return;
        }

        _lamps.SwitchOn(first.DeviceId);
        LedsSent?.Invoke(this, first.DeviceId);
    }

    // The layer keeps the effect of the device, so the effect list stays as it is. A lamp that is off
    // goes on after its colors, as with SendLeds; taking the layer away leaves it as it is. One call
    // per run of chosen LEDs, in one place in the queue, as with SendLeds.
    private void SendLayer(IReadOnlyList<LayerEdit>? edits, bool switchOn = true)
    {
        if (edits is not [var first, ..] || !PostRuns(LayerCall, first.DeviceId, edits, (api, edit) => api.SetOverlayAsync(edit.DeviceId, edit.FirstLed, edit.Colors)))
        {
            return;
        }

        if (switchOn)
        {
            _lamps.SwitchOn(first.DeviceId);
        }
    }

    // The runs of one edit as one call in the queue, keyed by the chosen LEDs the editor just made
    // them from. False when there is no engine to send them to.
    private bool PostRuns<T>(string kind, string deviceId, IReadOnlyList<T> edits, Func<IEngineApi, T, Task> send)
    {
        if (!_session.HasConnection || _editor.Selection is not { } selection)
        {
            return false;
        }

        ChangeSent?.Invoke(this, EventArgs.Empty);
        _session.Post((kind, deviceId, selection.Leds), async (IEngineApi api) =>
        {
            // Stopping at the first run that fails is good enough: the runs go in rising order, what
            // makes one fail (a lost engine, a device that went) makes the rest fail too, and the
            // next step of the edit sends all runs again.
            foreach (var edit in edits)
            {
                await send(api, edit);
            }
        });
        return true;
    }

    // An LED edit on DeviceId that starts at the LED FirstIndex once its stored colors come.
    private sealed record StartingEdit(
        IEngineConnection Connection, string DeviceId, int FirstIndex, Task<(bool Ok, IReadOnlyList<RgbColor> Value)> Colors, Task<(bool Ok, IReadOnlyList<RgbColor?> Value)> Layer);
}
