using System.Windows.Controls;
using System.Windows.Input;

namespace BuurmansRgb.UI.Views.Pages;

public partial class LampsPage : UserControl
{
    public LampsPage() => InitializeComponent();

    // The time of "Uit om" or "Aan om" counts once the field loses the focus, or at once with Enter.
    private void OnWholeHouseTimeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox box)
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            e.Handled = true;
        }
    }
}
