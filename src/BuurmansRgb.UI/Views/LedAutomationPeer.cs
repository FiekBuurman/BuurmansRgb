using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace BuurmansRgb.UI.Views;

// One LED of a LedPreviewView for UI Automation: its name, where it is drawn, whether it is
// selected and, for the active LED of a focused view, that it has the focus. Selecting it does what a click does, and adding it to the selection what Shift+click
// does. It has no element of its own, so it asks the view for all of that.
internal sealed class LedAutomationPeer(LedPreviewAutomationPeer list, LedPreviewView view, int led) : AutomationPeer, ISelectionItemProvider
{
    public int Index => led;

    public bool IsSelected => view.IsSelected(led);

    public IRawElementProviderSimple SelectionContainer => ProviderFromPeer(list);

    public void Select() => Click(extend: false);

    public void AddToSelection() => Click(extend: true);

    // The editor has no way to drop one LED from a range.
    public void RemoveFromSelection() => throw new InvalidOperationException("An LED cannot leave the selection on its own.");

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.SelectionItem ? this : null;

    protected override string GetNameCore() => view.LabelOf(led) ?? string.Empty;

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
