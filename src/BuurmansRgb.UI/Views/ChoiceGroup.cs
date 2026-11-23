using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace BuurmansRgb.UI.Views;

// A row of round toggles of one choice, such as "Waar komt de kleur?" in the Per-LED-editor on Kleur.
// A StackPanel has no automation peer, so a name on it never reaches a screen reader; this one is a
// group in UI Automation with the name from AutomationProperties.Name, around its toggles. The
// toggles keep their own Tab stop and need no GroupName: they are one choice by their panel.
public sealed class ChoiceGroup : StackPanel
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ChoiceGroupAutomationPeer(this);

    private sealed class ChoiceGroupAutomationPeer(ChoiceGroup owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(ChoiceGroup);
    }
}
