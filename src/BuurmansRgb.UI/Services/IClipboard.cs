namespace BuurmansRgb.UI.Services;

// The clipboard of Windows, for "Code kopiëren" and "Code plakken"; tests give one in memory.
public interface IClipboard
{
    // False when Windows did not let the text on, such as while another program holds the clipboard.
    bool TrySetText(string text);

    // The text on the clipboard; null without text, or when it could not be read.
    string? GetText();
}
