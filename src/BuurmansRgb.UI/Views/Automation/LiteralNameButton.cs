using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace BuurmansRgb.UI.Views;

// A button with a fixed text, such as "Knipperen", and a name for a screen reader with a name from
// OpenRGB or of the user in it, such as "Zone ARGB_V2_1 van ... laten knipperen": the name is read as
// it is set, underscores and all (LiteralName), where a Button would drop the first "_".
public sealed class LiteralNameButton : Button
{
    protected override AutomationPeer OnCreateAutomationPeer() => new LiteralNameButtonAutomationPeer(this);

    private sealed class LiteralNameButtonAutomationPeer(LiteralNameButton owner) : ButtonAutomationPeer(owner)
    {
        protected override string GetNameCore() => LiteralName.Of(owner) ?? base.GetNameCore();
    }
}
