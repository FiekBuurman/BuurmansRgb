using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BuurmansRgb.UI.Views;

// The arrow keys of a row of controls with one Tab stop, such as the chip row (DeviceChipRow) and
// the effect tiles (EffectTileGroup): which control of the row holds the focus, and where an arrow,
// Home or End takes it, from the last back to the first.
internal static class RovingFocus
{
    // The first T in each container of the items, in the order of the items.
    internal static List<T> Containers<T>(this ItemsControl items) where T : DependencyObject =>
        [.. Enumerable.Range(0, items.Items.Count)
            .Select(items.ItemContainerGenerator.ContainerFromIndex)
            .OfType<DependencyObject>()
            .Select(VisualTreeSearch.FirstDescendant<T>)
            .OfType<T>()];

    // The place of the control that is the source of a key or holds it; -1 when none does.
    internal static int IndexHolding<T>(this List<T> controls, DependencyObject source) where T : Visual =>
        controls.FindIndex(control => control == source || control.IsAncestorOf(source));

    // Where a key takes the focus from current, of count: ← and → the one before and after, round;
    // Home and End the first and the last. ↑ and ↓ go along as ← and → when upDownMoves, and else
    // stay on current (they are handled, but move nothing). Null for any other key.
    internal static int? NextIndex(int count, int current, Key key, bool upDownMoves) => key switch
    {
        Key.Left => Previous(count, current),
        Key.Right => (current + 1) % count,
        Key.Up => upDownMoves ? Previous(count, current) : current,
        Key.Down => upDownMoves ? (current + 1) % count : current,
        Key.Home => 0,
        Key.End => count - 1,
        _ => null,
    };

    private static int Previous(int count, int current) => (current - 1 + count) % count;
}
