using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

// Draws the LEDs of a zone in one of three ways. Without a Matrix, such as for a strip, every LED
// is a dot, wrapping to the available width; with IsRing, such as for a fan, the dots form a
// ring. With a Matrix, such as the key map of a keyboard, every LED is a key at its own position,
// and keys that LedNames call wide, such as the space bar, reach into the empty cells beside them.
// A ring and a key map shrink when they are wider than the available width. One element per
// zone instead of one per LED keeps a 104-key keyboard cheap to redraw. A click on an LED runs
// ClickCommand with a LedHit; the LEDs SelectionFirst to SelectionLast get a selection ring.
// With the keyboard focus, a key runs KeyCommand with a LedKeyPress and ActiveLed gets a focus
// ring. Only a view that holds the active LED of its device is a Tab stop, so Tab stops once per
// device. A mouse click only takes the focus from the LEDs of another device, so the mouse alone
// works as before.
public sealed class LedPreviewView : FrameworkElement
{
    public static readonly DependencyProperty ColorsProperty = DependencyProperty.Register(
        nameof(Colors),
        typeof(IReadOnlyList<RgbColor>),
        typeof(LedPreviewView),
        new FrameworkPropertyMetadata(Array.Empty<RgbColor>(), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnColorsChanged));

    public static readonly DependencyProperty MatrixProperty = DependencyProperty.Register(
        nameof(Matrix),
        typeof(LedMatrix),
        typeof(LedPreviewView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsRingProperty = DependencyProperty.Register(
        nameof(IsRing), typeof(bool), typeof(LedPreviewView), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LedNamesProperty = DependencyProperty.Register(
        nameof(LedNames), typeof(IReadOnlyList<string>), typeof(LedPreviewView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LedLabelsProperty = DependencyProperty.Register(
        nameof(LedLabels), typeof(IReadOnlyList<string>), typeof(LedPreviewView), new PropertyMetadata(null));

    public static readonly DependencyProperty SelectionFirstProperty = DependencyProperty.Register(
        nameof(SelectionFirst), typeof(int), typeof(LedPreviewView), new FrameworkPropertyMetadata(LedSelection.NoSelection, FrameworkPropertyMetadataOptions.AffectsRender, OnSelectionFirstChanged));

    public static readonly DependencyProperty SelectionLastProperty = DependencyProperty.Register(
        nameof(SelectionLast), typeof(int), typeof(LedPreviewView), new FrameworkPropertyMetadata(LedSelection.NoSelection, FrameworkPropertyMetadataOptions.AffectsRender, OnSelectionLastChanged));

    public static readonly DependencyProperty ClickCommandProperty = DependencyProperty.Register(
        nameof(ClickCommand), typeof(ICommand), typeof(LedPreviewView), new PropertyMetadata(null, OnClickCommandChanged));

    public static readonly DependencyProperty ActiveLedProperty = DependencyProperty.Register(
        nameof(ActiveLed), typeof(int), typeof(LedPreviewView), new FrameworkPropertyMetadata(LedMatrix.NoLed, FrameworkPropertyMetadataOptions.AffectsRender, OnActiveLedChanged));

    public static readonly DependencyProperty KeyCommandProperty = DependencyProperty.Register(
        nameof(KeyCommand), typeof(ICommand), typeof(LedPreviewView), new PropertyMetadata(null));

    private const double KeyCornerRadius = 3.0;
    private const double SelectionGap = 1.5;

    // The middle of the focus ring, just outside the selection ring with a pixel between them.
    private const double FocusGap = 4.5;

    private static readonly Pen Outline = CreatePen(System.Windows.Media.Color.FromArgb(0x60, 0x80, 0x80, 0x80), 1.0);

    // In the system highlight color, so the ring also shows in dark and high-contrast themes. Read
    // on every render, so the ring follows a theme change from the next redraw on.
    private static Pen _selectionRing = CreatePen(SystemColors.HighlightColor, 2.0);

    // 2 px in the text color, as the focus ring of the other controls (design/DESIGN.md § 6).
    private static Pen? _focusRing;

    // The cells of a row or key map, holding only LEDs below Colors.Count, and the arrangement of
    // the LEDs. Both are made again only when the number of LEDs, the width, the matrix or the LED
    // names change, not on every frame.
    private LedMatrix _layout = LedLayout.Wrap(0, 1);
    private (int LedCount, int Columns, LedMatrix? Matrix, IReadOnlyList<string>? Names) _layoutFor;
    private ILedArrangement? _arrangement;

    // Made on the first hover, so a preview nobody points at has no tooltip and no timer.
    private LedToolTip? _toolTip;

    // The focus shows as the ring around the active LED (OnRender), not as one around the zone.
    public LedPreviewView()
    {
        Focusable = true;
        FocusVisualStyle = null;
        KeyboardNavigation.SetIsTabStop(this, false);
        Unloaded += (_, _) => _toolTip?.Hide();
    }

    public IReadOnlyList<RgbColor> Colors
    {
        get => (IReadOnlyList<RgbColor>)GetValue(ColorsProperty);
        set => SetValue(ColorsProperty, value);
    }

    public LedMatrix? Matrix
    {
        get => (LedMatrix?)GetValue(MatrixProperty);
        set => SetValue(MatrixProperty, value);
    }

    // The LEDs as a ring; the zone decides, see ZonePreviewViewModel.IsRing.
    public bool IsRing
    {
        get => (bool)GetValue(IsRingProperty);
        set => SetValue(IsRingProperty, value);
    }

    // The names of the LEDs, such as "Space"; with a Matrix they tell which keys are wide.
    public IReadOnlyList<string>? LedNames
    {
        get => (IReadOnlyList<string>?)GetValue(LedNamesProperty);
        set => SetValue(LedNamesProperty, value);
    }

    // What the tooltip and a screen reader call each LED; see ZonePreviewViewModel.LedLabels.
    public IReadOnlyList<string>? LedLabels
    {
        get => (IReadOnlyList<string>?)GetValue(LedLabelsProperty);
        set => SetValue(LedLabelsProperty, value);
    }

    public int SelectionFirst
    {
        get => (int)GetValue(SelectionFirstProperty);
        set => SetValue(SelectionFirstProperty, value);
    }

    public int SelectionLast
    {
        get => (int)GetValue(SelectionLastProperty);
        set => SetValue(SelectionLastProperty, value);
    }

    public ICommand? ClickCommand
    {
        get => (ICommand?)GetValue(ClickCommandProperty);
        set => SetValue(ClickCommandProperty, value);
    }

    // The LED the keys move and choose; NoLed while it is in another zone of the device.
    public int ActiveLed
    {
        get => (int)GetValue(ActiveLedProperty);
        set => SetValue(ActiveLedProperty, value);
    }

    // Runs with a LedKeyPress for each key of the preview; see KeyPressOf.
    public ICommand? KeyCommand
    {
        get => (ICommand?)GetValue(KeyCommandProperty);
        set => SetValue(KeyCommandProperty, value);
    }

    // Where the mouse is on the view; null for where Windows says it is. A test puts it on an LED
    // here, as it cannot move the mouse.
    internal Func<MouseEventArgs, Point>? MousePosition { get; set; }

    // What a key does in the preview: the arrows, Home and End move the active LED, with Shift
    // they extend the range, Space and Enter choose the active LED, Ctrl+A chooses all and Escape
    // none. null for any other key, which the window gets.
    internal static LedKeyPress? KeyPressOf(Key key, ModifierKeys modifiers)
    {
        if (modifiers == ModifierKeys.Control)
        {
            return key == Key.A ? new(LedKey.SelectAll) : null;
        }

        if (modifiers is not (ModifierKeys.None or ModifierKeys.Shift))
        {
            return null;
        }

        var extend = modifiers == ModifierKeys.Shift;
        return key switch
        {
            Key.Left => new(LedKey.Previous, extend),
            Key.Right => new(LedKey.Next, extend),
            Key.Up => new(LedKey.Up, extend),
            Key.Down => new(LedKey.Down, extend),
            Key.Home => new(LedKey.First, extend),
            Key.End => new(LedKey.Last, extend),
            Key.Space or Key.Enter => new(LedKey.Select, extend),
            Key.Escape when !extend => new(LedKey.ClearSelection),
            _ => null,
        };
    }

    // Runs KeyCommand for a key, with the LED in the row above or below as drawn for Up and Down.
    // False when it is no key of the preview or the command does not take it.
    internal bool PressKey(Key key, ModifierKeys modifiers)
    {
        if (KeyCommand is not { } command || KeyPressOf(key, modifiers) is not { } press)
        {
            return false;
        }

        if (press.Key is LedKey.Up or LedKey.Down)
        {
            press = press with { Target = ArrangementFor(ActualWidth).LedInNextRow(ActiveLed, up: press.Key == LedKey.Up) };
        }

        if (!command.CanExecute(press))
        {
            return false;
        }

        command.Execute(press);
        return true;
    }

    // The label of an LED from LedLabels; null for no LED or when there are no labels.
    internal string? LabelOf(int led) => LedLabels is { } labels && led >= 0 && led < labels.Count ? labels[led] : null;

    internal bool IsSelected(int led) => led >= SelectionFirst && led <= SelectionLast;

    // Where an LED is drawn, from the top left of the view; Rect.Empty for an LED it does not show.
    internal Rect BoundsOf(int led)
    {
        var bounds = ArrangementFor(ActualWidth).Bounds;
        return led >= 0 && led < bounds.Count ? bounds[led] : Rect.Empty;
    }

    // The LED under a point, NoLed for a gap.
    internal int LedAt(Point position) => ArrangementFor(ActualWidth).LedAt(position);

    // Runs ClickCommand for an LED, as a click (with Shift: extend) on it does; UI Automation
    // selects an LED this way too. False when there is no command or it cannot run.
    internal bool ClickLed(int led, bool extend)
    {
        var hit = new LedHit(led, extend);
        if (ClickCommand is not { } command || !command.CanExecute(hit))
        {
            return false;
        }

        command.Execute(hit);
        return true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LedPreviewAutomationPeer(this);

    protected override Size MeasureOverride(Size availableSize) =>
        Colors.Count == 0 ? default : ArrangementFor(availableSize.Width).Size;

    protected override void OnRender(DrawingContext drawingContext)
    {
        var colors = Colors;
        var arrangement = ArrangementFor(ActualWidth);

        // The corners shrink with a shrunk key map, so a small key still looks like a key. The
        // gap to the selection ring stays, so the 2 px ring never covers the LED.
        var cornerRadius = KeyCornerRadius * arrangement.Scale;
        var selectionRing = SelectionRing();
        var count = Math.Min(colors.Count, arrangement.Bounds.Count);
        for (var led = 0; led < count; led++)
        {
            var bounds = arrangement.Bounds[led];
            if (bounds.IsEmpty)
            {
                continue;
            }

            var brush = ColorBrushes.Get(colors[led]);
            var selected = IsSelected(led);
            if (arrangement.Keys)
            {
                drawingContext.DrawRoundedRectangle(brush, Outline, bounds, cornerRadius, cornerRadius);
                if (selected)
                {
                    bounds.Inflate(SelectionGap, SelectionGap);
                    drawingContext.DrawRoundedRectangle(null, selectionRing, bounds, cornerRadius + SelectionGap, cornerRadius + SelectionGap);
                }
            }
            else
            {
                var center = new Point(bounds.X + bounds.Width / 2.0, bounds.Y + bounds.Height / 2.0);
                var radius = bounds.Height / 2.0;
                drawingContext.DrawEllipse(brush, Outline, center, radius, radius);
                if (selected)
                {
                    drawingContext.DrawEllipse(null, selectionRing, center, radius + SelectionGap, radius + SelectionGap);
                }
            }
        }

        // After all LEDs, so no neighbor covers the ring.
        var active = ActiveLed;
        if (IsKeyboardFocused && active >= 0 && active < count && !arrangement.Bounds[active].IsEmpty)
        {
            var bounds = arrangement.Bounds[active];
            var focusRing = FocusRing();
            if (arrangement.Keys)
            {
                bounds.Inflate(FocusGap, FocusGap);
                drawingContext.DrawRoundedRectangle(null, focusRing, bounds, cornerRadius + FocusGap, cornerRadius + FocusGap);
            }
            else
            {
                var radius = bounds.Height / 2.0 + FocusGap;
                drawingContext.DrawEllipse(null, focusRing, new Point(bounds.X + bounds.Width / 2.0, bounds.Y + bounds.Height / 2.0), radius, radius);
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && PressKey(e.Key, Keyboard.Modifiers))
        {
            e.Handled = true;
        }
    }

    // The focus comes with Tab, or when the active LED moves here from another zone. WPF tells a
    // screen reader that the view has the focus; the active LED follows after that.
    protected override void OnIsKeyboardFocusedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsKeyboardFocusedChanged(e);
        InvalidateVisual();
        if (IsKeyboardFocused)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, ShowActiveLed);
        }
        else
        {
            _toolTip?.Hide();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var led = LedAt(PositionOf(e));
        (_toolTip ??= new LedToolTip(this)).Show(led, LabelOf(led), BoundsOf(led));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _toolTip?.Hide();
    }

    // A click on a gap between keys or beside a ring picks nothing. While the keyboard focus is in
    // the LEDs of another device, as in "Hele huis", it comes along to the LED that was clicked,
    // so the keys go on where the selection is; a click in the same device already moved it there
    // with the active LED (OnActiveLedChanged).
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _toolTip?.Hide();
        var led = LedAt(PositionOf(e));
        if (led == LedMatrix.NoLed || !ClickLed(led, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
        {
            return;
        }

        e.Handled = true;
        if (Keyboard.FocusedElement is LedPreviewView other && other != this && ActiveLed != LedMatrix.NoLed)
        {
            Focus();
        }
    }

    private Point PositionOf(MouseEventArgs e) => MousePosition?.Invoke(e) ?? e.GetPosition(this);

    private static void OnClickCommandChanged(DependencyObject element, DependencyPropertyChangedEventArgs e) =>
        ((LedPreviewView)element).Cursor = e.NewValue is null ? null : Cursors.Hand;

    // When the active LED moves to another zone of the device, the view of that zone takes the
    // focus from the view of the zone it left, which has just heard that it lost it
    // (DevicePreviewViewModel). The view of another device still holds its own active LED.
    private static void OnActiveLedChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        var view = (LedPreviewView)element;
        var holds = (int)e.NewValue != LedMatrix.NoLed;
        KeyboardNavigation.SetIsTabStop(view, holds);
        if (holds && Keyboard.FocusedElement is LedPreviewView { ActiveLed: LedMatrix.NoLed } other && other != view && view.Focus())
        {
            return;
        }

        view.ShowActiveLed();
    }

    // The active LED of a view with the focus: scrolled into view, with its tooltip, and told to a
    // screen reader, which then says its name and whether it is chosen.
    private void ShowActiveLed()
    {
        var led = ActiveLed;
        var bounds = BoundsOf(led);
        if (!IsKeyboardFocused || bounds.IsEmpty)
        {
            return;
        }

        var ring = bounds;
        ring.Inflate(FocusGap + 1.0, FocusGap + 1.0);
        BringIntoView(ring);
        (_toolTip ??= new LedToolTip(this)).Show(led, LabelOf(led), bounds);
        if (UIElementAutomationPeer.FromElement(this) is LedPreviewAutomationPeer peer && AutomationPeer.ListenerExists(AutomationEvents.AutomationFocusChanged))
        {
            peer.RaiseLedFocused(led);
        }
    }

    // A new frame comes many times a second, but only another number of LEDs changes the items
    // UI Automation sees. Without a peer, nobody asked for them yet, so there is nothing to tell.
    private static void OnColorsChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (((IReadOnlyList<RgbColor>)e.OldValue).Count != ((IReadOnlyList<RgbColor>)e.NewValue).Count
            && UIElementAutomationPeer.FromElement((LedPreviewView)element) is LedPreviewAutomationPeer peer)
        {
            peer.ResetChildrenCache();
        }
    }

    private static void OnSelectionFirstChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        var view = (LedPreviewView)element;
        view.RaiseSelectionChanged((int)e.OldValue, view.SelectionLast);
    }

    private static void OnSelectionLastChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        var view = (LedPreviewView)element;
        view.RaiseSelectionChanged(view.SelectionFirst, (int)e.OldValue);
    }

    // Tells a screen reader which LEDs left or joined the selection, from the range oldFirst to
    // oldLast to the one of now.
    private void RaiseSelectionChanged(int oldFirst, int oldLast)
    {
        if (UIElementAutomationPeer.FromElement(this) is not LedPreviewAutomationPeer peer
            || !AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
        {
            return;
        }

        for (var led = 0; led < Colors.Count; led++)
        {
            var wasSelected = led >= oldFirst && led <= oldLast;
            if (wasSelected != IsSelected(led))
            {
                peer.RaiseSelectionChanged(led, wasSelected);
            }
        }
    }

    private static int ColumnsFor(double width, int count)
    {
        if (double.IsInfinity(width) || double.IsNaN(width))
        {
            return Math.Max(count, 1);
        }

        return Math.Clamp((int)((width + LedLayout.Spacing) / LedLayout.Pitch), 1, Math.Max(count, 1));
    }

    // Only made again when the highlight color changed. Rendering runs on the UI thread only.
    private static Pen SelectionRing()
    {
        var color = SystemColors.HighlightColor;
        if (((SolidColorBrush)_selectionRing.Brush).Color != color)
        {
            _selectionRing = CreatePen(color, 2.0);
        }

        return _selectionRing;
    }

    // The text color of the theme; outside the app, as in a test, that of the system.
    private Pen FocusRing()
    {
        var color = TryFindResource("TextBrush") is SolidColorBrush brush ? brush.Color : SystemColors.ControlTextColor;
        if (_focusRing is not { Brush: SolidColorBrush current } || current.Color != color)
        {
            _focusRing = CreatePen(color, 2.0);
        }

        return _focusRing;
    }

    private static Pen CreatePen(Color color, double thickness)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
    }

    // The arrangement of the LEDs in a width; the one of the last call when nothing changed.
    private ILedArrangement ArrangementFor(double width)
    {
        var count = Colors.Count;
        if (IsRing)
        {
            var scale = LedLayout.ScaleFor(RingLayout.Size(count), width);
            if (_arrangement is not RingArrangement ring || ring.LedCount != count || ring.Scale != scale)
            {
                _arrangement = new RingArrangement(count, scale);
            }

            return _arrangement;
        }

        var layout = PlaceLeds(width);
        var pitch = LedLayout.PitchFor(layout, width, shrinks: Matrix is not null);
        if (_arrangement is not CellArrangement cells || !ReferenceEquals(cells.Layout, layout) || cells.Pitch != pitch)
        {
            _arrangement = new CellArrangement(layout, pitch, count, keys: Matrix is not null);
        }

        return _arrangement;
    }

    private LedMatrix PlaceLeds(double width)
    {
        var count = Colors.Count;
        var matrix = Matrix;
        var names = LedNames;
        var columns = matrix is null ? ColumnsFor(width, count) : 0;
        if (_layoutFor.LedCount != count || _layoutFor.Columns != columns || !ReferenceEquals(_layoutFor.Matrix, matrix)
            || !ReferenceEquals(_layoutFor.Names, names))
        {
            _layout = matrix is null ? LedLayout.Wrap(count, columns) : WideKeys.Widen(LedLayout.Place(matrix, count), names);
            _layoutFor = (count, columns, matrix, names);
        }

        return _layout;
    }
}
