namespace BuurmansRgb.UI.Services;

// The clipboard of Windows, for "Code kopiëren" and "Code plakken"; tests give one in memory.
public interface IClipboard
{
    // False when Windows did not let the text on, such as while another program holds the clipboard.
    bool TrySetText(string text);

    // The text on the clipboard; null without text, or when it could not be read.
    string? GetText();
}

// A clipboard of the app alone, for a MainViewModel without the one of Windows, such as in a test:
// what "Code kopiëren" puts on it, "Code plakken" reads, and no other program sees it.
public sealed class AppClipboard : IClipboard
{
    public string? Text { get; set; }

    public bool TrySetText(string text)
    {
        Text = text;
        return true;
    }

    public string? GetText() => Text;
}
