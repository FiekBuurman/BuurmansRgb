using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Tests;

// A full-size US keyboard as the simulated one of Providers.Simulated: 104 keys in a key map 24
// cells wide, with the key names OpenRGB gives such a keyboard (without its "Key: " prefix).
internal static class TestKeyboards
{
    public static readonly LedMatrix KeyMap = Map(
        "#.############..###.....",
        "##############..###.####",
        "##############..###.####",
        "############.#......###.",
        "###########..#...#..####",
        "###...#...####..###.#.#.");

    public static readonly IReadOnlyList<string> Names =
    [
        "Escape", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        "Print Screen", "Scroll Lock", "Pause/Break",
        "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "Backspace",
        "Insert", "Home", "Page Up", "Num Lock", "Number Pad /", "Number Pad *", "Number Pad -",
        "Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\",
        "Delete", "End", "Page Down", "Number Pad 7", "Number Pad 8", "Number Pad 9", "Number Pad +",
        "Caps Lock", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "Enter",
        "Number Pad 4", "Number Pad 5", "Number Pad 6",
        "Left Shift", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "Right Shift",
        "Up Arrow", "Number Pad 1", "Number Pad 2", "Number Pad 3", "Number Pad Enter",
        "Left Control", "Left Windows", "Left Alt", "Space", "Right Alt", "Right Windows", "Menu", "Right Control",
        "Left Arrow", "Down Arrow", "Right Arrow", "Number Pad 0", "Number Pad .",
    ];

    public static DeviceInfo Create(string id = "keyboard", string name = "Toetsenbord") =>
        new(id, name, DeviceType.Keyboard, Names.Count, [RgbZone.Fixed("Keys", Names.Count) with { Matrix = KeyMap, LedNames = Names }])
        {
            Source = DeviceSource.Hardware,
        };

    // One row of text per row of keys: '#' is a key, anything else a gap; the keys get their LED in
    // reading order.
    private static LedMatrix Map(params string[] rows)
    {
        var cells = new List<int>();
        var led = 0;
        foreach (var row in rows)
        {
            cells.AddRange(row.Select(cell => cell == '#' ? led++ : LedMatrix.NoLed));
        }

        return new(rows.Length, rows[0].Length, cells);
    }
}
