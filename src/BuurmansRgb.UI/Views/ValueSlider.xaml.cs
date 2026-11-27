using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BuurmansRgb.UI.Views;

// A panel rather than a UserControl, which would only build its parts once it shows: so the slider is
// there even while it is hidden, such as Richting with Statisch, and it adds no element for UI
// Automation, so a screen reader meets the name, value and slider as it did before this control.
public partial class ValueSlider : StackPanel
{
    // The name above the slider, which is its name for a screen reader too.
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(ValueSlider), new PropertyMetadata(null));

    // The value as the page writes it, on the right, such as "80%" or "1,0 s".
    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(
        nameof(ValueText), typeof(string), typeof(ValueSlider), new PropertyMetadata(null));

    // Beside the value, such as the arrow of Richting; nothing by default.
    public static readonly DependencyProperty ValueIconProperty = DependencyProperty.Register(
        nameof(ValueIcon), typeof(object), typeof(ValueSlider), new PropertyMetadata(null));

    // What a screen reader says of the slider beside its name, which is also its tooltip.
    public static readonly DependencyProperty HelpTextProperty = DependencyProperty.Register(
        nameof(HelpText), typeof(string), typeof(ValueSlider), new PropertyMetadata(null));

    // What a screen reader says instead of the bare number, such as "Volgorde" or "1,0 s".
    public static readonly DependencyProperty ItemStatusProperty = DependencyProperty.Register(
        nameof(ItemStatus), typeof(string), typeof(ValueSlider), new PropertyMetadata(null));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ValueSlider),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    // The range and steps, with the defaults of a slider.
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(ValueSlider), new PropertyMetadata(0.0));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(ValueSlider), new PropertyMetadata(10.0));

    public static readonly DependencyProperty SmallChangeProperty = DependencyProperty.Register(
        nameof(SmallChange), typeof(double), typeof(ValueSlider), new PropertyMetadata(0.1));

    public static readonly DependencyProperty LargeChangeProperty = DependencyProperty.Register(
        nameof(LargeChange), typeof(double), typeof(ValueSlider), new PropertyMetadata(1.0));

    public static readonly DependencyProperty TickFrequencyProperty = DependencyProperty.Register(
        nameof(TickFrequency), typeof(double), typeof(ValueSlider), new PropertyMetadata(1.0));

    // The fill of the slider, such as red for Rood; without one, that of the style of every slider.
    // Not Foreground, which the name above it would take too.
    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush), typeof(Brush), typeof(ValueSlider), new PropertyMetadata(null, OnFillBrushChanged));

    public ValueSlider()
    {
        InitializeComponent();

        // Found without an x:Name, which UI Automation would take as its AutomationId; it takes
        // the name the page gives this control instead, such as DirectionSlider.
        Slider = Children.OfType<Slider>().Single();
    }

    // The slider itself, for what needs a focusable element, such as FocusFallback.Target.
    public Slider Slider { get; }

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? ValueText
    {
        get => (string?)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    public object? ValueIcon
    {
        get => GetValue(ValueIconProperty);
        set => SetValue(ValueIconProperty, value);
    }

    public string? HelpText
    {
        get => (string?)GetValue(HelpTextProperty);
        set => SetValue(HelpTextProperty, value);
    }

    public string? ItemStatus
    {
        get => (string?)GetValue(ItemStatusProperty);
        set => SetValue(ItemStatusProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double SmallChange
    {
        get => (double)GetValue(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    public double LargeChange
    {
        get => (double)GetValue(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
    }

    public double TickFrequency
    {
        get => (double)GetValue(TickFrequencyProperty);
        set => SetValue(TickFrequencyProperty, value);
    }

    public Brush? FillBrush
    {
        get => (Brush?)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    // A local Foreground of null would hide the fill, so without a brush the style sets it again.
    private static void OnFillBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var slider = ((ValueSlider)d).Slider;
        if (e.NewValue is Brush brush)
        {
            slider.Foreground = brush;
        }
        else
        {
            slider.ClearValue(Control.ForegroundProperty);
        }
    }
}
