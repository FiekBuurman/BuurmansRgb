using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;

namespace BuurmansRgb.UI.Views;

// A repeat button with a fixed text, such as the "−" and "+" of the length of a zone, and a name for a
// screen reader with a name from OpenRGB in it: the name is read as it is set, underscores and all
// (LiteralName), where a RepeatButton would drop the first "_".
public sealed class LiteralNameRepeatButton : RepeatButton
{
    protected override AutomationPeer OnCreateAutomationPeer() => new LiteralNameRepeatButtonAutomationPeer(this);

    private sealed class LiteralNameRepeatButtonAutomationPeer(LiteralNameRepeatButton owner) : RepeatButtonAutomationPeer(owner)
    {
        protected override string GetNameCore() => LiteralName.Of(owner) ?? base.GetNameCore();
    }
}
