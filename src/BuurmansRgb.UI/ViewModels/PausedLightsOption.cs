using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// The Dutch name of a mode comes from Strings.resx ("PausedLightsMode_<mode>"); a mode without one
// shows its English name.
public sealed record PausedLightsOption(PausedLightsMode Mode, string DisplayName)
{
    public static PausedLightsOption Create(PausedLightsMode mode) =>
        new(mode, UiText.Lookup(nameof(PausedLightsMode), mode) ?? mode.ToString());

    // A screen reader names a ComboBox item by its ToString, not by DisplayMemberPath.
    public override string ToString() => DisplayName;
}
