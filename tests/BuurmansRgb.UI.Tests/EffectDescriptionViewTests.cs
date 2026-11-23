using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The sentence under the effect tiles on the real ColorPage, in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class EffectDescriptionViewTests
{
    private static readonly string[] AllEffects =
    [
        EffectIds.Static, EffectIds.Breathing, EffectIds.Rainbow, EffectIds.Wave, EffectIds.ColorLoop,
        EffectIds.Twinkle, EffectIds.Comet, EffectIds.Scanner, EffectIds.Fire, EffectIds.PerLed,
    ];

    private static readonly string PerLedDescription = EffectOption.Create(EffectIds.PerLed).Description;

    // Under the tiles, and the help of each tile for a screen reader, so it hears what an effect does
    // before it chooses it.
    [Fact]
    public async Task Description_ShowsUnderTheTilesAndIsTheHelpOfEachTile()
    {
        using var viewModel = await ConnectWithAsync(EffectIds.Scanner);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var group = AppTheme.Descendants<EffectTileGroup>(page).Single();
            var description = VisibleDescription(page, Strings.EffectDescription_scanner);

            Assert.True(description.TranslatePoint(default, page).Y > group.TranslatePoint(default, page).Y + group.ActualHeight - 1);
            Assert.Equal(
                [.. AllEffects.Select(id => EffectOption.Create(id).Description)],
                AppTheme.Descendants<EffectTile>(page).Take(AllEffects.Length).Select(AutomationProperties.GetHelpText));
        });
    }

    // Another effect, such as from a scene or the tray menu, which the tiles then show.
    [Fact]
    public async Task Description_AnotherEffect_ChangesAlong()
    {
        using var viewModel = await ConnectWithAsync(EffectIds.Scanner);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);

            viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == EffectIds.PerLed);
            OffscreenWindow.WaitUntilIdle();

            Assert.True(VisibleDescription(page, PerLedDescription).IsVisible);
            Assert.DoesNotContain(AppTheme.Descendants<TextBlock>(page), text => text.IsVisible && text.Text == Strings.EffectDescription_scanner);
            Assert.Equal(PerLedDescription, AutomationProperties.GetHelpText(AppTheme.Descendants<EffectTile>(page).Single(tile => tile.IsChecked == true)));
        });
    }

    // Narrow, so the sentences wrap: the tiles, "Importeren…" and "Code plakken" stay where they are
    // in the card Effect for every effect, an own one too, and the card keeps its height, as the room
    // of the longest sentence is kept. Also in the smallest window (960 × 640), where the sentences
    // wrap the most.
    [Theory]
    [InlineData(960)]
    [InlineData(1000)]
    public async Task Description_EveryEffect_MovesNothing(double windowWidth)
    {
        using var viewModel = await ConnectWithAsync(EffectIds.Static);

        AppTheme.Run(panel =>
        {
            Window.GetWindow(panel)!.Width = PageViews.PageWidth(windowWidth);
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var places = new List<(string Effect, string Place)>();

            foreach (var effect in viewModel.Effects)
            {
                viewModel.SelectedEffect = effect;
                OffscreenWindow.WaitUntilIdle();
                Assert.True(VisibleDescription(page, effect.Description).IsVisible);
                places.Add((effect.Id, Places(page)));
            }

            Assert.Equal(AllEffects.Length + 1, places.Count);
            Assert.All(places, place => Assert.Equal(places[0].Place, place.Place));
        });
    }

    private static TextBlock VisibleDescription(DependencyObject page, string text) =>
        AppTheme.Descendants<TextBlock>(page).Single(block => block.IsVisible && block.Text == text);

    // Where the last tile and the two links are in the card Effect, and how high it is, to a tenth of
    // a pixel.
    private static string Places(FrameworkElement page)
    {
        var heading = AppTheme.Descendants<TextBlock>(page).Single(text => text.Text == Strings.EffectHeader);
        var card = (FrameworkElement)((FrameworkElement)heading.Parent).Parent;
        string At(FrameworkElement element)
        {
            var point = element.TranslatePoint(default, heading);
            return FormattableString.Invariant($"{point.X:0.0},{point.Y:0.0}");
        }

        return string.Join(
            " ",
            At(AppTheme.Descendants<EffectTile>(page).Last()),
            At(PageViews.Named<LinkButton>(page, Strings.ShareImportAccessibleName)),
            At(PageViews.Named<LinkButton>(page, Strings.SharePasteCodeAccessibleName)),
            FormattableString.Invariant($"{card.ActualHeight:0.0}"));
    }

    private static Task<MainViewModel> ConnectWithAsync(string effectId) => PageViews.ConnectAsync(api =>
    {
        api.Effects = AllEffects;
        api.CustomEffects = [new CustomEffect("custom:disco", "Disco", [new CustomEffectStep(Red, 1, 0), new CustomEffectStep(Blue, 1, 0)])];
        api.Lighting[""] = api.Lighting[""] with { EffectId = effectId };
    });
}
