using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace BuurmansRgb.UI.Services;

// The clipboard of WPF, on the UI thread. Another program that holds the clipboard at that moment
// makes WPF throw; the code then says it did not work instead of the app falling over.
public sealed class SystemClipboard : IClipboard
{
    public static SystemClipboard Instance { get; } = new();

    public bool TrySetText(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (ExternalException exception)
        {
            Trace.TraceWarning($"Could not put the code on the clipboard: {exception.Message}");
            return false;
        }
    }

    public string? GetText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (ExternalException exception)
        {
            Trace.TraceWarning($"Could not read the clipboard: {exception.Message}");
            return null;
        }
    }
}
