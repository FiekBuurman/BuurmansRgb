using System.Globalization;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

// What the preview writes on a key of a keyboard: short, as on the key itself, from the name the
// provider gives it (as OpenRGB names it, without "Key: "). A name of up to three letters stays as
// it is, such as "W", ";" or "F12", also on the number pad without "Number Pad"; known longer
// names get the short text of Strings.resx ("Esc", "Shift", "↑"), so a translation can follow the
// keyboards of another country. The space bar and names it does not know, longer than three letters, get none.
public static class KeyCapText
{
    private const string NumberPad = "Number Pad ";
    private const int MaxUnknownLength = 3;

    private static readonly Dictionary<string, Func<string>> Known = new(StringComparer.Ordinal)
    {
        ["Escape"] = () => Strings.KeyCapEscape,
        ["Tab"] = () => Strings.KeyCapTab,
        ["Caps Lock"] = () => Strings.KeyCapCapsLock,
        ["Left Shift"] = () => Strings.KeyCapShift,
        ["Right Shift"] = () => Strings.KeyCapShift,
        ["Left Control"] = () => Strings.KeyCapControl,
        ["Right Control"] = () => Strings.KeyCapControl,
        ["Left Windows"] = () => Strings.KeyCapWindows,
        ["Right Windows"] = () => Strings.KeyCapWindows,
        ["Left Alt"] = () => Strings.KeyCapAlt,
        ["Right Alt"] = () => Strings.KeyCapAltGr,
        ["Menu"] = () => Strings.KeyCapMenu,
        ["Enter"] = () => Strings.KeyCapEnter,
        ["Enter (ISO)"] = () => Strings.KeyCapEnter,
        ["Backspace"] = () => Strings.KeyCapBackspace,
        ["Print Screen"] = () => Strings.KeyCapPrintScreen,
        ["Scroll Lock"] = () => Strings.KeyCapScrollLock,
        ["Pause/Break"] = () => Strings.KeyCapPause,
        ["Insert"] = () => Strings.KeyCapInsert,
        ["Delete"] = () => Strings.KeyCapDelete,
        ["Home"] = () => Strings.KeyCapHome,
        ["End"] = () => Strings.KeyCapEnd,
        ["Page Up"] = () => Strings.KeyCapPageUp,
        ["Page Down"] = () => Strings.KeyCapPageDown,
        ["Num Lock"] = () => Strings.KeyCapNumLock,
        ["Up Arrow"] = () => Strings.KeyCapUp,
        ["Down Arrow"] = () => Strings.KeyCapDown,
        ["Left Arrow"] = () => Strings.KeyCapLeft,
        ["Right Arrow"] = () => Strings.KeyCapRight,
        ["Space"] = () => string.Empty,
    };

    // The text for each LED of names; an empty text for a key without one.
    public static string[] Of(IReadOnlyList<string> names) => [.. names.Select(TextOf)];

    public static string TextOf(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        name = name.Trim();
        if (name.StartsWith(NumberPad, StringComparison.Ordinal) && name.Length > NumberPad.Length)
        {
            name = name[NumberPad.Length..];
        }

        if (Known.TryGetValue(name, out var text))
        {
            return text();
        }

        return new StringInfo(name).LengthInTextElements <= MaxUnknownLength ? name : string.Empty;
    }
}
