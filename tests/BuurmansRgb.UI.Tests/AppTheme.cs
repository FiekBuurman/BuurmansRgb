using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BuurmansRgb.UI.Tests;

// The XAML of a control of the app, such as ColorListEditor, looks up the styles of the theme while
// it loads, before it is in a window, so only the resources of the application can give them. A
// process has one application: it is made once, and holds the theme only while a test of this
// collection runs. The collection runs alone, so the implicit styles of the theme, such as those
// of Slider and LedBulb, do not reach the other tests.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AppThemeCollection
{
    public const string Name = "App theme";
}

internal static class AppTheme
{
    private static readonly Uri Theme = new("/BuurmansRgb.UI;component/Themes/Theme.xaml", UriKind.Relative);

    // An off-screen window, with the theme of the app for what loads in it.
    public static void Run(Action<StackPanel> test) => OffscreenWindow.Run(panel =>
    {
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources = (ResourceDictionary)Application.LoadComponent(Theme);
        try
        {
            test(panel);
        }
        finally
        {
            app.Resources = new ResourceDictionary();
        }
    });

    public static IEnumerable<T> Descendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
