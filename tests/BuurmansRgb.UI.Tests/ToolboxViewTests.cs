using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// "Kleuren" on the real ColorPage, in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class ToolboxViewTests
{
    // Squares of 32 px instead of the 44 of the design, in rows from left to right: in a wide window
    // the nine of the first row, and the rest under them as far as the column needs; the tiles of a
    // list of colors keep their 44.
    [Theory]
    [InlineData(1400)]
    [InlineData(960)]
    public async Task Presets_AreSmallSquaresInRows(double width)
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            Window.GetWindow(panel)!.Width = width;
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var tiles = PresetTiles(page);
            var places = tiles.Select(tile => tile.TranslatePoint(default, page)).ToList();

            Assert.Equal(ColorPickerViewModel.Presets.Count, tiles.Count);
            Assert.All(tiles, tile => Assert.Equal((32.0, 32.0), (tile.ActualWidth, tile.ActualHeight)));
            Assert.Equal(places, places.OrderBy(place => place.Y).ThenBy(place => place.X));
            if (width >= 1400)
            {
                Assert.Single(places.Take(9).Select(place => place.Y).Distinct());
            }
        });
    }

    // Each tile takes the focus and has its Dutch name for a screen reader; a click mixes its color.
    [Fact]
    public async Task Presets_TakeTheFocusHaveTheirNameAndMixTheirColor()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var tiles = PresetTiles(page);

            Assert.Equal(ColorPickerViewModel.Presets.Select(preset => preset.Name), tiles.Select(AutomationProperties.GetName));
            Assert.True(tiles[8].Focus());
            Assert.True(tiles[8].IsKeyboardFocused);

            PageViews.Click(tiles[8]);

            Assert.Equal(new RgbColor(0, 0, 255), viewModel.ColorPicker.Color);
            Assert.Equal(true, tiles[8].Tag);
        });
    }

    // "+" puts the color of the picker in a tile of its own, with its name for a screen reader.
    [Fact]
    public async Task Add_PutsTheCurrentColorInATile()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var add = AddButton(page);
            viewModel.ColorPicker.HexColor = "#FF8800";

            PageViews.Click(add);

            var tiles = CustomTiles(page);
            Assert.Equal("Eigen kleur: oranje, #FF8800", AutomationProperties.GetName(Assert.Single(tiles)));
            Assert.Equal(Strings.CustomColorHelp, AutomationProperties.GetHelpText(tiles[0]));
            Assert.Equal(32.0, tiles[0].ActualWidth);
            Assert.Equal(true, tiles[0].Tag);
            Assert.False(add.IsEnabled);
            Assert.Equal(Strings.CustomColorsAdd, AutomationProperties.GetName(add));
        });
    }

    // "+" turns itself off after adding the color; a keyboard user goes on from the new tile.
    [Fact]
    public async Task Add_MovesTheFocusToTheNewTile()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.CustomColors = [Red]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var add = AddButton(page);
            viewModel.ColorPicker.HexColor = "#0000FF";
            Assert.True(add.Focus());

            PageViews.Click(add);

            var tiles = CustomTiles(page);
            Assert.Equal(2, tiles.Count);
            Assert.False(add.IsEnabled);
            Assert.True(tiles[1].IsKeyboardFocused);
        });
    }

    // Other colors from the engine, such as after settings.json changed, make the tiles anew; the
    // focus goes back to the tile of the same color.
    [Fact]
    public async Task OtherColorsFromTheEngine_KeepTheFocusOnItsColor()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await PageViews.ConnectAsync(api => (engine = api).CustomColors = [Red, Blue]);
        var green = new RgbColor(0, 255, 0);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            Assert.True(CustomTiles(page)[1].Focus());

            engine!.CustomColors = [green, Red, Blue];
            engine.RaiseOptionsChanged();
            OffscreenWindow.WaitUntilIdle();

            var tiles = CustomTiles(page);
            Assert.Equal([green, Red, Blue], tiles.Select(tile => ((CustomColorItem)tile.DataContext).Color));
            Assert.True(tiles[2].IsKeyboardFocused);
        });
    }

    // Delete takes the focused tile away, and the focus goes to the tile in its place, or to "+".
    [Fact]
    public async Task Delete_RemovesTheTileAndMovesTheFocusOn()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.CustomColors = [Red, Blue]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            Assert.True(CustomTiles(page)[0].Focus());

            PageViews.Press(CustomTiles(page)[0], Key.Delete, Keyboard.PreviewKeyDownEvent);

            var left = Assert.Single(CustomTiles(page));
            Assert.Equal([Blue], viewModel.CustomColors.Colors.Select(item => item.Color));
            Assert.True(left.IsKeyboardFocused);

            PageViews.Press(left, Key.Delete, Keyboard.PreviewKeyDownEvent);

            Assert.Empty(CustomTiles(page));
            Assert.True(AddButton(page).IsKeyboardFocused);
        });
    }

    // "Weghalen" in the context menu of a tile; without a tile in the place of the last one, the
    // focus goes to the one before it.
    [Fact]
    public async Task ContextMenuRemove_RemovesTheTileAndFocusesTheOneBefore()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.CustomColors = [Red, Blue]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var tile = CustomTiles(page)[1];
            var menu = tile.ContextMenu!;
            menu.PlacementTarget = tile;
            var remove = Assert.IsType<MenuItem>(Assert.Single(menu.Items));

            Assert.Equal(Strings.CustomColorRemove, remove.Header);
            remove.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal([Red], viewModel.CustomColors.Colors.Select(item => item.Color));
            Assert.True(Assert.Single(CustomTiles(page)).IsKeyboardFocused);
        });
    }

    internal static List<Button> PresetTiles(DependencyObject page) =>
        [.. AppTheme.Descendants<Button>(page).Where(button => button.DataContext is ColorPreset)];

    // No button puts the color on: a tile, also of the own colors, puts its color on every device
    // with Hele huis, and on the device that is chosen.
    [Fact]
    public async Task Tiles_PutTheirColorOnAtOnce_WithoutAButton()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(fake =>
        {
            api = fake;
            fake.CustomColors = [new RgbColor(1, 2, 3)];
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            api!.Calls.Clear();

            PageViews.Click(PresetTiles(page)[1]);
            Assert.Equal(["color all #FF3B3B"], api.Calls);

            viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "strip");
            OffscreenWindow.WaitUntilIdle();
            api.Calls.Clear();
            PageViews.Click(Assert.Single(CustomTiles(page)));

            Assert.Equal(["color strip #010203"], api.Calls);
            Assert.DoesNotContain(AppTheme.Descendants<Button>(page), button => button.Content is "Op alle apparaten zetten" or "Op het apparaat zetten");
        });
    }

    // With the rainbow, which uses no color of the device, the preview says why a tile changes
    // nothing on the device; the toolbox and "+" stay. With Statisch the message goes and a tile
    // puts its color on again.
    [Fact]
    public async Task Rainbow_ThePreviewSaysWhyATileChangesNothing()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(fake =>
        {
            api = fake;
            fake.Effects = [EffectIds.Static, EffectIds.Rainbow];
            fake.Lighting[""] = fake.Lighting[""] with { EffectId = EffectIds.Rainbow };
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var message = AppTheme.Descendants<TextBlock>(page).Single(block => block.Text == viewModel.DeviceColorUnusedText);
            api!.Calls.Clear();

            PageViews.Click(PresetTiles(page)[1]);

            Assert.True(message.IsVisible);
            Assert.StartsWith("Regenboog", message.Text, StringComparison.Ordinal);
            Assert.Empty(api.Calls);
            Assert.True(AddButton(page).IsEnabled);

            viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == EffectIds.Static);
            OffscreenWindow.WaitUntilIdle();
            api.Calls.Clear();
            PageViews.Click(PresetTiles(page)[2]);

            Assert.False(message.IsVisible);
            Assert.Equal(["color all #FF9A3D"], api.Calls);
        });
    }

    private static List<Button> CustomTiles(DependencyObject page) =>
        [.. AppTheme.Descendants<Button>(page).Where(button => button.DataContext is CustomColorItem)];

    private static Button AddButton(DependencyObject page) =>
        PageViews.ButtonNamed(page, Strings.CustomColorsAdd);
}
