using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace BuurmansRgb.UI.Views;

// The frame Windows puts around a window: the sizing border plus the padding Windows 10 and 11 add
// to it. A maximized window has it past the screen edges, which with our own title bar would cut
// off that much of the content. Read when asked, so a changed setting or monitor counts.
internal static partial class WindowFrame
{
    private const int SizeFrameWidth = 32;
    private const int SizeFrameHeight = 33;
    private const int PaddedBorderWidth = 92;

    public static Thickness Of(Visual window)
    {
        var dpi = VisualTreeHelper.GetDpi(window);
        var padding = GetSystemMetrics(PaddedBorderWidth);
        var x = (GetSystemMetrics(SizeFrameWidth) + padding) / dpi.DpiScaleX;
        var y = (GetSystemMetrics(SizeFrameHeight) + padding) / dpi.DpiScaleY;
        return new Thickness(x, y, x, y);
    }

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);
}
