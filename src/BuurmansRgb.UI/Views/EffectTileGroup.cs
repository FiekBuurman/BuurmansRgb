using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace BuurmansRgb.UI.Views;

// The effect tiles on Kleur (DESIGN.md § 9.3, 9.4 and 9.8): one group of radio buttons "Effect" with
// one Tab stop, the chosen tile. On the group KeyboardNavigation.TabNavigation is Once; as Once lands
// on the tile that had the focus last, not on the chosen one, the group moves the focus to the chosen
// tile when it comes in from outside by the keyboard (a click focuses the tile it chooses). The
// group itself takes the focus too (Focusable, no Tab stop) and hands it to the chosen tile, so
// FocusFallback can point to it. ←, →, ↑ and ↓ go to the tile before or after, round, and Home and
// End to the first and the last; they choose that tile at once, as radio buttons do, except a tile
// that may not choose (EffectTile.CanChoose).
public sealed class EffectTileGroup : ItemsControl
{
    // The tiles in the order of the group.
    internal List<EffectTile> Tiles() =>
        [.. Enumerable.Range(0, Items.Count)
            .Select(ItemContainerGenerator.ContainerFromIndex)
            .OfType<DependencyObject>()
            .Select(VisualTreeSearch.FirstDescendant<EffectTile>)
            .OfType<EffectTile>()];

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

        if (e.OldFocus is DependencyObject old && (ReferenceEquals(old, this) || IsAncestorOf(old)))
        {
            return;
        }

        if (Mouse.LeftButton == MouseButtonState.Pressed || e.NewFocus is not EffectTile { IsChecked: not true })
        {
            return;
        }

        ChosenTile()?.Focus();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled || Keyboard.Modifiers != ModifierKeys.None || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var tiles = Tiles();
        var current = tiles.FindIndex(tile => tile == source || tile.IsAncestorOf(source));
        if (current < 0)
        {
            return;
        }

        int? next = e.Key switch
        {
            Key.Left or Key.Up => (current - 1 + tiles.Count) % tiles.Count,
            Key.Right or Key.Down => (current + 1) % tiles.Count,
            Key.Home => 0,
            Key.End => tiles.Count - 1,
            _ => null,
        };
        if (next is not { } index)
        {
            return;
        }

        e.Handled = true;
        var tile = tiles[index];
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
