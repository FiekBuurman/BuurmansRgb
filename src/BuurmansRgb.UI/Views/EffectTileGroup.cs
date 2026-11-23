using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace BuurmansRgb.UI.Views;

// The effect tiles on Kleur (DESIGN.md § 9.3, 9.4 and 9.8): one group of radio buttons "Effect" with
// one Tab stop, the chosen tile. On the group KeyboardNavigation.TabNavigation is Once; as Once lands
// on the tile that had the focus last, not on the chosen one, the group moves the focus to the chosen
// tile when it comes in from another control by the keyboard (a click focuses the tile it chooses;
// focus that comes back after Alt+Tab or that UI Automation sets stays on its tile). The
// group itself takes the focus too (Focusable, no Tab stop) and hands it to the chosen tile, so
// FocusFallback can point to it. ←, →, ↑ and ↓ go to the tile before or after, round, and Home and
// End to the first and the last; they choose that tile at once, as radio buttons do, except a tile
// that may not choose (EffectTile.CanChoose).
public sealed class EffectTileGroup : ItemsControl
{
    // The tiles in the order of the group.
    internal List<EffectTile> Tiles() => this.Containers<EffectTile>();

    internal EffectTile? ChosenTile() => Tiles().FirstOrDefault(tile => tile.IsChecked == true);

    // Focuses the chosen tile, or the first one without a choice.
    public bool FocusChosen() => (ChosenTile() ?? Tiles().FirstOrDefault())?.Focus() == true;

    protected override AutomationPeer OnCreateAutomationPeer() => new EffectTileGroupAutomationPeer(this);

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (ReferenceEquals(e.NewFocus, this))
        {
            FocusChosen();
            return;
        }

        // Only Tab (or Shift+Tab) from another control moves the focus on. Not when the focus comes
        // back without an element before it, as when the window is active again after Alt+Tab; not
        // when UI Automation, such as a screen reader, puts the focus on this very tile; and not
        // for a click, which focuses the tile it chooses.
        if (e.OldFocus is not DependencyObject old || ReferenceEquals(old, this) || IsAncestorOf(old))
        {
            return;
        }

        if (Mouse.LeftButton == MouseButtonState.Pressed || e.NewFocus is not EffectTile { IsChecked: not true, IsFocusedByAutomation: false })
        {
            return;
        }

        ChosenTile()?.Focus();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (this.TargetOf<EffectTile>(e, upDownMoves: true) is not { } tile)
        {
            return;
        }

        e.Handled = true;
        tile.Focus();
        if (tile.CanChoose)
        {
            tile.Choose();
        }
    }

    // A group with the tiles as its children, not a list: an ItemsControl says nothing by itself, and
    // its items would make "lijstitem" of the radio buttons. The name ("Effect") comes from
    // AutomationProperties.Name.
    private sealed class EffectTileGroupAutomationPeer(EffectTileGroup owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(EffectTileGroup);
    }
}
