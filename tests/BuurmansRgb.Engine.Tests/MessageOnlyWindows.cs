using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BuurmansRgb.Engine.Tests;

// For the tests of the real message-only windows, on the thread of the test.
internal static class MessageOnlyWindows
{
    // The window that create makes, or a skip with reason when this session cannot make one.
    public static T CreateOrSkip<T>(Func<T> create, string reason)
        where T : MessageOnlyWindow
    {
        try
        {
            return create();
        }
        catch (Win32Exception)
        {
            Assert.Skip(reason);
            throw;
        }
    }

    // Delivers the message to WndProc at once, on this thread.
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    public static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);
}
