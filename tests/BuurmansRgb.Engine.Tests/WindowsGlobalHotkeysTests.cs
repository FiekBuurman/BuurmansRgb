using System.Windows.Forms;
using BuurmansRgb.Engine.Hotkeys;

namespace BuurmansRgb.Engine.Tests;

public sealed class WindowsGlobalHotkeysTests
{
    // F24 is on no keyboard, so the real registration steals no key the user has.
    private const Keys UnusedKey = Keys.Control | Keys.Alt | Keys.Shift | Keys.F24;

    // A key counts for the whole Windows session: another test run at the same time can have it,
    // and a session without a desktop has no hotkeys. Then a test skips instead of failing.
    private const string KeyNotFree = "The key is not free or there is no desktop.";

    private const uint WmHotkey = 0x0312;

    [Theory]
    [InlineData(AllOnOffHotkeys.AllOn, 0x4000u | 0x1 | 0x2 | 0x4)]
    [InlineData(Keys.Control | Keys.F9, 0x4000u | 0x2)]
    [InlineData(Keys.F9, 0x4000u)]
    public void ToModifiers_GivesTheModifiersOfRegisterHotKey(Keys key, uint modifiers) =>
        Assert.Equal(modifiers, WindowsGlobalHotkeys.ToModifiers(key));

    // Each key keeps one id of its own, which stays in the range Windows leaves to an app.
    [Fact]
    public void ToId_GivesEachKeyAFixedIdInTheRangeOfAnApp()
    {
        Keys[] keys =
        [
            AllOnOffHotkeys.AllOn, AllOnOffHotkeys.AllOff, UnusedKey,
            Keys.F24, Keys.Control | Keys.F24, Keys.Alt | Keys.F24, Keys.Shift | Keys.F24,
            Keys.Control | Keys.Alt | Keys.Shift | Keys.OemClear,
        ];

        var ids = keys.Select(WindowsGlobalHotkeys.ToId).ToList();

        Assert.Equal(keys.Length, ids.Distinct().Count());
        Assert.All(ids, id => Assert.InRange(id, 0x0000, 0xBFFF));
        Assert.Equal(ids, keys.Select(WindowsGlobalHotkeys.ToId));
    }

    // Another app (here another window) has the key: registering says so and logs it, without
    // throwing. Once that window gives it back, the key can be had.
    [Fact]
    public void Register_KeyTakenElsewhere_FailsAndLogsUntilItIsFree()
    {
        using var trace = new TraceCapture();
        WindowsGlobalHotkeys? other = null;
        WindowsGlobalHotkeys? engine = null;
        try
        {
            other = CreateOrSkip();
            engine = CreateOrSkip();
            if (!other.Register(UnusedKey, () => { }))
            {
                Assert.Skip(KeyNotFree);
            }

            Assert.False(engine.Register(UnusedKey, () => { }));
            Assert.Contains(trace.Messages, message => message.Contains($"The hotkey {UnusedKey} could not be registered", StringComparison.Ordinal));

            other.Dispose();
            if (!engine.Register(UnusedKey, () => { }))
            {
                Assert.Skip(KeyNotFree);
            }
        }
        finally
        {
            other?.Dispose();
            engine?.Dispose();
        }
    }

    // Windows tells the window with WM_HOTKEY and the id of the key. Sent here on the own thread,
    // the message goes straight through WndProc without touching the keyboard: the key with that
    // id does its work, the other key does not, and a key given back does nothing anymore.
    [Fact]
    public void WndProc_HotkeyMessage_RunsTheActionOfThatKeyUntilItIsGivenBack()
    {
        const Keys otherKey = Keys.Control | Keys.Alt | Keys.Shift | Keys.F23;
        WindowsGlobalHotkeys? hotkeys = null;
        try
        {
            hotkeys = CreateOrSkip();
            var pressed = 0;
            var otherPressed = 0;
            if (!hotkeys.Register(UnusedKey, () => pressed++) || !hotkeys.Register(otherKey, () => otherPressed++))
            {
                Assert.Skip(KeyNotFree);
            }

            Assert.True(hotkeys.IsRegistered(UnusedKey));
            SendHotkey(hotkeys, WindowsGlobalHotkeys.ToId(UnusedKey));
            Assert.Equal(1, pressed);
            Assert.Equal(0, otherPressed);

            hotkeys.Unregister(UnusedKey);
            Assert.False(hotkeys.IsRegistered(UnusedKey));
            Assert.True(hotkeys.IsRegistered(otherKey));
            SendHotkey(hotkeys, WindowsGlobalHotkeys.ToId(UnusedKey));
            Assert.Equal(1, pressed);

            SendHotkey(hotkeys, WindowsGlobalHotkeys.ToId(otherKey));
            Assert.Equal(1, pressed);
            Assert.Equal(1, otherPressed);
        }
        finally
        {
            hotkeys?.Dispose();
        }
    }

    private static void SendHotkey(WindowsGlobalHotkeys hotkeys, int id) =>
        MessageOnlyWindows.SendMessage(hotkeys.Handle, WmHotkey, id, 0);

    // The window of the hotkeys, or a skip when this session cannot make one.
    private static WindowsGlobalHotkeys CreateOrSkip() => MessageOnlyWindows.CreateOrSkip(() => new WindowsGlobalHotkeys(), KeyNotFree);
}
