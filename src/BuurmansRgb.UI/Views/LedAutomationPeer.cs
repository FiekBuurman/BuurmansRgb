using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

// One LED of a LedPreviewView for UI Automation: its name, where it is drawn, whether it is
// selected and, for the active LED of a focused view, that it has the focus. Selecting it does
// what a click does, and adding it to the selection what Shift+click does. It has no element of
// its own, so it asks the view for all of that.
internal sealed class LedAutomationPeer(LedPreviewAutomationPeer list, LedPreviewView view, int led) : AutomationPeer, ISelectionItemProvider
{
    private string? _label;
    private string? _name;

    public int Index => led;

    public bool IsSelected => view.IsSelected(led);

    public IRawElementProviderSimple SelectionContainer => ProviderFromPeer(list);

    public void Select() => Click(extend: false);

    public void AddToSelection() => Click(extend: true);

    // The editor has no way to drop one LED from a range.
    public void RemoveFromSelection() => throw new InvalidOperationException("An LED cannot leave the selection on its own.");

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.SelectionItem ? this : null;

    // "LED 5, rood": its label with the name of the color it showed when it last got the focus, or
    // when a screen reader first asked for it. While a screen reader listens, WPF compares the
    // name after every layout pass, and each frame of a moving effect makes one: a name that
    // followed the frames would raise a name change per LED per frame. So it stays until the LED
    // gets the focus again (Rename), the focus comes into the view or the number of LEDs changes
    // (both LedPreviewAutomationPeer.RenameLeds), or it gets another label.
    protected override string GetNameCore()
    {
        if (view.LabelOf(led) is not { } label)
        {
            return string.Empty;
        }

        if (_name is null || label != _label)
        {
            _label = label;
            _name = led < view.Colors.Count ? UiText.Format(Strings.LedNameWithColor, label, ColorNames.NameOf(view.Colors[led])) : label;
        }

        return _name;
    }

    // The next time its name is asked for, it says the color of now.
    internal void Rename() => _name = null;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string GetClassNameCore() => "Led";

    protected override string GetAutomationIdCore() => $"Led{led}";

    // In screen pixels, as UI Automation wants it. One PointToScreen per LED: the size only needs
    // the scale of the screen, as the preview has no transforms of its own.
    protected override Rect GetBoundingRectangleCore()
    {
        var bounds = view.BoundsOf(led);
        if (bounds.IsEmpty || PresentationSource.FromVisual(view) is not { CompositionTarget: { } target })
        {
            return Rect.Empty;
        }

        var size = target.TransformToDevice.Transform(new Vector(bounds.Width, bounds.Height));
        var topLeft = view.PointToScreen(bounds.TopLeft);
        return new Rect(topLeft.X, topLeft.Y, size.X, size.Y);
    }

    protected override Point GetClickablePointCore()
    {
        var bounds = GetBoundingRectangleCore();
        return bounds.IsEmpty ? new Point(double.NaN, double.NaN) : new Point(bounds.X + bounds.Width / 2.0, bounds.Y + bounds.Height / 2.0);
    }

    // Asking the list would walk up the tree once per LED; the LEDs show when the view does.
    protected override bool IsOffscreenCore() => !view.IsVisible;

    protected override bool IsEnabledCore() => view.IsEnabled;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    // Only the active LED takes the focus: the keys of the view move it, not UI Automation.
    protected override bool IsKeyboardFocusableCore() => view.Focusable && view.ActiveLed == led;

    protected override bool HasKeyboardFocusCore() => view.IsKeyboardFocused && view.ActiveLed == led;

    protected override void SetFocusCore()
    {
        if (!IsKeyboardFocusableCore() || !view.Focus())
        {
            throw new InvalidOperationException("Only the active LED can take the focus.");
        }
    }

    protected override bool IsPasswordCore() => false;

    protected override bool IsRequiredForFormCore() => false;

    protected override string GetAcceleratorKeyCore() => string.Empty;

    protected override string GetAccessKeyCore() => string.Empty;

    protected override string GetHelpTextCore() => string.Empty;

    protected override string GetItemStatusCore() => string.Empty;

    protected override string GetItemTypeCore() => string.Empty;

    protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;

    protected override AutomationPeer? GetLabeledByCore() => null;

    protected override List<AutomationPeer>? GetChildrenCore() => null;

    private void Click(bool extend)
    {
        if (!view.ClickLed(led, extend))
        {
            throw new ElementNotEnabledException();
        }
    }
}
