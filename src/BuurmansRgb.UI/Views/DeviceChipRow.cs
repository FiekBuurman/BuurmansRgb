using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace BuurmansRgb.UI.Views;

// The row of chips above the device cards on Kleur ("Naar een apparaat", DESIGN.md § 9.3 and 9.8):
// a chip per device that jumps to its card. One Tab stop, as a toolbar: on the row
// KeyboardNavigation.TabNavigation is Once, so Tab lands on the chip that last had the focus, the one
// of the last jump. ← and → go to the chip before or after, from the last back to the first; Home
// and End to the first and the last. ↑ and ↓ do nothing, also when the chips run over more rows. The
// chips wrap instead of going into an overflow menu as a ToolBar of WPF would, so the row is an
// ItemsControl that UI Automation sees as a toolbar.
public sealed class DeviceChipRow : ItemsControl
{
    protected override AutomationPeer OnCreateAutomationPeer() => new DeviceChipRowAutomationPeer(this);

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled || Keyboard.Modifiers != ModifierKeys.None || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var chips = Chips();
        var current = chips.IndexHolding(source);
        if (current < 0)
        {
            return;
        }

        if (RovingFocus.NextIndex(chips.Count, current, e.Key, upDownMoves: false) is { } index)
        {
            chips[index].Focus();
            e.Handled = true;
        }
    }

    // The chips in the order of the row.
    internal List<ButtonBase> Chips() => this.Containers<ButtonBase>();

    private sealed class DeviceChipRowAutomationPeer(DeviceChipRow owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;

        protected override string GetClassNameCore() => nameof(DeviceChipRow);
    }
}
