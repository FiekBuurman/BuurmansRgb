using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.Tests;

// The keys of the per-LED editor in the view of a zone; what they do to the active LED and the
// selection is in DevicePreviewKeyboardTests.
public sealed class LedPreviewViewKeyboardTests
{
    public static TheoryData<Key, ModifierKeys, LedKey, bool> Keys => new()
    {
        { Key.Left, ModifierKeys.None, LedKey.Previous, false },
        { Key.Right, ModifierKeys.Shift, LedKey.Next, true },
        { Key.Up, ModifierKeys.None, LedKey.Up, false },
        { Key.Down, ModifierKeys.Shift, LedKey.Down, true },
        { Key.Home, ModifierKeys.None, LedKey.First, false },
        { Key.End, ModifierKeys.Shift, LedKey.Last, true },
        { Key.Space, ModifierKeys.None, LedKey.Select, false },
        { Key.Enter, ModifierKeys.None, LedKey.Select, false },
        { Key.Enter, ModifierKeys.Shift, LedKey.Select, true },
        { Key.A, ModifierKeys.Control, LedKey.SelectAll, false },
        { Key.Escape, ModifierKeys.None, LedKey.ClearSelection, false },
    };

    [Theory]
    [MemberData(nameof(Keys))]
    public void KeyPressOf_KeysOfThePreview(Key key, ModifierKeys modifiers, LedKey expected, bool extend) =>
        Assert.Equal(new LedKeyPress(expected, extend), LedPreviewView.KeyPressOf(key, modifiers));

    // Tab, letters and keys with Ctrl or Alt stay with the window, such as Ctrl+Tab.
    [Theory]
    [InlineData(Key.Tab, ModifierKeys.None)]
    [InlineData(Key.A, ModifierKeys.None)]
    [InlineData(Key.Right, ModifierKeys.Control)]
    [InlineData(Key.Left, ModifierKeys.Alt)]
    [InlineData(Key.A, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.Escape, ModifierKeys.Shift)]
    public void KeyPressOf_OtherKeys_IsNull(Key key, ModifierKeys modifiers) =>
        Assert.Null(LedPreviewView.KeyPressOf(key, modifiers));

    // Six dots in rows of three: LED 1 has LED 4 below it and no row above.
    [Fact]
    public void PressKey_UpAndDownInRows_GiveTheLedInTheNextRow() => Sta.Run(() =>
    {
        List<LedKeyPress> presses = [];
        var view = Arranged(new LedPreviewView { Colors = new RgbColor[6], ActiveLed = 1, KeyCommand = Recording(presses) }, width: 3 * LedLayout.Pitch);

        Assert.True(view.PressKey(Key.Down, ModifierKeys.None));
        Assert.True(view.PressKey(Key.Up, ModifierKeys.Shift));

        Assert.Equal([new LedKeyPress(LedKey.Down, Target: 4), new LedKeyPress(LedKey.Up, Extend: true)], presses);
    });

    // The space bar reaches below A, B and C, so all of them go down to it, and up from it is the
    // key above its middle.
    [Fact]
    public void PressKey_UpAndDownOnAKeyMap_FollowWideKeys() => Sta.Run(() =>
    {
        List<LedKeyPress> presses = [];
        var view = Arranged(new LedPreviewView
        {
            Colors = new RgbColor[4],
            Matrix = new LedMatrix(2, 3, [0, 1, 2, LedMatrix.NoLed, 3, LedMatrix.NoLed]),
            LedNames = ["A", "B", "C", "Space"],
            KeyCommand = Recording(presses),
        });

        view.ActiveLed = 2;
        view.PressKey(Key.Down, ModifierKeys.None);
        view.ActiveLed = 3;
        view.PressKey(Key.Up, ModifierKeys.None);
        view.PressKey(Key.Down, ModifierKeys.None);

        Assert.Equal([3, 1, LedMatrix.NoLed], presses.Select(press => press.Target));
    });

    [Fact]
    public void PressKey_UpAndDownInARing_HaveNoRow() => Sta.Run(() =>
    {
        List<LedKeyPress> presses = [];
        var view = Arranged(new LedPreviewView { Colors = new RgbColor[8], IsRing = true, ActiveLed = 0, KeyCommand = Recording(presses) });

        view.PressKey(Key.Down, ModifierKeys.None);

        Assert.Equal(LedMatrix.NoLed, Assert.Single(presses).Target);
    });

    // The key then goes on to the window, such as Escape without a selection.
    [Fact]
    public void PressKey_TheCommandDoesNotTake_IsNotHandled() => Sta.Run(() =>
    {
        var view = Arranged(new LedPreviewView { Colors = new RgbColor[3], ActiveLed = 0 });
        Assert.False(view.PressKey(Key.Right, ModifierKeys.None));

        view.KeyCommand = new RelayCommand<LedKeyPress>(_ => { }, press => press!.Key != LedKey.ClearSelection);

        Assert.True(view.PressKey(Key.Right, ModifierKeys.None));
        Assert.False(view.PressKey(Key.Escape, ModifierKeys.None));
        Assert.False(view.PressKey(Key.Tab, ModifierKeys.None));
    });

    // Tab stops once per device: at the zone with the active LED.
    [Fact]
    public void ActiveLed_MakesTheViewATabStop() => Sta.Run(() =>
    {
        var view = new LedPreviewView { Colors = new RgbColor[3] };
        Assert.True(view.Focusable);
        Assert.False(KeyboardNavigation.GetIsTabStop(view));

        view.ActiveLed = 2;
        Assert.True(KeyboardNavigation.GetIsTabStop(view));

        view.ActiveLed = LedMatrix.NoLed;
        Assert.False(KeyboardNavigation.GetIsTabStop(view));
    });

    // Two zones of one device: the focus goes along, whichever zone hears of the move first.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ActiveLedToAnotherViewOfTheGroup_TakesTheFocus(bool leftHearsFirst) => OffscreenWindow.Run(panel =>
    {
        var left = Added(panel, new LedPreviewView { Colors = new RgbColor[3], ActiveLed = 2, KeyGroup = "board" });
        var right = Added(panel, new LedPreviewView { Colors = new RgbColor[3], KeyGroup = "board" });
        OffscreenWindow.WaitUntilIdle();
        Assert.True(left.Focus());

        if (leftHearsFirst)
        {
            left.ActiveLed = LedMatrix.NoLed;
            right.ActiveLed = 0;
        }
        else
        {
            right.ActiveLed = 0;
            left.ActiveLed = LedMatrix.NoLed;
        }

        Assert.True(right.IsKeyboardFocused);
    });

    // Another device, or views without a group, keep their active LED to themselves.
    [Theory]
    [InlineData("fan")]
    [InlineData(null)]
    public void ActiveLedOutsideTheGroup_LeavesTheFocus(string? otherGroup) => OffscreenWindow.Run(panel =>
    {
        var focused = Added(panel, new LedPreviewView { Colors = new RgbColor[3], ActiveLed = 2, KeyGroup = otherGroup is null ? null : "board" });
        var other = Added(panel, new LedPreviewView { Colors = new RgbColor[3], KeyGroup = otherGroup });
        OffscreenWindow.WaitUntilIdle();
        Assert.True(focused.Focus());

        focused.ActiveLed = LedMatrix.NoLed;
        other.ActiveLed = 0;

        Assert.True(focused.IsKeyboardFocused);
    });

    // In "Hele huis", with the focus in the LEDs of one lamp, a click on an LED of another lamp
    // brings the focus along, so the keys go on where the selection is. Only moving the active LED
    // of the other lamp, as when the preview is made again, leaves the focus.
    [Fact]
    public void ClickInAnotherDevice_WithTheFocusInTheLeds_TakesTheFocus() => OffscreenWindow.Run(panel =>
    {
        var focused = Added(panel, new LedPreviewView { Colors = new RgbColor[3], ActiveLed = 1 });
        List<LedHit> clicks = [];
        var other = Added(panel, new LedPreviewView { Colors = new RgbColor[3], ActiveLed = 0 });
        other.ClickCommand = new RelayCommand<LedHit>(hit =>
        {
            clicks.Add(hit!);
            other.ActiveLed = hit!.Index;
        });
        OffscreenWindow.WaitUntilIdle();
        Assert.True(focused.Focus());

        other.ActiveLed = 1;
        Assert.True(focused.IsKeyboardFocused);

        Click(other, 2);

        Assert.Equal([new LedHit(2, Extend: false)], clicks);
        Assert.True(other.IsKeyboardFocused);
        Assert.Equal(2, other.ActiveLed);
    });

    // With the mouse alone the focus stays where it is, such as on a button.
    [Fact]
    public void Click_WithoutTheFocusInTheLeds_LeavesTheFocus() => OffscreenWindow.Run(panel =>
    {
        var button = new System.Windows.Controls.Button { Content = "Button" };
        panel.Children.Add(button);
        List<LedHit> clicks = [];
        var view = Added(panel, new LedPreviewView { Colors = new RgbColor[3], ActiveLed = 0, ClickCommand = new RelayCommand<LedHit>(hit => clicks.Add(hit!)) });
        OffscreenWindow.WaitUntilIdle();
        Assert.True(button.Focus());

        Click(view, 1);

        Assert.Single(clicks);
        Assert.True(button.IsKeyboardFocused);
        Assert.False(view.IsKeyboardFocused);
    });

    // A screen reader hears the active LED as the item with the focus, with its name and
    // whether it is selected.
    [Fact]
    public void AutomationPeer_ActiveLedHasTheFocus() => OffscreenWindow.Run(panel =>
    {
        var view = Added(panel, new LedPreviewView
        {
            Colors = new RgbColor[3], LedLabels = ["Escape (LED 1)", "LED 2", "F1 (LED 3)"], ActiveLed = 1, SelectionFirst = 1, SelectionLast = 1,
        });
        OffscreenWindow.WaitUntilIdle();
        var leds = UIElementAutomationPeer.CreatePeerForElement(view).GetChildren();
        Assert.Equal([false, true, false], leds.Select(led => led.IsKeyboardFocusable()));
        Assert.All(leds, led => Assert.False(led.HasKeyboardFocus()));

        leds[1].SetFocus();

        Assert.True(view.IsKeyboardFocused);
        Assert.Equal([false, true, false], leds.Select(led => led.HasKeyboardFocus()));
        Assert.Equal("LED 2", leds[1].GetName());
        Assert.Throws<InvalidOperationException>(() => leds[0].SetFocus());
    });

    // The item of the active LED tells a screen reader that it has the focus, when the view gets
    // the focus and on every move after that; without the focus it says nothing.
    [Fact]
    public void ShowActiveLed_RaisesFocusChangedForTheItemOfTheActiveLed() => OffscreenWindow.Run(panel =>
    {
        var view = Added(panel, new LedPreviewView { Colors = new RgbColor[3], LedLabels = ["LED 1", "LED 2", "LED 3"], ActiveLed = 1 });
        OffscreenWindow.WaitUntilIdle();
        var peer = Assert.IsType<LedPreviewAutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(view));
        List<(string Name, AutomationEvents Event)> events = [];
        peer.RaiseLedEvent = (item, automationEvent) => events.Add((item.GetName(), automationEvent));

        view.ActiveLed = 0;
        OffscreenWindow.WaitUntilIdle();
        Assert.Empty(events);

        Assert.True(view.Focus());
        OffscreenWindow.WaitUntilIdle();
        view.ActiveLed = 2;

        Assert.Equal([("LED 1", AutomationEvents.AutomationFocusChanged), ("LED 3", AutomationEvents.AutomationFocusChanged)], events);
    });

    // With the focus the tooltip names the active LED; it follows the mouse and comes back to the
    // active LED when the mouse leaves. Without the focus it goes away with the mouse.
    [Fact]
    public void MouseLeave_WithTheFocus_GivesTheToolTipBackToTheActiveLed() => OffscreenWindow.Run(panel =>
    {
        var view = Added(panel, new LedPreviewView { Colors = new RgbColor[3], LedLabels = ["LED 1", "LED 2", "LED 3"], ActiveLed = 1 });
        OffscreenWindow.WaitUntilIdle();
        Assert.True(view.Focus());
        OffscreenWindow.WaitUntilIdle();
        Assert.Equal(1, view.ToolTipLed);

        MoveMouse(view, 2);
        Assert.Equal(2, view.ToolTipLed);

        view.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseLeaveEvent });
        Assert.Equal(1, view.ToolTipLed);

        Keyboard.ClearFocus();
        MoveMouse(view, 2);
        view.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseLeaveEvent });
        Assert.Equal(LedMatrix.NoLed, view.ToolTipLed);
    });

    // The ring around the active LED only shows while the view has the keyboard focus: 2 px,
    // around the LED and drawn after all of them.
    [Fact]
    public void Render_WithTheFocus_DrawsARingAroundTheActiveLed() => OffscreenWindow.Run(panel =>
    {
        var view = Added(panel, new LedPreviewView { Colors = new RgbColor[3], ActiveLed = 1 });
        OffscreenWindow.WaitUntilIdle();
        Assert.Equal(3, Drawings(view).Count);

        Assert.True(view.Focus());

        var drawings = Drawings(view);
        Assert.Equal(4, drawings.Count);
        var ring = Assert.IsType<GeometryDrawing>(drawings[^1]);
        Assert.Null(ring.Brush);
        Assert.Equal(2.0, ring.Pen.Thickness);
        Assert.True(ring.Geometry.Bounds.Contains(view.BoundsOf(1)));
        Assert.False(ring.Geometry.Bounds.Contains(view.BoundsOf(0)));

        view.ActiveLed = LedMatrix.NoLed;
        Assert.Equal(3, Drawings(view).Count);
    });

    private static RelayCommand<LedKeyPress> Recording(List<LedKeyPress> presses) => new(press => presses.Add(press!));

    // A left click as Windows gives it, in the middle of an LED.
    private static void Click(LedPreviewView view, int led)
    {
        PutMouseOn(view, led);
        view.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });
    }

    private static void MoveMouse(LedPreviewView view, int led)
    {
        PutMouseOn(view, led);
        view.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseMoveEvent });
    }

    // The view gets the place of the mouse from the test, as a test cannot move the mouse.
    private static void PutMouseOn(LedPreviewView view, int led)
    {
        var bounds = view.BoundsOf(led);
        view.MousePosition = _ => new Point(bounds.X + bounds.Width / 2.0, bounds.Y + bounds.Height / 2.0);
    }

    private static LedPreviewView Arranged(LedPreviewView view, double width = 300.0)
    {
        view.Measure(new Size(width, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, width, view.DesiredSize.Height));
        return view;
    }

    private static LedPreviewView Added(System.Windows.Controls.Panel panel, LedPreviewView view)
    {
        panel.Children.Add(view);
        return view;
    }

    // What OnRender drew last, one drawing per LED and ring.
    private static List<GeometryDrawing> Drawings(LedPreviewView view) => LedDrawings.Of(view);
}
