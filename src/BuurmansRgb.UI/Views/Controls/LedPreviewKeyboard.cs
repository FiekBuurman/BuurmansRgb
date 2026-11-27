using System.Windows.Input;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

// What a key does in a LedPreviewView, without the view: the arrows, Home and End move the active
// LED, with Shift they extend the range, Space and Enter choose the active LED, Ctrl+Space puts it
// in the selection or takes it out, Ctrl+A chooses all and Escape none. With Ctrl the arrows, Home
// and End move the active LED as without it, leaving the selection, as in a list of Windows. Any
// other key is no key of the preview, and the window gets it.
internal static class LedPreviewKeyboard
{
    // The press for a key; null for a key that is no key of the preview.
    public static LedKeyPress? KeyPressOf(Key key, ModifierKeys modifiers)
    {
        if (modifiers == ModifierKeys.Control)
        {
            return key switch
            {
                Key.A => new(LedKey.SelectAll),
                Key.Space => new(LedKey.Toggle),
                Key.Left => new(LedKey.Previous),
                Key.Right => new(LedKey.Next),
                Key.Up => new(LedKey.Up),
                Key.Down => new(LedKey.Down),
                Key.Home => new(LedKey.First),
                Key.End => new(LedKey.Last),
                _ => null,
            };
        }

        if (modifiers is not (ModifierKeys.None or ModifierKeys.Shift))
        {
            return null;
        }

        var extend = modifiers == ModifierKeys.Shift;
        return key switch
        {
            Key.Left => new(LedKey.Previous, extend),
            Key.Right => new(LedKey.Next, extend),
            Key.Up => new(LedKey.Up, extend),
            Key.Down => new(LedKey.Down, extend),
            Key.Home => new(LedKey.First, extend),
            Key.End => new(LedKey.Last, extend),
            Key.Space or Key.Enter => new(LedKey.Select, extend),
            Key.Escape when !extend => new(LedKey.ClearSelection),
            _ => null,
        };
    }

    // KeyPressOf, with for Up and Down the LED in the row above or below activeLed as arrangement
    // draws it. The arrangement is only asked for those two keys.
    public static LedKeyPress? PressOf(Key key, ModifierKeys modifiers, int activeLed, Func<ILedArrangement> arrangement)
    {
        if (KeyPressOf(key, modifiers) is not { } press)
        {
            return null;
        }

        return press.Key is LedKey.Up or LedKey.Down
            ? press with { Target = arrangement().LedInNextRow(activeLed, up: press.Key == LedKey.Up) }
            : press;
    }
}
