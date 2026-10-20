using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// With a real window, off screen and not activated, so the tests do not take the focus of the desktop.
public sealed class FocusKeeperTests
{
    [Fact]
    public void ElementMadeAgain_GetsTheFocusBack() => InWindow(panel =>
    {
        var before = Keyed(panel, "plus");
        Assert.True(before.Focus());

        panel.Children.Remove(before);
        var after = Keyed(panel, "plus");
        WaitUntilIdle();

        Assert.True(after.IsKeyboardFocused);
    });

    // As in the main window: the key of a zone length plus the role of the control.
    [Fact]
    public void KeyFromABindingWithARole_GetsTheFocusBack() => InWindow(panel =>
    {
        var before = Bound(panel, "zone-length/board/2");
        Assert.True(before.Focus());
        Assert.Equal("zone-length/board/2/increase", FocusKeeper.GetKey(before));

        panel.Children.Remove(before);
        var after = Bound(panel, "zone-length/board/2");
        var otherZone = Bound(panel, "zone-length/board/3");
        WaitUntilIdle();

        Assert.True(after.IsKeyboardFocused);
        Assert.False(otherZone.IsKeyboardFocused);
    });

    [Fact]
    public void ElementWithAnotherKey_DoesNotGetTheFocus() => InWindow(panel =>
    {
        var before = Keyed(panel, "plus");
        before.Focus();

        panel.Children.Remove(before);
        var other = Keyed(panel, "minus");
        WaitUntilIdle();

        Assert.False(other.IsKeyboardFocused);
    });

    // The focus only comes back in the same pass, so it does not jump to a control made much later.
    [Fact]
    public void ElementMadeLater_DoesNotGetTheFocus() => InWindow(panel =>
    {
        var before = Keyed(panel, "plus");
        before.Focus();

        panel.Children.Remove(before);
        WaitUntilIdle();

        // The keeper forgets behind this wait, also at idle priority.
        WaitUntilIdle();
        var later = Keyed(panel, "plus");
        WaitUntilIdle();

        Assert.False(later.IsKeyboardFocused);
    });

    [Fact]
    public void ElementRemovedWithoutTheFocus_GivesNothingAway() => InWindow(panel =>
    {
        var focused = Keyed(panel, "minus");
        var before = Keyed(panel, "plus");
        focused.Focus();

        panel.Children.Remove(before);
        var after = Keyed(panel, "plus");
        WaitUntilIdle();

        Assert.True(focused.IsKeyboardFocused);
        Assert.False(after.IsKeyboardFocused);
    });

    private static Button Keyed(Panel panel, string key)
    {
        var button = new Button { Content = key };
        FocusKeeper.SetKey(button, key);
        panel.Children.Add(button);
        WaitUntilIdle();
        return button;
    }

    private static Button Bound(Panel panel, string focusKey)
    {
        var button = new Button { Content = focusKey, DataContext = new { FocusKey = focusKey } };
        BindingOperations.SetBinding(button, FocusKeeper.KeyProperty, new Binding("FocusKey") { StringFormat = "{0}/increase" });
        panel.Children.Add(button);
        WaitUntilIdle();
        return button;
    }

    private static void WaitUntilIdle() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void InWindow(Action<StackPanel> test) => Sta.Run(() =>
    {
        var panel = new StackPanel();
        var window = new Window
        {
            Content = panel,
            Left = -10000,
            Top = -10000,
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
}
