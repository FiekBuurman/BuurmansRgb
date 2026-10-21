using System.Windows.Forms;
using BuurmansRgb.Engine.Hotkeys;

namespace BuurmansRgb.Engine.Tests;

public sealed class WindowsGlobalHotkeysTests
{
    // F24 is on no keyboard, so the real registration steals no key the user has.
    private const Keys UnusedKey = Keys.Control | Keys.Alt | Keys.Shift | Keys.F24;

    [Theory]
    [InlineData(AllOnOffHotkeys.AllOn, 0x4000u | 0x1 | 0x2 | 0x4)]
    [InlineData(Keys.Control | Keys.F9, 0x4000u | 0x2)]
    [InlineData(Keys.F9, 0x4000u)]
    public void ToModifiers_GivesTheModifiersOfRegisterHotKey(Keys key, uint modifiers) =>
        Assert.Equal(modifiers, WindowsGlobalHotkeys.ToModifiers(key));

    // Another app (here another window) has the key: registering says so and logs it, without
    // throwing. Once that window gives it back, the key can be had.
    [Fact]
    public void Register_KeyTakenElsewhere_FailsAndLogsUntilItIsFree()
    {
        using var trace = new TraceCapture();
        var other = new WindowsGlobalHotkeys();
        var engine = new WindowsGlobalHotkeys();
        try
        {
            if (!other.Register(UnusedKey, () => { }))
            {
                Assert.Skip("Windows gives this session no hotkeys (no desktop).");
            }

            Assert.False(engine.Register(UnusedKey, () => { }));
            Assert.Contains(trace.Messages, message => message.Contains($"The hotkey {UnusedKey} could not be registered", StringComparison.Ordinal));

            other.Dispose();
            Assert.True(engine.Register(UnusedKey, () => { }));
        }
        finally
        {
            other.Dispose();
            engine.Dispose();
        }
    }
}
