namespace BuurmansRgb.UI.ViewModels;

// What a key does in the preview of a device; LedPreviewView tells which key is which.
public enum LedKey
{
    // To the LED before or after the active one, in LED order, also into another zone. These moves
    // leave the selection as it is, with Ctrl too; with Shift they extend it.
    Previous,
    Next,

    // To the row above or below as the preview draws it, or into the zone before or after.
    Up,
    Down,

    // To the first or last LED of the device.
    First,
    Last,

    // Chooses the active LED, as a click on it does.
    Select,

    // Ctrl+Space: puts the active LED in the selection or takes it out, as Ctrl+click does.
    Toggle,

    SelectAll,
    ClearSelection,
}
