using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using BuurmansRgb.UI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.Tests;

// A text box with a binding that writes only when asked, in a real window off screen, so it can have
// the focus; ColorPage (CustomEffectViewTests) and LampsPage (LampsPageViewTests) use it for real.
public sealed partial class CommitOnEnterTests
{
    [Fact]
    public void Enter_WritesTheText_AndTheBoxKeepsTheFocus() => OffscreenWindow.Run(panel =>
    {
        var (box, source) = Box(panel, enabled: true);
        box.Text = "21:30";

        PageViews.Press(box, Key.Enter);

        Assert.Equal("21:30", source.Text);
        Assert.True(box.IsKeyboardFocused);
    });

    // Without a cancel command, Escape is left to the box and the page.
    [Fact]
    public void Escape_WithoutCancelCommand_DoesNothing() => OffscreenWindow.Run(panel =>
    {
        var (box, source) = Box(panel, enabled: true);
        box.Text = "21:30";

        var escape = Press(box, Key.Escape);

        Assert.False(escape.Handled);
        Assert.Equal("21:30", box.Text);
        Assert.Equal("23:00", source.Text);
    });

    // Escape runs the command, which puts back what the view model has, and the box shows it.
    [Fact]
    public void Escape_RunsTheCancelCommand_AndShowsTheSourceAgain() => OffscreenWindow.Run(panel =>
    {
        var (box, source) = Box(panel, enabled: false);
        var cancelled = 0;
        CommitOnEnter.SetCancelCommand(box, new RelayCommand(() => cancelled++));
        box.Text = "21:30";

        var escape = Press(box, Key.Escape);

        Assert.True(escape.Handled);
        Assert.Equal(1, cancelled);
        Assert.Equal("23:00", box.Text);
        Assert.Equal("23:00", source.Text);
        Assert.True(box.IsKeyboardFocused);

        // A cancel command alone also gives Enter.
        box.Text = "07:00";
        PageViews.Press(box, Key.Enter);
        Assert.Equal("07:00", source.Text);
    });

    // A command that cannot run is not run; the box still shows the source again.
    [Fact]
    public void Escape_CancelCommandThatCannotRun_IsNotRun() => OffscreenWindow.Run(panel =>
    {
        var (box, source) = Box(panel, enabled: false);
        var cancelled = 0;
        CommitOnEnter.SetCancelCommand(box, new RelayCommand(() => cancelled++, () => false));
        box.Text = "21:30";

        PageViews.Press(box, Key.Escape);

        Assert.Equal(0, cancelled);
        Assert.Equal("23:00", box.Text);
    });

    // Turned off, Enter writes nothing until the box loses the focus.
    [Fact]
    public void Off_EnterWritesNothing() => OffscreenWindow.Run(panel =>
    {
        var (box, source) = Box(panel, enabled: true);
        CommitOnEnter.SetIsEnabled(box, false);
        box.Text = "21:30";

        var enter = Press(box, Key.Enter);

        Assert.False(enter.Handled);
        Assert.Equal("23:00", source.Text);
    });

    // A box with the focus, bound to a source with "23:00" that only takes the text when asked.
    private static (TextBox Box, TimeSource Source) Box(StackPanel panel, bool enabled)
    {
        var source = new TimeSource();
        var box = new TextBox { DataContext = source };
        box.SetBinding(TextBox.TextProperty, new Binding(nameof(TimeSource.Text)) { UpdateSourceTrigger = UpdateSourceTrigger.Explicit });
        CommitOnEnter.SetIsEnabled(box, enabled);
        panel.Children.Add(box);
        OffscreenWindow.WaitUntilIdle();
        Assert.True(box.Focus());
        return (box, source);
    }

    // As PageViews.Press, with the event to see whether it was handled.
    private static KeyEventArgs Press(TextBox box, Key key)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, System.Windows.PresentationSource.FromVisual(box), 0, key) { RoutedEvent = Keyboard.KeyDownEvent };
        box.RaiseEvent(args);
        OffscreenWindow.WaitUntilIdle();
        return args;
    }

    private sealed partial class TimeSource : ObservableObject
    {
        [ObservableProperty]
        public partial string Text { get; set; } = "23:00";
    }
}
