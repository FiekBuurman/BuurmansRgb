using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace BuurmansRgb.UI.Views;

// Text that only decorates, such as the bullet before a line of a list: it scales with the text
// size of Windows and takes the colors of the contrast theme like other text, but a screen reader
// does not read it out, as the line next to it says what matters. UI Automation sees it only in its
// raw view, as AccessibilityView="Raw" does in UWP and WinUI, which WPF lacks.
public sealed class DecorativeText : TextBlock
{
    protected override AutomationPeer OnCreateAutomationPeer() => new DecorationAutomationPeer(this);

    private sealed class DecorationAutomationPeer(DecorativeText owner) : TextBlockAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(DecorativeText);

        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}
