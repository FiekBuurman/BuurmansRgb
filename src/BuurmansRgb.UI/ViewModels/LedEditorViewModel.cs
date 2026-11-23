using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// The LEDs picked in the preview, with the stored colors of their device and its layer over the
// effect. An edit changes those colors here first and returns what the engine must store, so the
// caller only has to send it. PaintsLayer says where a color goes: "Per LED" (the per-LED effect,
// which the device then shows) or "Als laag boven het effect" (a fixed color over any effect).
// The picked LEDs can have gaps (Ctrl+click); an edit then holds one piece per run of picked LEDs
// without a gap, so an LED that is not picked is never sent: the colors kept here for it can be
// older than what the engine has now, after a scene from the tray for example.
public sealed partial class LedEditorViewModel : ObservableObject
{
    private RgbColor[] _colors = [];
    private RgbColor?[] _layer = [];
    private int _anchor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(CanFillGradient), nameof(SelectionText), nameof(CanRemoveLayer), nameof(AnchorColor))]
    public partial LedSelection? Selection { get; private set; }

    // Where a color goes; it stays for the next LEDs, until an LED with a layer color is clicked
    // (Start), which shows the layer.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PaintsPerLed), nameof(AnchorColor), nameof(SelectionHint))]
    public partial bool PaintsLayer { get; set; }

    // The other radio button of the same choice.
    public bool PaintsPerLed
    {
        get => !PaintsLayer;
        set => PaintsLayer = !value;
    }

    public bool HasSelection => Selection is not null;

    public bool CanFillGradient => Selection is { Count: >= 2 };

    // "Laag weghalen": only when a chosen LED has a color in the layer.
    public bool CanRemoveLayer => Selection is { } selection && selection.Leds.Any(led => _layer[led] is not null);

    // LED numbers start at 1 for the user. LEDs with gaps between them (Ctrl+click) are counted.
    public string? SelectionText => Selection switch
    {
        null => null,
        { Count: 1 } selection => UiText.Format(Strings.LedSelectionSingle, selection.First + 1, selection.DeviceName),
        { IsRange: true } selection => UiText.Format(Strings.LedSelectionRange, selection.First + 1, selection.Last + 1, selection.DeviceName),
        var selection => UiText.Format(Strings.LedSelectionLoose, UiText.LedCount(selection.Count), selection.DeviceName),
    };

    public string SelectionHint => PaintsLayer ? Strings.LedLayerHint : Strings.LedSelectionHint;

    // The color of the LED that was clicked without Shift, for the color picker: in the layer its
    // color there, or for an LED without one what the per-LED effect gives it. When Ctrl+click took
    // that LED out, the first chosen one.
    public RgbColor AnchorColor
    {
        get
        {
            if (_colors.Length == 0)
            {
                return RgbColor.Black;
            }

            var led = Selection is { } selection && !selection.Contains(_anchor) ? selection.First : _anchor;
            return PaintsLayer && _layer[led] is { } layered ? layered : _colors[led];
        }
    }

    // The number of LEDs of the device being edited, or 0.
    public int LedCount => _colors.Length;

    public bool IsEditing(string deviceId) => Selection?.DeviceId == deviceId;

    // Selects one LED of a device whose stored colors, and layer when it has one, are given. An index
    // the colors do not cover, for example from a preview that is out of date, is ignored. An LED
    // with a color in the layer shows the layer, so the next color changes what it shows.
    public void Start(string deviceId, string deviceName, IReadOnlyList<RgbColor> storedColors, int index, IReadOnlyList<RgbColor?>? layer = null)
    {
        if (index < 0 || index >= storedColors.Count)
        {
            return;
        }

        _colors = [.. storedColors];
        _layer = new RgbColor?[_colors.Length];
        for (var i = 0; i < Math.Min(_layer.Length, layer?.Count ?? 0); i++)
        {
            _layer[i] = layer![i];
        }

        _anchor = index;
        if (_layer[index] is not null)
        {
            PaintsLayer = true;
        }

        Selection = new(deviceId, deviceName, index, index);
    }

    // Selects another LED of the device being edited; with extend, every LED from the last
    // clicked one up to this one, in place of what was chosen.
    public void Select(int index, bool extend)
    {
        if (Selection is not { } selection || index < 0 || index >= _colors.Length)
        {
            return;
        }

        if (!extend)
        {
            _anchor = index;
        }

        Selection = selection with { Leds = LedSet.Range(Math.Min(_anchor, index), Math.Max(_anchor, index)) };
    }

    // Ctrl+click or Ctrl+Space: the LED joins the chosen ones, or leaves them when it was one, and it
    // becomes where a next Shift+click starts, as in Windows. With extend (Ctrl+Shift+click) every LED
    // from there up to this one joins. Taking out the last chosen LED ends the edit, as "Selectie
    // opheffen" does; HasSelection then says so.
    public void Toggle(int index, bool extend)
    {
        if (Selection is not { } selection || index < 0 || index >= _colors.Length)
        {
            return;
        }

        if (extend)
        {
            Selection = selection with { Leds = selection.Leds.Union(LedSet.Range(Math.Min(_anchor, index), Math.Max(_anchor, index))) };
            return;
        }

        _anchor = index;
        var leds = selection.Leds.Toggle(index);
        if (leds.Count == 0)
        {
            Clear();
            return;
        }

        Selection = selection with { Leds = leds };
    }

    // Selects the LEDs first to last of the device being edited, such as a whole zone; the first
    // one becomes the anchor. A range the colors do not cover is ignored.
    public void SelectRange(int first, int last)
    {
        if (Selection is not { } selection || first < 0 || last < first || last >= _colors.Length)
        {
            return;
        }

        _anchor = first;
        Selection = selection with { Leds = LedSet.Range(first, last) };
    }

    public IReadOnlyList<LedEdit>? Paint(RgbColor color)
    {
        if (Selection is not { } selection)
        {
            return null;
        }

        foreach (var led in selection.Leds)
        {
            _colors[led] = color;
        }

        return EditsOf(selection);
    }

    // From the color of the first selected LED to that of the last one, in even steps over the chosen
    // LEDs, also with gaps between them.
    public IReadOnlyList<LedEdit>? FillGradient()
    {
        if (Selection is not { Count: >= 2 } selection)
        {
            return null;
        }

        var from = _colors[selection.First];
        var to = _colors[selection.Last];
        for (var i = 0; i < selection.Count; i++)
        {
            _colors[selection.Leds[i]] = RgbColor.Lerp(from, to, i / (double)(selection.Count - 1));
        }

        return EditsOf(selection);
    }

    // The chosen LEDs get color in the layer, over whatever effect the device shows.
    public IReadOnlyList<LayerEdit>? PaintLayer(RgbColor color)
    {
        if (Selection is not { } selection)
        {
            return null;
        }

        foreach (var led in selection.Leds)
        {
            _layer[led] = color;
        }

        return LayerEditsOf(selection);
    }

    // A gradient in the layer, from the first chosen LED to the last; an end without a layer color
    // starts from what the per-LED effect gives it, as the picker shows it.
    public IReadOnlyList<LayerEdit>? FillLayerGradient()
    {
        if (Selection is not { Count: >= 2 } selection)
        {
            return null;
        }

        var from = _layer[selection.First] ?? _colors[selection.First];
        var to = _layer[selection.Last] ?? _colors[selection.Last];
        for (var i = 0; i < selection.Count; i++)
        {
            _layer[selection.Leds[i]] = RgbColor.Lerp(from, to, i / (double)(selection.Count - 1));
        }

        return LayerEditsOf(selection);
    }

    // "Laag weghalen": the chosen LEDs show the effect again.
    public IReadOnlyList<LayerEdit>? RemoveLayer()
    {
        if (!CanRemoveLayer || Selection is not { } selection)
        {
            return null;
        }

        foreach (var led in selection.Leds)
        {
            _layer[led] = null;
        }

        return LayerEditsOf(selection);
    }

    public void Clear()
    {
        Selection = null;
        _colors = [];
        _layer = [];
    }

    // One piece per run of chosen LEDs: those between them that are not chosen are left out.
    private List<LedEdit> EditsOf(LedSelection selection) =>
        [.. selection.Leds.Runs().Select(run => new LedEdit(selection.DeviceId, run.First, _colors[run.First..(run.Last + 1)]))];

    private List<LayerEdit> LayerEditsOf(LedSelection selection)
    {
        List<LayerEdit> edits = [.. selection.Leds.Runs().Select(run => new LayerEdit(selection.DeviceId, run.First, _layer[run.First..(run.Last + 1)]))];
        OnPropertyChanged(nameof(CanRemoveLayer));
        return edits;
    }
}
