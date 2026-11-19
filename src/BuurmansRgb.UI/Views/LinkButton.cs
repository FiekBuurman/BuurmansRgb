using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace BuurmansRgb.UI.Views;

// The one link of the app (its style in Themes/Controls.xaml, DESIGN.md § 4 "Link"): a button that a
// screen reader reads as a link, as it was a Hyperlink, instead of a button. UI Automation sees the
// control type Hyperlink, which Verteller says as "link", and still the Invoke pattern of a button,
// so a click, Enter, Space and a screen reader all do the same.
public sealed class LinkButton : Button
{
    protected override AutomationPeer OnCreateAutomationPeer() => new LinkButtonAutomationPeer(this);

    private sealed class LinkButtonAutomationPeer(LinkButton owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

        protected override string GetClassNameCore() => nameof(LinkButton);
    }
}
