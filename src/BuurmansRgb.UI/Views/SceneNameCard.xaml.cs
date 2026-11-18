using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BuurmansRgb.UI.Views;

public partial class SceneNameCard : UserControl
{
    public static readonly DependencyProperty ReturnFocusToProperty = DependencyProperty.Register(
        nameof(ReturnFocusTo), typeof(UIElement), typeof(SceneNameCard));

    public SceneNameCard()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    // Gets the keyboard focus when the card goes while the focus is in it, such as after Enter or
    // Escape in the name: WPF would drop it with the card, which leaves a keyboard user at the top of
    // the window.
    public UIElement? ReturnFocusTo
    {
        get => (UIElement?)GetValue(ReturnFocusToProperty);
        set => SetValue(ReturnFocusToProperty, value);
    }

    // Once the card shows, so typing can start at once.
    public void FocusName() => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => NameBox.Focus());

    // The button that opened the card shows again as the card goes, maybe only after it: the focus
    // goes there once both changed.
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false && IsKeyboardFocusWithin && ReturnFocusTo is { } target)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (target.IsVisible)
                {
                    target.Focus();
                }
            });
        }
    }
}
