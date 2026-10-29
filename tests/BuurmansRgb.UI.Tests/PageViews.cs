using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// For the tests that load a real page of the app in a window off screen (AppTheme.Run).
internal static class PageViews
{
    // The width of the navigation bar of MainWindow, beside the page.
    private const double NavigationWidth = 88;

    // The width a page gets in a window of windowWidth: beside the navigation bar, with room for
    // the scroll bar of the page. The smallest window is 960 px wide.
    internal static double PageWidth(double windowWidth) =>
        windowWidth - NavigationWidth - SystemParameters.VerticalScrollBarWidth;

    // A view model connected to a fake engine, which arrange prepares before the view model loads
    // from it. "Kies uit wat nu draait" lists programs, or nothing, but never what runs on this pc,
    // and what Instellingen opens (the log, the guide at guideAddress) goes into opened instead of
    // opening on this pc. animationEffects is "Animatie-effecten" of Windows, on without it.
    internal static async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null, IReadOnlyList<RunningProgram>? programs = null, List<string>? opened = null, string? guideAddress = AppInfoViewModel.GuideAddress, IAnimationEffects? animationEffects = null)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(new FakeEngineConnector(), api => arrange?.Invoke(api)), findPrograms: () => Task.FromResult(programs ?? []), appInfo: AppInfoViewModel.ForThisApp(target => opened?.Add(target), guideAddress), animationEffects: animationEffects);
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }

    internal static TPage Show<TPage>(StackPanel panel, MainViewModel viewModel)
        where TPage : FrameworkElement, new()
    {
        var page = new TPage { DataContext = viewModel };
        panel.Children.Add(page);
        OffscreenWindow.WaitUntilIdle();
        return page;
    }

    // As a screen reader or a click would: only an enabled button does something.
    internal static void Click(Button button)
    {
        var invoke = (IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke);
        invoke.Invoke();
        OffscreenWindow.WaitUntilIdle();
    }

    // As the keyboard would, to the element that has the focus: KeyDown, or the routed event given,
    // such as PreviewKeyDown for a key the element takes before its template does.
    internal static void Press(UIElement element, Key key, RoutedEvent? routedEvent = null)
    {
        element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(element), 0, key) { RoutedEvent = routedEvent ?? Keyboard.KeyDownEvent });
        OffscreenWindow.WaitUntilIdle();
    }

    internal static Button ButtonNamed(DependencyObject parent, string name) => Named<Button>(parent, name);

    // Every element UI Automation finds in the page, as a screen reader walks it.
    internal static List<AutomationPeer> AutomationTree(FrameworkElement page) =>
        [.. Descendants(UIElementAutomationPeer.CreatePeerForElement(page))];

    // The one T with this name for a screen reader.
    internal static T Named<T>(DependencyObject parent, string name)
        where T : DependencyObject =>
        AppTheme.Descendants<T>(parent).Single(element => AutomationProperties.GetName(element) == name);

    private static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
    {
        yield return peer;
        foreach (var child in peer.GetChildren() ?? [])
        {
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
