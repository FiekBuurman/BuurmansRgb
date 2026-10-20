using System.Windows;
using System.Windows.Automation.Peers;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.Views;

// One LED of a LedPreviewView for UI Automation: its name, where it is drawn and whether it is
// selected. It has no element of its own, so it asks the view for all of that.
internal sealed class LedAutomationPeer(LedPreviewAutomationPeer list, LedPreviewView view, int led) : AutomationPeer
{
    protected override string GetNameCore() => view.LabelOf(led) ?? string.Empty;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string GetClassNameCore() => "Led";

    protected override string GetAutomationIdCore() => $"Led{led}";

    // Like the selection ring, so the selection does not rest on color alone.
    protected override string GetItemStatusCore() => view.IsSelected(led) ? Strings.LedSelectedStatus : string.Empty;

    // In screen pixels, as UI Automation wants it.
    protected override Rect GetBoundingRectangleCore()
    {
        var bounds = view.BoundsOf(led);
        if (bounds.IsEmpty || PresentationSource.FromVisual(view) is null)
        {
            return Rect.Empty;
        }

        return new Rect(view.PointToScreen(bounds.TopLeft), view.PointToScreen(bounds.BottomRight));
    }

    protected override Point GetClickablePointCore()
    {
        var bounds = GetBoundingRectangleCore();
        return bounds.IsEmpty ? new Point(double.NaN, double.NaN) : new Point(bounds.X + bounds.Width / 2.0, bounds.Y + bounds.Height / 2.0);
    }

    protected override bool IsOffscreenCore() => list.IsOffscreen();

    protected override bool IsEnabledCore() => view.IsEnabled;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override bool HasKeyboardFocusCore() => false;

    protected override void SetFocusCore()
    {
    }

    protected override bool IsPasswordCore() => false;

    protected override bool IsRequiredForFormCore() => false;

    protected override string GetAcceleratorKeyCore() => string.Empty;

    protected override string GetAccessKeyCore() => string.Empty;

    protected override string GetHelpTextCore() => string.Empty;

    protected override string GetItemTypeCore() => string.Empty;

    protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;

    protected override AutomationPeer? GetLabeledByCore() => null;

    protected override List<AutomationPeer>? GetChildrenCore() => null;

    public override object? GetPattern(PatternInterface patternInterface) => null;
}
