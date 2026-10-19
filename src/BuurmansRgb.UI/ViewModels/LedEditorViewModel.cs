using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// The LEDs picked in the preview, with the stored colors of their device. An edit changes those
// colors here first and returns what the engine must store, so the caller only has to send it.
public sealed partial class LedEditorViewModel : ObservableObject
{
    private RgbColor[] _colors = [];
    private int _anchor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(CanFillGradient), nameof(SelectionText))]
    public partial LedSelection? Selection { get; private set; }

    public bool HasSelection => Selection is not null;

    public bool CanFillGradient => Selection is { Count: >= 2 };

    // LED numbers start at 1 for the user.
    public string? SelectionText => Selection switch
    {
        null => null,
        { Count: 1 } selection => UiText.Format(Strings.LedSelectionSingle, selection.First + 1, selection.DeviceName),
        var selection => UiText.Format(Strings.LedSelectionRange, selection.First + 1, selection.Last + 1, selection.DeviceName),
    };

    // The stored color of the LED that was clicked without Shift, for the color picker.
    public RgbColor AnchorColor => _colors.Length > 0 ? _colors[_anchor] : RgbColor.Black;

    // The number of LEDs of the device being edited, or 0.
    public int LedCount => _colors.Length;

    public bool IsEditing(string deviceId) => Selection?.DeviceId == deviceId;

    // Selects one LED of a device whose stored colors are given. An index the colors do not
    // cover, for example from a preview that is out of date, is ignored.
    public void Start(string deviceId, string deviceName, IReadOnlyList<RgbColor> storedColors, int index)
    {
        if (index < 0 || index >= storedColors.Count)
        {
            return;
        }

        _colors = [.. storedColors];
        _anchor = index;
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

    public void Clear()
    {
        Selection = null;
        _colors = [];
    }

    private LedEdit EditOf(LedSelection selection) =>
        new(selection.DeviceId, selection.First, _colors.AsSpan(selection.First, selection.Count).ToArray());
}
