using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The arrow keys of a row with one Tab stop (DeviceChipRow, EffectTileGroup): where a key takes the
// focus, and which control of the row holds it.
public sealed class RovingFocusTests
{
    // ← and → go round, Home and End to the first and the last; ↑ and ↓ go along only for a row
    // where they move (the effect tiles), and else stay (the chip row).
    [Theory]
    [InlineData(Key.Left, 0, false, 3)]
    [InlineData(Key.Left, 2, false, 1)]
    [InlineData(Key.Right, 2, false, 3)]
    [InlineData(Key.Right, 3, false, 0)]
    [InlineData(Key.Home, 2, false, 0)]
    [InlineData(Key.End, 1, false, 3)]
    [InlineData(Key.Up, 2, false, 2)]
    [InlineData(Key.Down, 2, false, 2)]
    [InlineData(Key.Up, 0, true, 3)]
    [InlineData(Key.Up, 2, true, 1)]
    [InlineData(Key.Down, 3, true, 0)]
    [InlineData(Key.Down, 1, true, 2)]
    [InlineData(Key.Left, 1, true, 0)]
    [InlineData(Key.Home, 3, true, 0)]
    [InlineData(Key.End, 0, true, 3)]
    public void NextIndex_GoesWhereTheKeyTakesIt(Key key, int current, bool upDownMoves, int next) =>
        Assert.Equal(next, RovingFocus.NextIndex(4, current, key, upDownMoves));

    [Theory]
    [InlineData(Key.Tab)]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    [InlineData(Key.A)]
    public void NextIndex_OtherKeys_MoveNothing(Key key)
    {
        Assert.Null(RovingFocus.NextIndex(4, 1, key, upDownMoves: false));
        Assert.Null(RovingFocus.NextIndex(4, 1, key, upDownMoves: true));
    }

    // One control: every arrow stays on it.
    [Theory]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    public void NextIndex_OneControl_StaysOnIt(Key key) =>
        Assert.Equal(0, RovingFocus.NextIndex(1, 0, key, upDownMoves: true));

    // The first control of each item in the order of the items, and the one that holds a source
    // inside it; -1 for a source outside the row.
    [Fact]
    public void Containers_TheControlsInTheOrderOfTheItems() => OffscreenWindow.Run(panel =>
    {
        var button = new FrameworkElementFactory(typeof(Button));
        button.SetBinding(ContentControl.ContentProperty, new Binding());
        var row = new ItemsControl
        {
            ItemsSource = new[] { "een", "twee", "drie" },
            ItemTemplate = new DataTemplate { VisualTree = button },
        };
        var outside = new TextBlock();
        panel.Children.Add(row);
        panel.Children.Add(outside);
        panel.UpdateLayout();

        var buttons = row.Containers<Button>();

        Assert.Equal(["een", "twee", "drie"], buttons.Select(control => control.Content));
        Assert.Equal(1, buttons.IndexHolding(buttons[1]));
        Assert.Equal(2, buttons.IndexHolding(AppTheme.Descendants<TextBlock>(buttons[2]).First()));
        Assert.Equal(-1, buttons.IndexHolding(outside));
    });

    // The control a key goes to; null with Ctrl held, from a source outside the row, or for a key
    // that is handled already.
    [Fact]
    public void TargetOf_TheControlTheKeyGoesTo() => OffscreenWindow.Run(panel =>
    {
        var button = new FrameworkElementFactory(typeof(Button));
        button.SetBinding(ContentControl.ContentProperty, new Binding());
        var row = new ItemsControl
        {
            ItemsSource = new[] { "een", "twee", "drie" },
            ItemTemplate = new DataTemplate { VisualTree = button },
        };
        var outside = new TextBlock();
        panel.Children.Add(row);
        panel.Children.Add(outside);
        panel.UpdateLayout();
        var buttons = row.Containers<Button>();

        KeyEventArgs Press(Key key, DependencyObject source) =>
            new(Keyboard.PrimaryDevice, PresentationSource.FromVisual(panel), 0, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
                Source = source,
            };
        Button? Target(KeyEventArgs e, ModifierKeys modifiers = ModifierKeys.None) =>
            row.TargetOf<Button>(e, modifiers, upDownMoves: false);

        Assert.Same(buttons[2], Target(Press(Key.Right, buttons[1])));
        Assert.Same(buttons[0], Target(Press(Key.Right, AppTheme.Descendants<TextBlock>(buttons[2]).First())));
        Assert.Null(Target(Press(Key.Right, buttons[1]), ModifierKeys.Control));
        Assert.Null(Target(Press(Key.Right, outside)));
        Assert.Null(Target(Press(Key.Tab, buttons[1])));
        var handled = Press(Key.Right, buttons[1]);
        handled.Handled = true;
        Assert.Null(Target(handled));
    });
}
