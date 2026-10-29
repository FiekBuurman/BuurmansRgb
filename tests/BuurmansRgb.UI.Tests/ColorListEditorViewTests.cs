using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The tiles of a list of colors with + and − as they load in a window, and the card "Kleuren van
// het effect" on the Kleur page with its button "Standaardkleuren".
[Collection(AppThemeCollection.Name)]
public sealed class ColorListEditorViewTests
{
    [Fact]
    public void Load_ShowsATileForEveryColorWithTheSelectedOneChosen() => AppTheme.Run(panel =>
    {
        var view = Show(panel, new ColorListEditorViewModel(1, 3, [Red, Blue]));

        var tiles = Tiles(view);

        Assert.Equal([Red, Blue], tiles.Select(tile => ((SolidColorBrush)tile.Background).Color).Select(color => new RgbColor(color.R, color.G, color.B)));
        Assert.Equal([true, false], tiles.Select(tile => tile.Tag));
        Assert.Equal("Kleur 2: echt blauw, #0000FF", AutomationProperties.GetName(tiles[1]));
        Assert.All(tiles, tile => Assert.Equal((44.0, new Thickness(0, 0, 8, 8)), (tile.ActualWidth, tile.Margin)));
    });

    [Fact]
    public void ClickTile_SelectsItsColor() => AppTheme.Run(panel =>
    {
        var editor = new ColorListEditorViewModel(1, 3, [Red, Blue]);
        var view = Show(panel, editor);

        PageViews.Click(Tiles(view)[1]);

        Assert.Equal(1, editor.SelectedIndex);
        Assert.Equal([false, true], Tiles(view).Select(tile => tile.Tag));
    });

    // + adds a copy of the selected color up to the most, − takes it away down to the least.
    [Fact]
    public void ClickAddAndRemove_ChangeTheTiles() => AppTheme.Run(panel =>
    {
        var editor = new ColorListEditorViewModel(1, 3, [Red, Blue]);
        var view = Show(panel, editor);
        var add = PageViews.ButtonNamed(view, Strings.ColorListAdd);
        var remove = PageViews.ButtonNamed(view, Strings.ColorListRemove);

        PageViews.Click(add);

        Assert.Equal([Red, Red, Blue], editor.Colors);
        Assert.Equal(3, Tiles(view).Count);
        Assert.False(add.IsEnabled);

        PageViews.Click(remove);
        PageViews.Click(remove);

        Assert.Equal([Red], editor.Colors);
        Assert.Single(Tiles(view));
        Assert.False(remove.IsEnabled);
        Assert.True(add.IsEnabled);
    });

    // Twelve colors, the most, are two even rows of six, with + and − to the right of the first.
    [Fact]
    public void Load_TwelveColors_LaysThemOutInTwoRowsOfSix() => AppTheme.Run(panel =>
    {
        Window.GetWindow(panel)!.Width = 600;
        var view = Show(panel, new ColorListEditorViewModel(ColorLoop.MinColors, ColorLoop.MaxColors, Twelve));

        var places = Tiles(view).Select(tile => tile.TranslatePoint(default, view)).ToList();
        var add = PageViews.ButtonNamed(view, Strings.ColorListAdd).TranslatePoint(default, view);

        Assert.Equal(12, places.Count);
        Assert.Equal([6, 6], places.GroupBy(place => place.Y).Select(row => row.Count()));
        Assert.Equal(places[0].Y, add.Y);
        Assert.True(add.X > places[5].X);
    });

    // With the comet and a list of its own the card shows, with "Standaardkleuren" on; after it the
    // tiles show the lamp color and the button is off. Without an effect with a list the card goes.
    [Fact]
    public async Task ColorPage_WithCometAndAList_ShowsTheCardAndItsDefaultButton()
    {
        using var viewModel = await ConnectAsync(EffectIds.Comet, EffectColors.None.With(EffectIds.Comet, [Green, Blue]));

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var header = AppTheme.Descendants<TextBlock>(page).Single(text => text.Text == Strings.EffectColorsHeader);
            var reset = AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, Strings.EffectColorsDefault));

            Assert.True(header.IsVisible);
            Assert.True(reset.IsVisible);
            Assert.True(reset.IsEnabled);

            PageViews.Click(reset);

            Assert.False(reset.IsEnabled);
            Assert.Equal([new RgbColor(255, 0, 0)], viewModel.EffectColorsEditor.Colors);

            viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == EffectIds.Static);
            OffscreenWindow.WaitUntilIdle();

            Assert.False(header.IsVisible);
        });
    }

    private static ColorListEditor Show(StackPanel panel, ColorListEditorViewModel editor)
    {
        var view = new ColorListEditor { DataContext = editor };
        panel.Children.Add(view);
        OffscreenWindow.WaitUntilIdle();
        return view;
    }

    private static List<Button> Tiles(ColorListEditor view) =>
        [.. AppTheme.Descendants<Button>(view).Where(button => button.DataContext is ColorSwatch)];

    private static Task<MainViewModel> ConnectAsync(string effectId, EffectColors colors) => PageViews.ConnectAsync(api =>
    {
        api.Effects = [EffectIds.Static, EffectIds.Comet];
        api.Lighting[""] = api.Lighting[""] with { EffectId = effectId, EffectColors = colors };
    });
}
