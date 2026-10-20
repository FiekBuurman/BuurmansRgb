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

    // Holds only LEDs below Colors.Count. Made again only when the number of LEDs, the columns, the
    // matrix or the LED names change, not on every frame.
    private LedMatrix _layout = LedLayout.Wrap(0, 1);
    private (int LedCount, int Columns, LedMatrix? Matrix, IReadOnlyList<string>? Names) _layoutFor;

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

    // Ignored for a zone with a Matrix.
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
        if (led < 0 || led >= Colors.Count)
        {
            return Rect.Empty;
        }

        if (ShowsRing)
        {
            var scale = RingScale(ActualWidth);
            var center = LedLayout.RingCenter(led, Colors.Count);
            var size = LedLayout.LedSize * scale;
            return new Rect(center.X * scale - size / 2.0, center.Y * scale - size / 2.0, size, size);
        }

        var (layout, pitch) = LayoutFor(ActualWidth);
        return LedLayout.BoundsOf(layout, pitch, led);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LedPreviewAutomationPeer(this);

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

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Colors.Count == 0)
        {
            return default;
        }

        if (ShowsRing)
        {
            var side = LedLayout.RingSize(Colors.Count) * RingScale(availableSize.Width);
            return new Size(side, side);
        }

        var (layout, pitch) = LayoutFor(availableSize.Width);
        var scale = pitch / LedLayout.Pitch;
        return new Size(
            (layout.Width * LedLayout.Pitch - LedLayout.Spacing) * scale,
            (layout.Height * LedLayout.Pitch - LedLayout.Spacing) * scale);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (ShowsRing)
        {
            RenderRing(drawingContext);
        }
        else
        {
            RenderCells(drawingContext);
        }
    }

    // A click on a gap between keys or beside a ring picks nothing.
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _toolTip?.Hide();
        if (ClickCommand is not { } command)
        {
            return;
        }

        var led = LedAt(e.GetPosition(this));
        if (led == LedMatrix.NoLed)
        {
            return;
        }

        var hit = new LedHit(led, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        if (command.CanExecute(hit))
        {
            command.Execute(hit);
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

    // A key map always shows as keys, also on a fan.
    private bool ShowsRing => IsRing && Matrix is null;

    private double RingScale(double width) => LedLayout.ScaleFor(LedLayout.RingSize(Colors.Count), width);

    // The LED under a point; Measure, OnRender and the click all use the same layout, so a click
    // lands on the LED that is drawn there.
    internal int LedAt(Point position)
    {
        if (ShowsRing)
        {
            return LedLayout.RingLedAt(Colors.Count, RingScale(ActualWidth), position.X, position.Y);
        }

        var (layout, pitch) = LayoutFor(ActualWidth);
        return LedLayout.LedAt(layout, pitch, position.X, position.Y);
    }

    private void RenderRing(DrawingContext drawingContext)
    {
        var colors = Colors;
        var scale = RingScale(ActualWidth);
        var selectionRing = SelectionRing();
        for (var led = 0; led < colors.Count; led++)
        {
            var center = LedLayout.RingCenter(led, colors.Count);
            DrawDot(drawingContext, ColorBrushes.Get(colors[led]), IsSelected(led), new Point(center.X * scale, center.Y * scale), LedLayout.LedSize * scale / 2.0, selectionRing);
        }
    }

    private void RenderCells(DrawingContext drawingContext)
    {
        var colors = Colors;
        var (layout, pitch) = LayoutFor(ActualWidth);
        var scale = pitch / LedLayout.Pitch;
        var size = LedLayout.LedSize * scale;

        // The corners shrink with a shrunk key map, so a small key still looks like a key. The
        // gap to the selection ring stays, so the 2 px ring never covers the key.
        var cornerRadius = KeyCornerRadius * scale;
        var keys = Matrix is not null;
        var selectionRing = SelectionRing();
        for (var cell = 0; cell < layout.Cells.Count; cell++)
        {
            // A wide key is drawn once, from its first cell.
            var led = layout.Cells[cell];
            if (led == LedMatrix.NoLed || (cell % layout.Width > 0 && layout.Cells[cell - 1] == led))
            {
                continue;
            }

            var bounds = LedLayout.CellBounds(layout, pitch, cell);
            if (keys)
            {
                drawingContext.DrawRoundedRectangle(ColorBrushes.Get(colors[led]), Outline, bounds, cornerRadius, cornerRadius);
                if (IsSelected(led))
                {
                    bounds.Inflate(SelectionGap, SelectionGap);
                    drawingContext.DrawRoundedRectangle(null, selectionRing, bounds, cornerRadius + SelectionGap, cornerRadius + SelectionGap);
                }
            }
            else
            {
                DrawDot(drawingContext, ColorBrushes.Get(colors[led]), IsSelected(led), new Point(bounds.X + size / 2.0, bounds.Y + size / 2.0), size / 2.0, selectionRing);
            }
        }
    }

    private static void DrawDot(DrawingContext drawingContext, Brush brush, bool selected, Point center, double radius, Pen selectionRing)
    {
        drawingContext.DrawEllipse(brush, Outline, center, radius, radius);
        if (selected)
        {
            drawingContext.DrawEllipse(null, selectionRing, center, radius + SelectionGap, radius + SelectionGap);
        }
    }

    // The layout of a row or key map and its pitch for a width.
    private (LedMatrix Layout, double Pitch) LayoutFor(double width)
    {
        var layout = PlaceLeds(width);
        return (layout, LedLayout.PitchFor(layout, width, shrinks: Matrix is not null));
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
            _layout = matrix is null ? LedLayout.Wrap(count, columns) : LedLayout.Widen(LedLayout.Place(matrix, count), names);
            _layoutFor = (count, columns, matrix, names);
        }

        return _layout;
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
}
