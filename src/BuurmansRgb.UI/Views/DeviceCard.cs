using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace BuurmansRgb.UI.Views;

// The card of a device on Kleur that opens and closes (DESIGN.md § 9.3 "Apparaatkaart"), with its
// template in Themes/Controls.xaml. The part HeaderSite is the ToggleButton of the head: it takes the
// keyboard focus, Enter, Space and the mouse, and → opens and ← closes the card, as a folder in
// Verkenner. For UI Automation the card itself is the element (ExpanderAutomationPeer, type Group
// with ExpandCollapse): it reports the focus of HeaderSite as its own, so Verteller reads the card,
// "Gesimuleerde ledstrip, samengevouwen". Its name, help text and heading level are therefore set on
// the card. Aside is what stands on the right of the head, beside HeaderSite and not in it (the small
// strip and Knipperen), so Knipperen is a Tab stop of its own and no button in a button; Note stands
// under the head also while the card is closed, such as the mark of a flash. Opening and closing
// never animates. Closing with the focus in the card puts the focus on its head, so it does not fall
// to the window.
public sealed class DeviceCard : Expander
{
    public static readonly DependencyProperty AsideProperty = DependencyProperty.Register(
        nameof(Aside), typeof(object), typeof(DeviceCard), new PropertyMetadata(null));

    public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(
        nameof(Note), typeof(object), typeof(DeviceCard), new PropertyMetadata(null));

    // The key under which FocusKeeper gives the focus back to the head when the preview is made
    // again for the same device, such as after a new length.
    public static readonly DependencyProperty FocusKeyProperty = DependencyProperty.Register(
        nameof(FocusKey), typeof(string), typeof(DeviceCard), new PropertyMetadata(null, OnFocusKeyChanged));

    public const string HeaderSitePart = "HeaderSite";

    public const string ExpandSitePart = "ExpandSite";

    private ToggleButton? _headerSite;
    private FrameworkElement? _expandSite;

    public object? Aside
    {
        get => GetValue(AsideProperty);
        set => SetValue(AsideProperty, value);
    }

    public object? Note
    {
        get => GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    public string? FocusKey
    {
        get => (string?)GetValue(FocusKeyProperty);
        set => SetValue(FocusKeyProperty, value);
    }

    // The head of the card, which takes the keyboard focus.
    public ToggleButton? HeaderSite => _headerSite;

    public override void OnApplyTemplate()
    {
        if (_headerSite is not null)
        {
            _headerSite.PreviewKeyDown -= OnHeaderKeyDown;
        }

        base.OnApplyTemplate();
        _headerSite = GetTemplateChild(HeaderSitePart) as ToggleButton;
        _expandSite = GetTemplateChild(ExpandSitePart) as FrameworkElement;
        if (_headerSite is not null)
        {
            _headerSite.PreviewKeyDown += OnHeaderKeyDown;
            FocusKeeper.SetKey(_headerSite, FocusKey);
        }
    }

    // Puts the keyboard focus on the head, such as after a jump to the card.
    public bool FocusHeader() => _headerSite?.Focus() == true;

    protected override void OnCollapsed()
    {
        var focusInside = _expandSite?.IsKeyboardFocusWithin == true;
        base.OnCollapsed();
        if (focusInside)
        {
            FocusHeader();
        }
    }

    private static void OnFocusKeyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is DeviceCard { _headerSite: { } headerSite })
        {
            FocusKeeper.SetKey(headerSite, (string?)e.NewValue);
        }
    }

    // → opens a closed card and ← closes an open one; the focus stays on the head. Without Alt or
    // Ctrl, which belong to the window.
    private void OnHeaderKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None || (e.Key != Key.Right && e.Key != Key.Left))
        {
            return;
        }

        IsExpanded = e.Key == Key.Right;
        e.Handled = true;
    }
}
