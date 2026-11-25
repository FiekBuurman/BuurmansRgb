using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace BuurmansRgb.UI.Views;

// The one link of the app (its style in Themes/Controls.xaml, DESIGN.md § 4 "Link"): a button that a
// screen reader reads as a link, as it was a Hyperlink, instead of a button. UI Automation sees the
// control type Hyperlink, which Verteller says as "link", and still the Invoke pattern of a button,
// so a click, Enter, Space and a screen reader all do the same. Its text is shown as it is, and so is
// its name, also a name set on it (LiteralName): the app has no access keys, so an underscore, as in
// a zone name of OpenRGB such as "D_LED1", stays in both, where a button would drop it from its name.
public sealed class LinkButton : Button
{
    protected override AutomationPeer OnCreateAutomationPeer() => new LinkButtonAutomationPeer(this);

    private sealed class LinkButtonAutomationPeer(LinkButton owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

        // A name set on the link wins, then the text of a label it points to (a button of WPF reads its
        // own text before that label), then its own text.
        protected override string GetNameCore() =>
            LiteralName.Of(owner) is { } name ? name
            : AutomationProperties.GetLabeledBy(owner) is { } labeledBy
                && CreatePeerForElement(labeledBy)?.GetName() is { Length: > 0 } label ? label
            : owner.Content is string text ? text
            : base.GetNameCore();

        protected override string GetClassNameCore() => nameof(LinkButton);
    }
}
