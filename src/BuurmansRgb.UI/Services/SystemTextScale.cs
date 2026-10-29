using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace BuurmansRgb.UI.Services;

// The text size of Windows, as the settings store it: TextScaleFactor under
// HKCU\Software\Microsoft\Accessibility, a percentage from 100 to 225, or nothing at 100 %. That is
// what UISettings.TextScaleFactor of WinRT reads too, but WinRT would need a Windows target framework
// and its projection (some MB more for the app and the installer), and WPF has no event for it.
// This only reads the key: Windows tells when a value under it is set (RegNotifyChangeKeyValue), on
// a thread of the pool, and TextScaleMessages turns that into a Changed on the UI thread once the
// factor really changed. The key exists in every Windows profile; without it the factor stays 1.0
// until the app starts again.
internal sealed partial class SystemTextScale : ITextScale, IDisposable
{
    private const string KeyPath = @"Software\Microsoft\Accessibility";
    private const string ValueName = "TextScaleFactor";

    // REG_NOTIFY_CHANGE_LAST_SET: a value is set or removed. REG_NOTIFY_THREAD_AGNOSTIC: the
    // notification stays when the thread of the pool that asked for it ends.
    private const uint NotifyChangeLastSet = 0x4;
    private const uint NotifyThreadAgnostic = 0x10000000;

    private readonly RegistryKey? _key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
    private readonly AutoResetEvent _signal = new(false);
    private readonly TextScaleMessages _messages;
    private readonly RegisteredWaitHandle? _wait;
    private bool _disposed;

    public SystemTextScale()
    {
        // First the watch, then the read: a change in between then still gives a signal.
        var watching = _key is not null && Watch();
        _messages = new TextScaleMessages(Dispatcher.CurrentDispatcher, () => _key?.GetValue(ValueName));
        _messages.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        if (watching)
        {
            _wait = ThreadPool.RegisterWaitForSingleObject(_signal, OnSignaled, null, Timeout.Infinite, executeOnlyOnce: false);
        }
    }

    public event EventHandler? Changed;

    public double Factor => _messages.Factor;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messages.Dispose();
        if (_wait is not null)
        {
            // Waits for a callback that runs, which still uses the key.
            using var done = new ManualResetEvent(false);
            if (_wait.Unregister(done))
            {
                done.WaitOne();
            }
        }

        _key?.Dispose();
        _signal.Dispose();
    }

    // Asks Windows once to set _signal at the next change of a value of the key.
    private bool Watch()
    {
        var error = RegNotifyChangeKeyValue(_key!.Handle, watchSubtree: false, NotifyChangeLastSet | NotifyThreadAgnostic, _signal.SafeWaitHandle, asynchronous: true);
        if (error != 0)
        {
            Trace.TraceWarning($"Could not watch the text size of Windows (error {error}); a next change follows once the app starts again.");
        }

        return error == 0;
    }

    private void OnSignaled(object? state, bool timedOut)
    {
        Watch();
        _messages.OnSignaled();
    }

    [LibraryImport("advapi32.dll")]
    private static partial int RegNotifyChangeKeyValue(SafeRegistryHandle key, [MarshalAs(UnmanagedType.Bool)] bool watchSubtree, uint notifyFilter, SafeWaitHandle eventHandle, [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
}
