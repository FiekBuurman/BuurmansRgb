using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The searches of the visual tree that ColorPage (the tiles), ScenesPage (the times) and LedPreviewPanel
// (the scroller of a card) share, in a real window off screen, so an element can have the focus.
public sealed class VisualTreeSearchTests
{
    // The nearest one around the element, not the element itself, and null when there is none.
    [Fact]
    public void Ancestor_TheNearestOneAround() => OffscreenWindow.Run(panel =>
    {
        var inner = new StackPanel();
        var button = new Button();
        inner.Children.Add(button);
        var outer = new Border { Child = inner };
        panel.Children.Add(outer);
        panel.UpdateLayout();

        Assert.Same(inner, VisualTreeSearch.Ancestor<StackPanel>(button));
        Assert.Same(panel, VisualTreeSearch.Ancestor<StackPanel>(inner));
        Assert.Same(outer, VisualTreeSearch.Ancestor<Border>(button));
        Assert.Null(VisualTreeSearch.Ancestor<ListBox>(button));
    });

    [Fact]
    public void FocusItemAt_FocusesTheItemAtIndex() => OffscreenWindow.Run(panel =>
    {
        var row = Row(panel, "een", "twee", "drie");

        Assert.True(row.FocusItemAt<Button>(1));

        Assert.Equal("twee", ((Button)Keyboard().FocusedElement).Content);
    });

    // A list that got shorter, such as after a removed tile: the last one takes the focus.
    [Fact]
    public void FocusItemAt_PastTheEnd_FocusesTheLast() => OffscreenWindow.Run(panel =>
    {
        var row = Row(panel, "een", "twee");

        Assert.True(row.FocusItemAt<Button>(5));

        Assert.Equal("twee", ((Button)Keyboard().FocusedElement).Content);
    });

    // Without items there is nothing to focus; the caller gives the focus elsewhere.
    [Fact]
    public void FocusItemAt_WithoutItems_IsFalse() => OffscreenWindow.Run(panel =>
    {
        var row = Row(panel);
        var other = new Button();
        panel.Children.Add(other);
        panel.UpdateLayout();
        Assert.True(other.Focus());

        Assert.False(row.FocusItemAt<Button>(0));

        Assert.True(other.IsKeyboardFocused);
    });

    // An item without the kind asked for is not focused either.
    [Fact]
    public void FocusItemAt_ItemWithoutTheElement_IsFalse() => OffscreenWindow.Run(panel =>
    {
        var row = Row(panel, "een");

        Assert.False(row.FocusItemAt<TextBox>(0));
    });

    private static System.Windows.Input.KeyboardDevice Keyboard() => System.Windows.Input.Keyboard.PrimaryDevice;

    // A row with a button per item, laid out, so its containers exist.
    private static ItemsControl Row(StackPanel panel, params string[] items)
    {
        var button = new FrameworkElementFactory(typeof(Button));
        button.SetBinding(ContentControl.ContentProperty, new Binding());
        var row = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = new DataTemplate { VisualTree = button },
        };
        panel.Children.Add(row);
        panel.UpdateLayout();
        return row;
    }
}
