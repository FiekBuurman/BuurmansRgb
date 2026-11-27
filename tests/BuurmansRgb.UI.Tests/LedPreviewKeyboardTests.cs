using System.Windows.Input;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// What a key does in the preview of a zone, without a view; the view running KeyCommand with it is
// in LedPreviewViewKeyboardTests.
public sealed class LedPreviewKeyboardTests
{
    public static TheoryData<Key, ModifierKeys, LedKey, bool> Keys => new()
    {
        { Key.Left, ModifierKeys.None, LedKey.Previous, false },
        { Key.Right, ModifierKeys.Shift, LedKey.Next, true },
        { Key.Up, ModifierKeys.None, LedKey.Up, false },
        { Key.Down, ModifierKeys.Shift, LedKey.Down, true },
        { Key.Home, ModifierKeys.None, LedKey.First, false },
        { Key.End, ModifierKeys.Shift, LedKey.Last, true },
        { Key.Space, ModifierKeys.None, LedKey.Select, false },
        { Key.Enter, ModifierKeys.None, LedKey.Select, false },
        { Key.Enter, ModifierKeys.Shift, LedKey.Select, true },
        { Key.A, ModifierKeys.Control, LedKey.SelectAll, false },
        { Key.Space, ModifierKeys.Control, LedKey.Toggle, false },
        { Key.Right, ModifierKeys.Control, LedKey.Next, false },
        { Key.Up, ModifierKeys.Control, LedKey.Up, false },
        { Key.Home, ModifierKeys.Control, LedKey.First, false },
        { Key.Escape, ModifierKeys.None, LedKey.ClearSelection, false },
    };

    [Theory]
    [MemberData(nameof(Keys))]
    public void KeyPressOf_KeysOfThePreview(Key key, ModifierKeys modifiers, LedKey expected, bool extend) =>
        Assert.Equal(new LedKeyPress(expected, extend), LedPreviewKeyboard.KeyPressOf(key, modifiers));

    // Tab, letters and keys with Ctrl or Alt stay with the window, such as Ctrl+Tab.
    [Theory]
    [InlineData(Key.Tab, ModifierKeys.None)]
    [InlineData(Key.A, ModifierKeys.None)]
    [InlineData(Key.Tab, ModifierKeys.Control)]
    [InlineData(Key.Right, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.Space, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.Left, ModifierKeys.Alt)]
    [InlineData(Key.A, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.Escape, ModifierKeys.Shift)]
    public void KeyPressOf_OtherKeys_IsNull(Key key, ModifierKeys modifiers) =>
        Assert.Null(LedPreviewKeyboard.KeyPressOf(key, modifiers));

    // Six dots in rows of three: LED 1 has LED 4 below it and no row above; Shift still extends.
    [Fact]
    public void PressOf_UpAndDown_AimAtTheLedInTheNextRow()
    {
        var rows = InRowsOfThree(6);

        Assert.Equal(new LedKeyPress(LedKey.Down, Target: 4), LedPreviewKeyboard.PressOf(Key.Down, ModifierKeys.None, 1, () => rows));
        Assert.Equal(new LedKeyPress(LedKey.Up, Extend: true), LedPreviewKeyboard.PressOf(Key.Up, ModifierKeys.Shift, 1, () => rows));
        Assert.Equal(new LedKeyPress(LedKey.Up, Target: 1), LedPreviewKeyboard.PressOf(Key.Up, ModifierKeys.Control, 4, () => rows));
    }

    [Fact]
    public void PressOf_UpAndDownInARing_HaveNoRow() =>
        Assert.Equal(new LedKeyPress(LedKey.Down), LedPreviewKeyboard.PressOf(Key.Down, ModifierKeys.None, 0, () => new RingArrangement(8, 1.0)));

    // The other keys do not need the arrangement, so the view does not make one for them.
    [Theory]
    [InlineData(Key.Right, ModifierKeys.None)]
    [InlineData(Key.Space, ModifierKeys.Control)]
    [InlineData(Key.Tab, ModifierKeys.None)]
    public void PressOf_OtherThanUpAndDown_DoesNotAskTheArrangement(Key key, ModifierKeys modifiers)
    {
        var asked = 0;

        var press = LedPreviewKeyboard.PressOf(key, modifiers, 0, () =>
        {
            asked++;
            return InRowsOfThree(6);
        });

        Assert.Equal(LedPreviewKeyboard.KeyPressOf(key, modifiers), press);
        Assert.Equal(0, asked);
    }

    private static CellArrangement InRowsOfThree(int count) => new(LedPlaces.Wrap(count, 3), LedLayout.Pitch, count, keys: false);
}
