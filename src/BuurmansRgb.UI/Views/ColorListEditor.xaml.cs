using System.Windows;
using System.Windows.Controls;

namespace BuurmansRgb.UI.Views;

public partial class ColorListEditor : UserControl
{
    // The name of the list of tiles for a screen reader, such as "Kleuren van de loop". On the list
    // itself, not as AutomationProperties.Name on the editor, which would add an element of that
    // name around the list, so a screen reader would say it twice.
    public static readonly DependencyProperty ListNameProperty = DependencyProperty.Register(
        nameof(ListName), typeof(string), typeof(ColorListEditor), new PropertyMetadata(null));

    public ColorListEditor() => InitializeComponent();

    public string? ListName
    {
        get => (string?)GetValue(ListNameProperty);
        set => SetValue(ListNameProperty, value);
    }
}
