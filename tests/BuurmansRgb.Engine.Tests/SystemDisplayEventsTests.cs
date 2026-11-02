using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Power;
using Microsoft.Win32;

namespace BuurmansRgb.Engine.Tests;

public sealed class SystemDisplayEventsTests
{
    private const uint WmPowerBroadcast = 0x0218;
    private const nint PbtPowerSettingChange = 0x8013;
    private const string NoWindow = "This session cannot make a window.";
    private const string AskingAgain = "asking Windows again whether it is off";

    private static readonly Guid OtherSetting = new("02731015-4510-4526-99e6-e5a17ebd1aea");

    // GUID_CONSOLE_DISPLAY_STATE, the screen of the console that the engine followed before.
    private static readonly Guid ConsoleDisplayState = new("6fe69556-704a-47a0-8f24-c28d936fda47");
    private static readonly TimeSpan StateTimeout = TimeSpan.FromSeconds(3);

    // GUID_SESSION_DISPLAY_STATUS gives 0 for off, 1 for on and 2 for dimmed.
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    public void ReadIsOff_OnlyOffIsOff(int state, bool off) =>
        Assert.Equal(off, SystemDisplayEvents.ReadIsOff(Setting(SystemDisplayEvents.SessionDisplayStatus, state)));

    // The screen of the console (GUID_CONSOLE_DISPLAY_STATE) is another setting: over a remote
    // desktop it is off while the user looks at the session.
    [Fact]
    public void ReadIsOff_AnotherSettingOrTooShort_IsNotKnown()
    {
        Assert.Null(SystemDisplayEvents.ReadIsOff(Setting(OtherSetting, 0)));
        Assert.Null(SystemDisplayEvents.ReadIsOff(Setting(ConsoleDisplayState, 0)));
        Assert.Null(SystemDisplayEvents.ReadIsOff(Setting(SystemDisplayEvents.SessionDisplayStatus, 0)[..^1]));
        Assert.Null(SystemDisplayEvents.ReadIsOff(Setting(SystemDisplayEvents.SessionDisplayStatus, 0, dataLength: 0)));
        Assert.Null(SystemDisplayEvents.ReadIsOff([]));
    }

    // Sent here on the own thread, the message goes straight through WndProc: Switched follows the
    // screen, dimmed counts as on, a state that stays the same is told again, and another setting
    // changes nothing. The screen of the console going off, as over a remote desktop, is what the
    // engine followed before; now it is another setting too, all the way from WndProc to Switched.
    [Fact]
    public void WndProc_PowerSettingChange_FollowsTheScreen()
    {
        SystemDisplayEvents? display = null;
        try
        {
            display = CreateOrSkip(new FakeSessionEvents());
            Send(display, SystemDisplayEvents.SessionDisplayStatus, 1);
            Assert.False(display.IsOff);
            var changes = new List<bool>();
            display.Switched += (_, off) => changes.Add(off);

            Send(display, ConsoleDisplayState, 0);
            Assert.False(display.IsOff);
            Assert.Empty(changes);

            Send(display, SystemDisplayEvents.SessionDisplayStatus, 0);
            Assert.True(display.IsOff);
            Send(display, SystemDisplayEvents.SessionDisplayStatus, 0);
            Send(display, OtherSetting, 1);
            Send(display, SystemDisplayEvents.SessionDisplayStatus, 2);
            Send(display, SystemDisplayEvents.SessionDisplayStatus, 1);
            Send(display, SystemDisplayEvents.SessionDisplayStatus, 0);

            Assert.Equal([true, true, false, false, true], changes);
            Assert.True(display.IsOff);
        }
        finally
        {
            display?.Dispose();
        }
    }

    // Unlocking ends the pause of a screen that went off without coming on again; the next "off"
    // from Windows must pause again, though the screen was off for SystemDisplayEvents all along.
    [Fact]
    public void OffUnlockOff_PausesAgainInAutoPause()
    {
        SystemDisplayEvents? display = null;
        try
        {
            var session = new FakeSessionEvents();
            display = CreateOrSkip(session);
            var pause = PauseReasons.None;
            using var autoPause = new AutoPause(
                new FakePowerEvents(),
                session,
                display,
                new FakeScreenSaverEvents(),
                (reason, paused) => pause = paused ? pause | reason : pause & ~reason);

            Send(display, SystemDisplayEvents.SessionDisplayStatus, 0);
            session.Raise(SessionSwitchReason.SessionUnlock);
            Assert.Equal(PauseReasons.None, pause);

            Send(display, SystemDisplayEvents.SessionDisplayStatus, 0);
            Assert.Equal(PauseReasons.DisplayOff, pause);
        }
        finally
        {
            display?.Dispose();
        }
    }

    // Registering and unregistering with Windows itself. Only its own warnings count: other tests
    // write about the screen saver at the same time.
    [Fact]
    public void CreateAndDispose_RegisterAndUnregisterWithoutWarnings()
    {
        using var trace = new TraceCapture();

        CreateOrSkip(new FakeSessionEvents()).Dispose();

        Assert.DoesNotContain(trace.Messages, message =>
            message.Contains("Whether the screen is off cannot be followed", StringComparison.Ordinal)
            || message.Contains("The screen state could not be unregistered", StringComparison.Ordinal));
    }

    // When the session moves to another screen, the registration is made again, so Windows sends
    // the state of that screen once more; the log says so. Only connecting counts: locking,
    // unlocking and disconnecting leave the registration alone.
    [Theory]
    [InlineData(SessionSwitchReason.ConsoleConnect, true)]
    [InlineData(SessionSwitchReason.RemoteConnect, true)]
    [InlineData(SessionSwitchReason.SessionLock, false)]
    [InlineData(SessionSwitchReason.SessionUnlock, false)]
    [InlineData(SessionSwitchReason.ConsoleDisconnect, false)]
    [InlineData(SessionSwitchReason.RemoteDisconnect, false)]
    public void SessionSwitch_OnlyConnectingAsksWindowsAgain(SessionSwitchReason reason, bool asksAgain)
    {
        using var trace = new TraceCapture();
        SystemDisplayEvents? display = null;
        try
        {
            var session = new FakeSessionEvents();
            display = CreateOrSkip(session);

            session.Raise(reason);

            Assert.Equal(asksAgain, trace.Messages.Any(message => message.Contains(AskingAgain, StringComparison.Ordinal)));
            Assert.DoesNotContain(trace.Messages, message =>
                message.Contains("Whether the screen is off cannot be followed", StringComparison.Ordinal)
                || message.Contains("The screen state could not be unregistered", StringComparison.Ordinal));
        }
        finally
        {
            display?.Dispose();
        }
    }

    // With Windows itself: it sends the state through the message queue once after registering,
    // and so once more after the session connected. Pumped here on the thread of the test; a
    // session in which Windows sends nothing after the first registration (no screen) is skipped.
    [Fact]
    public void SessionConnect_WindowsTellsTheStateOnceMore()
    {
        SystemDisplayEvents? display = null;
        try
        {
            var session = new FakeSessionEvents();
            display = CreateOrSkip(session);
            var states = new List<bool>();
            display.Switched += (_, off) => states.Add(off);
            if (!PumpUntil(() => states.Count > 0))
            {
                Assert.Skip("Windows sends no screen state in this session.");
            }

            states.Clear();
            session.Raise(SessionSwitchReason.RemoteConnect);

            Assert.True(PumpUntil(() => states.Count > 0), "Windows did not send the screen state again after the session connected.");
        }
        finally
        {
            display?.Dispose();
        }
    }

    [Fact]
    public void Dispose_StopsListeningToTheSession()
    {
        var session = new FakeSessionEvents();
        CreateOrSkip(session).Dispose();

        Assert.False(session.HasListeners);
    }

    // A POWERBROADCAST_SETTING: the GUID, DataLength and a DWORD.
    private static byte[] Setting(Guid setting, int state, int dataLength = sizeof(int))
    {
        var bytes = new byte[24];
        setting.TryWriteBytes(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), dataLength);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), state);
        return bytes;
    }

    private static void Send(SystemDisplayEvents display, Guid setting, int state)
    {
        var bytes = Setting(setting, state);
        var memory = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, memory, bytes.Length);
            MessageOnlyWindows.SendMessage(display.Handle, WmPowerBroadcast, PbtPowerSettingChange, memory);
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    // Pumps the messages of this thread until done, or false after StateTimeout.
    private static bool PumpUntil(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            if (watch.Elapsed > StateTimeout)
            {
                return false;
            }

            Application.DoEvents();
            Thread.Sleep(10);
        }

        return true;
    }

    private static SystemDisplayEvents CreateOrSkip(ISessionEvents session) =>
        MessageOnlyWindows.CreateOrSkip(() => new SystemDisplayEvents(session), NoWindow);
}
