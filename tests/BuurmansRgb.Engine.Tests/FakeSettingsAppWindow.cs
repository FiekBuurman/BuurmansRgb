using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine.Tests;

// The settings app, open or not.
internal sealed class FakeSettingsAppWindow : ISettingsAppWindow
{
    public bool IsOpen { get; set; }

    public bool CloseIfOpen()
    {
        var wasOpen = IsOpen;
        IsOpen = false;
        return wasOpen;
    }
}
