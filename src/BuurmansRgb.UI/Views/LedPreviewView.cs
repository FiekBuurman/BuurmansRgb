using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
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
public sealed class LedPreviewView : FrameworkElement
{
    public static readonly DependencyProperty ColorsProperty = DependencyProperty.Register(
        nameof(Colors),
        typeof(IReadOnlyList<RgbColor>),
        typeof(LedPreviewView),
        new FrameworkPropertyMetadata(Array.Empty<RgbColor>(), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

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
        nameof(SelectionFirst), typeof(int), typeof(LedPreviewView), new FrameworkPropertyMetadata(LedSelection.NoSelection, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionLastProperty = DependencyProperty.Register(
        nameof(SelectionLast), typeof(int), typeof(LedPreviewView), new FrameworkPropertyMetadata(LedSelection.NoSelection, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ClickCommandProperty = DependencyProperty.Register(
        nameof(ClickCommand), typeof(ICommand), typeof(LedPreviewView), new PropertyMetadata(null, OnClickCommandChanged));

    private const double KeyCornerRadius = 3.0;
    private const double SelectionGap = 1.5;

    private static readonly Pen Outline = CreatePen(System.Windows.Media.Color.FromArgb(0x60, 0x80, 0x80, 0x80), 1.0);

    // In the system highlight color, so the ring also shows in dark and high-contrast themes. Read
    // on every render, so the ring follows a theme change from the next redraw on.
    private static Pen _selectionRing = CreatePen(SystemColors.HighlightColor, 2.0);

    // The cells of a row or key map, holding only LEDs below Colors.Count, and the arrangement of
    // the LEDs. Both are made again only when the number of LEDs, the width, the matrix or the LED
    // names change, not on every frame.
    private LedMatrix _layout = LedLayout.Wrap(0, 1);
    private (int LedCount, int Columns, LedMatrix? Matrix, IReadOnlyList<string>? Names) _layoutFor;
    private ILedArrangement? _arrangement;

    // Made on the first hover, so a preview nobody points at has no tooltip and no timer.
    private LedToolTip? _toolTip;

    public LedPreviewView() => Unloaded += (_, _) => _toolTip?.Hide();

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
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var led = LedAt(e.GetPosition(this));
        (_toolTip ??= new LedToolTip(this)).Show(led, LabelOf(led), BoundsOf(led));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _toolTip?.Hide();
    }

    // A click on a gap between keys or beside a ring picks nothing.
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _toolTip?.Hide();
        var led = LedAt(e.GetPosition(this));
        if (led != LedMatrix.NoLed && ClickLed(led, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
        {
            e.Handled = true;
        }
    }

    private static void OnClickCommandChanged(DependencyObject element, DependencyPropertyChangedEventArgs e) =>
        ((LedPreviewView)element).Cursor = e.NewValue is null ? null : Cursors.Hand;

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
