using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

// Draws every LED as a dot, wrapping to the available width. With a Matrix, such as the key map
// of a keyboard, every LED is a key at its own position instead, shrunk when the matrix is wider
// than the available width. One element per zone instead of one per LED keeps a 104-key keyboard
// cheap to redraw. A click on an LED runs ClickCommand with a LedHit; the LEDs SelectionFirst to
// SelectionLast get a ring.
public sealed class LedStripView : FrameworkElement
{
    public static readonly DependencyProperty ColorsProperty = DependencyProperty.Register(
        nameof(Colors),
        typeof(IReadOnlyList<RgbColor>),
        typeof(LedStripView),
        new FrameworkPropertyMetadata(Array.Empty<RgbColor>(), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MatrixProperty = DependencyProperty.Register(
        nameof(Matrix),
        typeof(LedMatrix),
        typeof(LedStripView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionFirstProperty = DependencyProperty.Register(
        nameof(SelectionFirst), typeof(int), typeof(LedStripView), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionLastProperty = DependencyProperty.Register(
        nameof(SelectionLast), typeof(int), typeof(LedStripView), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ClickCommandProperty = DependencyProperty.Register(
        nameof(ClickCommand), typeof(ICommand), typeof(LedStripView), new PropertyMetadata(null, OnClickCommandChanged));

    private const double LedSize = 14.0;
    private const double Spacing = 4.0;
    private const double Pitch = LedSize + Spacing;
    private const double KeyCornerRadius = 3.0;
    private const double SelectionGap = 1.5;

    // A wide key map shrinks to fit, but no further than this, so the keys stay clickable.
    private const double MinScale = 0.5;

    private static readonly Pen Outline = CreatePen(System.Windows.Media.Color.FromArgb(0x60, 0x80, 0x80, 0x80), 1.0);

    // In the system highlight color, so the ring also shows in dark and high-contrast themes. Read
    // on every render, so the ring follows a theme change from the next redraw on.
    private static Pen _selectionRing = CreatePen(SystemColors.HighlightColor, 2.0);

    // Made again only when the number of LEDs, the columns or the matrix change, not on every frame.
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

        var layout = LayoutFor(availableSize.Width);
        var scale = ScaleFor(layout, availableSize.Width);
        return new Size((layout.Width * Pitch - Spacing) * scale, (layout.Height * Pitch - Spacing) * scale);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var colors = Colors;
        var layout = LayoutFor(ActualWidth);
        var scale = ScaleFor(layout, ActualWidth);
        var pitch = Pitch * scale;
        var size = LedSize * scale;
        var keys = Matrix is not null;
        var selectionRing = SelectionRing();
        for (var cell = 0; cell < layout.Cells.Count; cell++)
        {
            var led = layout.Cells[cell];
            if (led == LedMatrix.NoLed || led >= colors.Count)
            {
                continue;
            }

            var bounds = new Rect((cell % layout.Width) * pitch, (cell / layout.Width) * pitch, size, size);
            var selected = led >= SelectionFirst && led <= SelectionLast;
            var brush = ColorBrushes.Get(colors[led]);
            if (keys)
            {
                drawingContext.DrawRoundedRectangle(brush, Outline, bounds, KeyCornerRadius, KeyCornerRadius);
                if (selected)
                {
                    bounds.Inflate(SelectionGap, SelectionGap);
                    drawingContext.DrawRoundedRectangle(null, selectionRing, bounds, KeyCornerRadius + SelectionGap, KeyCornerRadius + SelectionGap);
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

    // The whole cell around an LED counts, so a click between two LEDs still picks one. A click
    // on a gap between keys picks nothing.
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (ClickCommand is not { } command)
        {
            return;
        }

        var layout = LayoutFor(ActualWidth);
        var pitch = Pitch * ScaleFor(layout, ActualWidth);
        var position = e.GetPosition(this);
        var column = (int)Math.Floor(position.X / pitch);
        var row = (int)Math.Floor(position.Y / pitch);
        if (column < 0 || column >= layout.Width || row < 0 || row >= layout.Height)
        {
            return;
        }

        var led = layout[row, column];
        if (led == LedMatrix.NoLed || led >= Colors.Count)
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
        ((LedStripView)element).Cursor = e.NewValue is null ? null : Cursors.Hand;

    private static int ColumnsFor(double width, int count)
    {
        if (double.IsInfinity(width) || double.IsNaN(width))
        {
            return Math.Max(count, 1);
        }

        return Math.Clamp((int)((width + Spacing) / Pitch), 1, Math.Max(count, 1));
    }

    // Only a matrix shrinks; dots wrap to the next row instead.
    private double ScaleFor(LedMatrix layout, double width)
    {
        var naturalWidth = layout.Width * Pitch - Spacing;
        if (Matrix is null || double.IsInfinity(width) || double.IsNaN(width) || naturalWidth <= width)
        {
            return 1.0;
        }

        return Math.Max(width / naturalWidth, MinScale);
    }

    private LedMatrix LayoutFor(double width)
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
