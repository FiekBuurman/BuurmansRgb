using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

// Draws every LED as a dot, wrapping to the available width. One element per device instead of
// one per LED keeps a 104-key keyboard cheap to redraw. A click on a dot runs ClickCommand with
// a LedHit; the LEDs SelectionFirst to SelectionLast get a ring.
public sealed class LedStripView : FrameworkElement
{
    public static readonly DependencyProperty ColorsProperty = DependencyProperty.Register(
        nameof(Colors),
        typeof(IReadOnlyList<RgbColor>),
        typeof(LedStripView),
        new FrameworkPropertyMetadata(Array.Empty<RgbColor>(), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionFirstProperty = DependencyProperty.Register(
        nameof(SelectionFirst), typeof(int), typeof(LedStripView), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionLastProperty = DependencyProperty.Register(
        nameof(SelectionLast), typeof(int), typeof(LedStripView), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ClickCommandProperty = DependencyProperty.Register(
        nameof(ClickCommand), typeof(ICommand), typeof(LedStripView), new PropertyMetadata(null, OnClickCommandChanged));

    private const double LedSize = 14.0;
    private const double Spacing = 4.0;
    private const double Pitch = LedSize + Spacing;
    private const double Radius = LedSize / 2.0;

    private static readonly Pen Outline = CreatePen(System.Windows.Media.Color.FromArgb(0x60, 0x80, 0x80, 0x80), 1.0);

    // In the system highlight color, so the ring also shows in dark and high-contrast themes. Read
    // on every render, so the ring follows a theme change from the next redraw on.
    private static Pen _selectionRing = CreatePen(SystemColors.HighlightColor, 2.0);

    public IReadOnlyList<RgbColor> Colors
    {
        get => (IReadOnlyList<RgbColor>)GetValue(ColorsProperty);
        set => SetValue(ColorsProperty, value);
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
        var count = Colors.Count;
        if (count == 0)
        {
            return default;
        }

        var columns = ColumnsFor(availableSize.Width, count);
        var rows = (count + columns - 1) / columns;
        return new Size(columns * Pitch - Spacing, rows * Pitch - Spacing);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var colors = Colors;
        var columns = ColumnsFor(ActualWidth, colors.Count);
        var selectionRing = SelectionRing();
        for (var i = 0; i < colors.Count; i++)
        {
            var center = new Point((i % columns) * Pitch + Radius, (i / columns) * Pitch + Radius);
            drawingContext.DrawEllipse(ColorBrushes.Get(colors[i]), Outline, center, Radius, Radius);
            if (i >= SelectionFirst && i <= SelectionLast)
            {
                drawingContext.DrawEllipse(null, selectionRing, center, Radius + 1.5, Radius + 1.5);
            }
        }
    }

    // The whole cell around a dot counts, so a click between two dots still picks one.
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (ClickCommand is not { } command)
        {
            return;
        }

        var position = e.GetPosition(this);
        var columns = ColumnsFor(ActualWidth, Colors.Count);
        var column = (int)(position.X / Pitch);
        var index = (int)(position.Y / Pitch) * columns + column;
        if (position.X < 0 || position.Y < 0 || column >= columns || index >= Colors.Count)
        {
            return;
        }

        var hit = new LedHit(index, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
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
