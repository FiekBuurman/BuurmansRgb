using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

// Draws the LEDs of a zone in one of two ways. Without a Matrix, such as for a strip or a fan,
// every LED is a dot, wrapping to the available width. With a Matrix, such as the key map of a
// keyboard, every LED is a key at its own position, shrunk when the matrix is wider than the
// available width. One element per zone instead of one per LED keeps a 104-key keyboard
// cheap to redraw. A click on an LED runs ClickCommand with a LedHit; the LEDs SelectionFirst to
// SelectionLast get a ring.
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

    // Holds only LEDs below Colors.Count. Made again only when the number of LEDs, the columns or
    // the matrix change, not on every frame.
    private LedMatrix _layout = LedLayout.Wrap(0, 1);
    private (int LedCount, int Columns, LedMatrix? Matrix) _layoutFor;

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

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Colors.Count == 0)
        {
            return default;
        }

        var (layout, pitch) = LayoutFor(availableSize.Width);
        var scale = pitch / LedLayout.Pitch;
        return new Size(
            (layout.Width * LedLayout.Pitch - LedLayout.Spacing) * scale,
            (layout.Height * LedLayout.Pitch - LedLayout.Spacing) * scale);
    }

    protected override void OnRender(DrawingContext drawingContext)
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
            var led = layout.Cells[cell];
            if (led == LedMatrix.NoLed)
            {
                continue;
            }

            var bounds = new Rect((cell % layout.Width) * pitch, (cell / layout.Width) * pitch, size, size);
            var selected = led >= SelectionFirst && led <= SelectionLast;
            var brush = ColorBrushes.Get(colors[led]);
            if (keys)
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
                var center = new Point(bounds.X + size / 2.0, bounds.Y + size / 2.0);
                drawingContext.DrawEllipse(brush, Outline, center, size / 2.0, size / 2.0);
                if (selected)
                {
                    drawingContext.DrawEllipse(null, selectionRing, center, size / 2.0 + SelectionGap, size / 2.0 + SelectionGap);
                }
            }
        }
    }

    // A click on a gap between keys picks nothing.
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (ClickCommand is not { } command)
        {
            return;
        }

        var (layout, pitch) = LayoutFor(ActualWidth);
        var position = e.GetPosition(this);
        var led = LedLayout.LedAt(layout, pitch, position.X, position.Y);
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

    // The layout and its pitch for a width; Measure, OnRender and the click all use this, so a
    // click lands on the key that is drawn there.
    private (LedMatrix Layout, double Pitch) LayoutFor(double width)
    {
        var layout = PlaceLeds(width);
        return (layout, LedLayout.PitchFor(layout, width, shrinks: Matrix is not null));
    }

    private LedMatrix PlaceLeds(double width)
    {
        var count = Colors.Count;
        var matrix = Matrix;
        var columns = matrix is null ? ColumnsFor(width, count) : 0;
        if (_layoutFor.LedCount != count || _layoutFor.Columns != columns || !ReferenceEquals(_layoutFor.Matrix, matrix))
        {
            _layout = matrix is null ? LedLayout.Wrap(count, columns) : LedLayout.Place(matrix, count);
            _layoutFor = (count, columns, matrix);
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
