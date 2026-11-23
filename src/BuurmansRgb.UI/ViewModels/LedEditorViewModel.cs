using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// The LEDs picked in the preview, with the stored colors of their device and its layer over the
// effect. An edit changes those colors here first and returns what the engine must store, so the
// caller only has to send it. PaintsLayer says where a color goes: "Per LED" (the per-LED effect,
// which the device then shows) or "Als laag boven het effect" (a fixed color over any effect).
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
    public bool CanRemoveLayer => Selection is { } selection && _layer.Skip(selection.First).Take(selection.Count).Any(color => color is not null);

    // LED numbers start at 1 for the user.
    public string? SelectionText => Selection switch
    {
        null => null,
        { Count: 1 } selection => UiText.Format(Strings.LedSelectionSingle, selection.First + 1, selection.DeviceName),
        var selection => UiText.Format(Strings.LedSelectionRange, selection.First + 1, selection.Last + 1, selection.DeviceName),
    };

    public string SelectionHint => PaintsLayer ? Strings.LedLayerHint : Strings.LedSelectionHint;

    // The color of the LED that was clicked without Shift, for the color picker: in the layer its
    // color there, or for an LED without one what the per-LED effect gives it.
    public RgbColor AnchorColor => _colors.Length == 0 ? RgbColor.Black
        : PaintsLayer && _layer[_anchor] is { } layered ? layered
        : _colors[_anchor];

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
    // clicked one up to this one.
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

        Selection = selection with { First = Math.Min(_anchor, index), Last = Math.Max(_anchor, index) };
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
        Selection = selection with { First = first, Last = last };
    }

    public LedEdit? Paint(RgbColor color)
    {
        if (Selection is not { } selection)
        {
            return null;
        }

        _colors.AsSpan(selection.First, selection.Count).Fill(color);
        return EditOf(selection);
    }

    // From the color of the first selected LED to that of the last one.
    public LedEdit? FillGradient()
    {
        if (Selection is not { Count: >= 2 } selection)
        {
            return null;
        }

        var from = _colors[selection.First];
        var to = _colors[selection.Last];
        for (var i = 0; i < selection.Count; i++)
        {
            _colors[selection.First + i] = RgbColor.Lerp(from, to, i / (double)(selection.Count - 1));
        }

        return EditOf(selection);
    }

    // The chosen LEDs get color in the layer, over whatever effect the device shows.
    public LayerEdit? PaintLayer(RgbColor color)
    {
        if (Selection is not { } selection)
        {
            return null;
        }

        _layer.AsSpan(selection.First, selection.Count).Fill(color);
        return LayerEditOf(selection);
    }

    // A gradient in the layer, from the first chosen LED to the last; an end without a layer color
    // starts from what the per-LED effect gives it, as the picker shows it.
    public LayerEdit? FillLayerGradient()
    {
        if (Selection is not { Count: >= 2 } selection)
        {
            return null;
        }

        var from = _layer[selection.First] ?? _colors[selection.First];
        var to = _layer[selection.Last] ?? _colors[selection.Last];
        for (var i = 0; i < selection.Count; i++)
        {
            _layer[selection.First + i] = RgbColor.Lerp(from, to, i / (double)(selection.Count - 1));
        }

        return LayerEditOf(selection);
    }

    // "Laag weghalen": the chosen LEDs show the effect again.
    public LayerEdit? RemoveLayer()
    {
        if (!CanRemoveLayer || Selection is not { } selection)
        {
            return null;
        }

        _layer.AsSpan(selection.First, selection.Count).Clear();
        return LayerEditOf(selection);
    }

    public void Clear()
    {
        Selection = null;
        _colors = [];
        _layer = [];
    }

    private LedEdit EditOf(LedSelection selection) =>
        new(selection.DeviceId, selection.First, _colors.AsSpan(selection.First, selection.Count).ToArray());

    private LayerEdit LayerEditOf(LedSelection selection)
    {
        var edit = new LayerEdit(selection.DeviceId, selection.First, _layer.AsSpan(selection.First, selection.Count).ToArray());
        OnPropertyChanged(nameof(CanRemoveLayer));
        return edit;
    }
}
