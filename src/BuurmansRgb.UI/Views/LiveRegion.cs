using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Data;
using System.Windows.Threading;

namespace BuurmansRgb.UI.Views;

// AutomationProperties.LiveSetting only marks an element as a live region; WPF does not tell a
// screen reader when it changes. With AnnouncesUpdates on, the element raises LiveRegionChanged
// each time one of its bindings with NotifyOnTargetUpdated writes a new value, so the reader says
// the new text.
public static class LiveRegion
{
    public static readonly DependencyProperty AnnouncesUpdatesProperty = DependencyProperty.RegisterAttached(
        "AnnouncesUpdates", typeof(bool), typeof(LiveRegion), new PropertyMetadata(false, OnAnnouncesUpdatesChanged));

    public static bool GetAnnouncesUpdates(DependencyObject element) => (bool)element.GetValue(AnnouncesUpdatesProperty);

    public static void SetAnnouncesUpdates(DependencyObject element, bool value) => element.SetValue(AnnouncesUpdatesProperty, value);

    private static void OnAnnouncesUpdatesChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not FrameworkElement frameworkElement)
        {
            return;
        }

        frameworkElement.TargetUpdated -= OnTargetUpdated;
        if (e.NewValue is true)
        {
            frameworkElement.TargetUpdated += OnTargetUpdated;
        }
    }

    // Only while a screen reader or another client listens, so nothing is made for nobody, and not
    // for a hidden element, whose text no one sees. After the layout, so an element that shows
    // because of the same change (such as an error that appears) is visible by then.
    private static void OnTargetUpdated(object? sender, DataTransferEventArgs e)
    {
        if (sender is UIElement element)
        {
            element.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (element.IsVisible && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
                {
                    UIElementAutomationPeer.CreatePeerForElement(element)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                }
            });
        }
    }
}
