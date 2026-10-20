using System.Windows.Automation.Peers;

namespace BuurmansRgb.UI.Views;

// Shows a LedPreviewView to UI Automation as a list with an item per LED, named like its tooltip,
// so a screen reader can name each key and tell where it is. The items are kept, so a screen
// reader sees the same item for an LED on every frame.
internal sealed class LedPreviewAutomationPeer(LedPreviewView owner) : FrameworkElementAutomationPeer(owner)
{
    private readonly List<LedAutomationPeer> _leds = [];

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

    protected override string GetClassNameCore() => nameof(LedPreviewView);

    protected override List<AutomationPeer> GetChildrenCore()
    {
        var view = (LedPreviewView)Owner;
        var count = view.Colors.Count;
        while (_leds.Count < count)
        {
            _leds.Add(new LedAutomationPeer(this, view, _leds.Count));
        }

        _leds.RemoveRange(count, _leds.Count - count);
        return [.. _leds];
    }
}
