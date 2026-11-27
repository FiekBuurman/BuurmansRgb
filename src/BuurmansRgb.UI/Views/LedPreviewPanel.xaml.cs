using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

public partial class LedPreviewPanel : UserControl
{
    // The room above a card after a jump, so its top does not touch the edge of the page.
    public const double JumpMargin = 12;

    // Where the keyboard focus goes when the last card goes while the focus was in it, such as the
    // choice "Welk apparaat?" at the top of Kleur.
    public static readonly DependencyProperty FallbackFocusProperty = DependencyProperty.Register(
        nameof(FallbackFocus), typeof(UIElement), typeof(LedPreviewPanel), new PropertyMetadata(null));

    private PreviewViewModel? _preview;

    // The device whose card had the keyboard focus, until the focus leaves the cards; kept when the
    // card goes with the focus in it. And the devices in the order the cards had then.
    private string? _focusedDevice;
    private string[] _order = [];
    private bool _restorePending;

    public LedPreviewPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Follow((DataContext as MainViewModel)?.Preview);
        Cards.PreviewGotKeyboardFocus += (_, e) => _focusedDevice = DeviceOf(e.NewFocus as DependencyObject)?.DeviceId ?? _focusedDevice;
        Cards.PreviewLostKeyboardFocus += OnCardsLostKeyboardFocus;
    }

    public UIElement? FallbackFocus
    {
        get => (UIElement?)GetValue(FallbackFocusProperty);
        set => SetValue(FallbackFocusProperty, value);
    }

    // A chip jumps to its device: the card opens if it was closed, comes at the top of the page and
    // gets the keyboard focus on its head (DESIGN.md § 9.4). The chip keeps the Tab stop of the row.
    public void JumpTo(DevicePreviewViewModel device)
    {
        device.IsExpanded = true;
        UpdateLayout();
        if (CardOf(device) is not { } card)
        {
            return;
        }

        ScrollToTop(card);
        card.FocusHeader();
    }

    // "Naar de apparaten" on Kleur (DESIGN.md § 9.2): "Per apparaat" comes at the top of the page and
    // the focus goes to its first Tab stop, such as "Alles openklappen", or Knipperen of the one card.
    public void JumpToDevices()
    {
        ScrollToTop(this);
        MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

    // The card of a device that opens and closes; null for one that is always open.
    public DeviceCard? CardOf(DevicePreviewViewModel device) =>
        Cards.ItemContainerGenerator.ContainerFromItem(device) is DependencyObject container
            ? VisualTreeSearch.FirstDescendant<DeviceCard>(container)
            : null;

    private void OnChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DevicePreviewViewModel device })
        {
            JumpTo(device);
        }
    }

    // BringIntoView scrolls just far enough, so a card below the view would end up at the bottom:
    // the top of the card, less a small margin, becomes the offset, as far as the page can scroll.
    private static void ScrollToTop(FrameworkElement card)
    {
        if (VisualTreeSearch.Ancestor<ScrollViewer>(card) is not { } scroller)
        {
            return;
        }

        var top = card.TransformToAncestor(scroller).Transform(default).Y + scroller.VerticalOffset;
        scroller.ScrollToVerticalOffset(Math.Clamp(top - JumpMargin, 0, scroller.ScrollableHeight));
        scroller.UpdateLayout();
    }

    private void Follow(PreviewViewModel? preview)
    {
        if (_preview is not null)
        {
            _preview.Devices.CollectionChanged -= OnDevicesChanged;
        }

        _preview = preview;
        _order = [.. preview?.Devices.Select(device => device.DeviceId) ?? []];
        if (preview is not null)
        {
            preview.Devices.CollectionChanged += OnDevicesChanged;
        }
    }

    // The focus left the cards for another place on the page; a card that went with the focus in it
    // is no longer in a window, and then its device is kept.
    private void OnCardsLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is DependencyObject next && Cards.IsAncestorOf(next))
        {
            return;
        }

        if (e.OldFocus is Visual old && PresentationSource.FromVisual(old) is not null && e.NewFocus is not null)
        {
            _focusedDevice = null;
        }
    }

    // The preview is made anew when the engine reports the devices again. FocusKeeper gives the focus
    // back to the same place when the device is still there; a device that stayed away hands it to
    // the head of the next card, or without one the card before it, or without cards FallbackFocus:
    // never to the window.
    private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_restorePending)
        {
            return;
        }

        _restorePending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, RestoreFocus);
    }

    private void RestoreFocus()
    {
        _restorePending = false;
        var order = _order;
        _order = [.. _preview?.Devices.Select(device => device.DeviceId) ?? []];
        if (_focusedDevice is not { } id || HasFocusInWindow())
        {
            return;
        }

        var devices = _preview?.Devices ?? [];
        var index = Array.IndexOf(order, id);
        var target = devices.FirstOrDefault(device => device.DeviceId == id)
            ?? order.Skip(index + 1).Select(next => devices.FirstOrDefault(device => device.DeviceId == next)).FirstOrDefault(device => device is not null)
            ?? order.Take(Math.Max(index, 0)).Reverse().Select(before => devices.FirstOrDefault(device => device.DeviceId == before)).FirstOrDefault(device => device is not null);
        _focusedDevice = null;
        if (target is not null && CardOf(target) is { } card && card.FocusHeader())
        {
            return;
        }

        if (target is not null && Cards.ItemContainerGenerator.ContainerFromItem(target) is DependencyObject container
            && VisualTreeSearch.FirstDescendant<Button>(container) is { } button && button.Focus())
        {
            return;
        }

        FallbackFocus?.Focus();
    }

    // The focus is on something of the window that is still there, other than the window itself,
    // such as the same place made anew (FocusKeeper). Until WPF notices, the focus can still be on an
    // element that was removed, whose ancestors still count it as theirs.
    private bool HasFocusInWindow() =>
        Keyboard.FocusedElement is Visual focused and not Window
        && PresentationSource.FromVisual(focused) is not null
        && Window.GetWindow(this) is { } window
        && window.IsAncestorOf(focused);

    private static DevicePreviewViewModel? DeviceOf(DependencyObject? element)
    {
        for (; element is Visual; element = VisualTreeHelper.GetParent(element))
        {
            if (element is FrameworkElement { DataContext: DevicePreviewViewModel device })
            {
                return device;
            }
        }

        return null;
    }
}
