namespace BuurmansRgb.UI.Services;

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
