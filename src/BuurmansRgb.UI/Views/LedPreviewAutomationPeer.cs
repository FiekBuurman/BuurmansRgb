using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace BuurmansRgb.UI.Views;

// Shows a LedPreviewView to UI Automation as a list with an item per LED, named like its tooltip,
// so a screen reader can name each key, tell which LEDs are selected and select them without a
// mouse. The items are kept, so a screen reader sees the same item for an LED on every frame.
internal sealed class LedPreviewAutomationPeer(LedPreviewView owner) : FrameworkElementAutomationPeer(owner), ISelectionProvider
{
    private readonly List<LedAutomationPeer> _leds = [];

    // Raises an event of the item of one LED, when a screen reader listens for it. A test, which
    // has no screen reader, hears the events here instead.
    internal Action<LedAutomationPeer, AutomationEvents> RaiseLedEvent { get; set; } = static (item, automationEvent) =>
    {
        if (ListenerExists(automationEvent))
        {
            item.RaiseAutomationEvent(automationEvent);
        }
    };

    // Shift+click selects a range, so more than one LED can be selected.
    public bool CanSelectMultiple => true;

    public bool IsSelectionRequired => false;

    public IRawElementProviderSimple[] GetSelection()
    {
        var view = (LedPreviewView)Owner;
        return [.. GetChildren().OfType<LedAutomationPeer>().Where(led => view.IsSelected(led.Index)).Select(ProviderFromPeer)];
    }

    // The item of the LED says it is no longer selected, or now is.
    public void RaiseSelectionChanged(int led, bool wasSelected)
    {
        if (GetChildren()?.ElementAtOrDefault(led) is LedAutomationPeer item)
        {
            item.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, wasSelected, !wasSelected);
        }
    }

    // The item of the active LED now has the focus, so a screen reader says its name and whether
    // it is selected.
    public void RaiseLedFocused(int led)
    {
        if (GetChildren()?.ElementAtOrDefault(led) is LedAutomationPeer item)
        {
            RaiseLedEvent(item, AutomationEvents.AutomationFocusChanged);
        }
    }

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Selection ? this : base.GetPattern(patternInterface);

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
