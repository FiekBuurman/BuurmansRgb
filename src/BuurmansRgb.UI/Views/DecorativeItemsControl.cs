using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace BuurmansRgb.UI.Views;

// A row of bulbs that only shows what the lights do, such as the strip of "Hele huis" or of the
// preview on Kleur. UI Automation sees no items in it, so a screen reader does not read out every
// bulb: without AutomationProperties.Name it skips the row, as the text next to it says it in
// words; with one, such as the bulbs of a scene card, it reads the row as one image of that name.
public sealed class DecorativeItemsControl : ItemsControl
{
    protected override AutomationPeer OnCreateAutomationPeer() => new DecorationAutomationPeer(this);

    private sealed class DecorationAutomationPeer(DecorativeItemsControl owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override List<AutomationPeer>? GetChildrenCore() => null;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string GetClassNameCore() => nameof(DecorativeItemsControl);

        protected override bool IsControlElementCore() => HasName;

        protected override bool IsContentElementCore() => HasName;

        private bool HasName => !string.IsNullOrEmpty(AutomationProperties.GetName(Owner));
    }
}
