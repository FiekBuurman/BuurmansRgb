using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using BuurmansRgb.Engine.Power;

namespace BuurmansRgb.Engine.Tests;

public sealed class SystemDisplayEventsTests
{
    private const uint WmPowerBroadcast = 0x0218;
    private const nint PbtPowerSettingChange = 0x8013;
    private const string NoWindow = "This session cannot make a window.";

    private static readonly Guid OtherSetting = new("02731015-4510-4526-99e6-e5a17ebd1aea");

    // GUID_CONSOLE_DISPLAY_STATE gives 0 for off, 1 for on and 2 for dimmed.
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    public void ReadIsOff_OnlyOffIsOff(int state, bool off) =>
        Assert.Equal(off, SystemDisplayEvents.ReadIsOff(Setting(SystemDisplayEvents.ConsoleDisplayState, state)));

    [Fact]
    public void ReadIsOff_AnotherSettingOrTooShort_IsNotKnown()
    {
        Assert.Null(SystemDisplayEvents.ReadIsOff(Setting(OtherSetting, 0)));
        Assert.Null(SystemDisplayEvents.ReadIsOff(Setting(SystemDisplayEvents.ConsoleDisplayState, 0)[..^1]));
        Assert.Null(SystemDisplayEvents.ReadIsOff(Setting(SystemDisplayEvents.ConsoleDisplayState, 0, dataLength: 0)));
        Assert.Null(SystemDisplayEvents.ReadIsOff([]));
    }

    // Sent here on the own thread, the message goes straight through WndProc: Switched follows the
    // screen, dimmed counts as on, and a state that stays the same or another setting changes nothing.
    [Fact]
    public void WndProc_PowerSettingChange_FollowsTheScreen()
    {
        SystemDisplayEvents? display = null;
        try
        {
            display = CreateOrSkip();
            Send(display, SystemDisplayEvents.ConsoleDisplayState, 1);
            Assert.False(display.IsOff);
            var changes = new List<bool>();
            display.Switched += (_, off) => changes.Add(off);

            Send(display, SystemDisplayEvents.ConsoleDisplayState, 0);
            Assert.True(display.IsOff);
            Send(display, SystemDisplayEvents.ConsoleDisplayState, 0);
            Send(display, OtherSetting, 1);
            Send(display, SystemDisplayEvents.ConsoleDisplayState, 2);
            Send(display, SystemDisplayEvents.ConsoleDisplayState, 1);
            Send(display, SystemDisplayEvents.ConsoleDisplayState, 0);

            Assert.Equal([true, false, true], changes);
            Assert.True(display.IsOff);
        }
        finally
        {
            display?.Dispose();
        }
    }

    // Registering and unregistering with Windows itself.
    [Fact]
    public void CreateAndDispose_RegisterAndUnregisterWithoutWarnings()
    {
        using var trace = new TraceCapture();

        CreateOrSkip().Dispose();

        Assert.DoesNotContain(trace.Messages, message => message.Contains("screen", StringComparison.Ordinal));
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
            SendMessage(display.Handle, WmPowerBroadcast, PbtPowerSettingChange, memory);
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static SystemDisplayEvents CreateOrSkip()
    {
        try
        {
            return new SystemDisplayEvents();
        }
        catch (Win32Exception)
        {
            Assert.Skip(NoWindow);
            throw;
        }
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);
}
