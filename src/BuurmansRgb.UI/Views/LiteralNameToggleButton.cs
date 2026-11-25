using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;

namespace BuurmansRgb.UI.Views;

// A toggle button with a fixed text, such as "Uitleg" of the status line, and a name for a screen
// reader with another name in it, such as that of other RGB software: the name is read as it is set,
// underscores and all (LiteralName), where a ToggleButton would drop the first "_".
public sealed class LiteralNameToggleButton : ToggleButton
{
    protected override AutomationPeer OnCreateAutomationPeer() => new LiteralNameToggleButtonAutomationPeer(this);

    private sealed class LiteralNameToggleButtonAutomationPeer(LiteralNameToggleButton owner) : ToggleButtonAutomationPeer(owner)
    {
        protected override string GetNameCore() => LiteralName.Of(owner) ?? base.GetNameCore();
    }
}
