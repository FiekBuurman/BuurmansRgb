using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BuurmansRgb.UI.Tests;

// A real window for tests that need focus or a loaded template: off screen and not activated, so
// the tests do not take the focus of the desktop.
internal static class OffscreenWindow
{
    // Left and top of a window off screen, far from any screen.
    internal const double Offset = -10000;

    public static void Run(Action<StackPanel> test) => Sta.Run(() =>
    {
        var panel = new StackPanel();
        var window = new Window
        {
            Content = panel,
            Left = Offset,
            Top = Offset,
            Width = 200,
            Height = 200,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();
        try
        {
            test(panel);
        }
        finally
        {
            window.Close();
        }
    });

    // Lets WPF load, lay out and finish what it posted, such as Loaded and Unloaded.
    public static void WaitUntilIdle() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
