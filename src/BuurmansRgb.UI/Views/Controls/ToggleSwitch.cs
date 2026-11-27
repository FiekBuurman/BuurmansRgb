using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.Views;

// A switch (style Switch or LargeSwitch) that a screen reader reads as a switch that is on or off,
// as the ToggleSwitch of WinUI, instead of a toggle button that is pressed or not. UI Automation sees
// a button with the Toggle pattern, as for a ToggleButton, but with the class name of the switch of
// WinUI, which Verteller reads as on or off, and "schakelaar" as its type. The "Aan" or "Uit" beside
// the track is decorative text in the template: the Toggle pattern already says it.
public sealed class ToggleSwitch : ToggleButton
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ToggleSwitchAutomationPeer(this);

    private sealed class ToggleSwitchAutomationPeer(ToggleSwitch owner) : ToggleButtonAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(ToggleSwitch);

        protected override string GetLocalizedControlTypeCore() => Strings.SwitchControlType;
    }
}
