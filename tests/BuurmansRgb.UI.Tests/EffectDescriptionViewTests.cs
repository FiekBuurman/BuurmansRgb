using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The sentence under the effect choice on the real ColorPage, in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class EffectDescriptionViewTests
{
    private static readonly string[] AllEffects =
    [
        EffectIds.Static, EffectIds.Breathing, EffectIds.Rainbow, EffectIds.Wave, EffectIds.ColorLoop,
        EffectIds.Twinkle, EffectIds.Comet, EffectIds.Scanner, EffectIds.Fire, EffectIds.PerLed,
    ];

    private static readonly string PerLedDescription = EffectOption.Create(EffectIds.PerLed).Description;

    // Under the choice, and the help of the choice for a screen reader, which reads it once.
    [Fact]
    public async Task Description_ShowsUnderTheChoiceAsItsHelp()
    {
        using var viewModel = await ConnectWithAsync(EffectIds.Scanner);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var choice = PageViews.Named<ComboBox>(page, Strings.EffectHeader);
            var description = VisibleDescription(page, Strings.EffectDescription_scanner);

            Assert.Equal(Strings.EffectDescription_scanner, AutomationProperties.GetHelpText(choice));
            Assert.True(description.TranslatePoint(default, page).Y > choice.TranslatePoint(default, page).Y + choice.ActualHeight - 1);
            Assert.Single(PageViews.AutomationTree(page), peer => peer.GetName() == Strings.EffectDescription_scanner);
        });
    }

    // Another effect, such as from a scene or the tray menu, which the choice then shows.
    [Fact]
    public async Task Description_AnotherEffect_ChangesAlong()
    {
        using var viewModel = await ConnectWithAsync(EffectIds.Scanner);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);

            viewModel.SelectedEffect = viewModel.Effects.Single(effect => effect.Id == EffectIds.PerLed);
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(PerLedDescription, AutomationProperties.GetHelpText(PageViews.Named<ComboBox>(page, Strings.EffectHeader)));
            Assert.True(VisibleDescription(page, PerLedDescription).IsVisible);
            Assert.DoesNotContain(AppTheme.Descendants<TextBlock>(page), text => text.IsVisible && text.Text == Strings.EffectDescription_scanner);
        });
    }

    // Narrow, so the sentences wrap: the sliders and "+ Eigen effect maken" stay where they are in
    // "Licht en beweging" for every effect, an own one too, and the card keeps its height, as the
    // room of the longest sentence is kept. The card itself moves with the panels above it, such as
    // the color loop, so the places count from its heading.
    [Fact]
    public async Task Description_EveryEffect_MovesNothing()
    {
        using var viewModel = await ConnectWithAsync(EffectIds.Static);

        AppTheme.Run(panel =>
        {
            Window.GetWindow(panel)!.Width = 1000;
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

    // Where the brightness, the speed and "+ Eigen effect maken" are in the card, and how high it
    // is, to a tenth of a pixel.
    private static string Places(FrameworkElement page)
    {
        var heading = AppTheme.Descendants<TextBlock>(page).Single(text => text.Text == Strings.LightingHeader);
        var card = (FrameworkElement)heading.Parent;
        string At(FrameworkElement element)
        {
            var point = element.TranslatePoint(default, heading);
            return FormattableString.Invariant($"{point.X:0.0},{point.Y:0.0}");
        }

        var newEffect = AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, Strings.CustomEffectNew));
        return string.Join(
            " ",
            At(PageViews.Named<Slider>(page, Strings.BrightnessHeader)),
            At(PageViews.Named<Slider>(page, Strings.SpeedHeader)),
            At(newEffect),
            FormattableString.Invariant($"{card.ActualHeight:0.0}"));
    }

    private static Task<MainViewModel> ConnectWithAsync(string effectId) => PageViews.ConnectAsync(api =>
    {
        api.Effects = AllEffects;
        api.CustomEffects = [new CustomEffect("custom:disco", "Disco", [new CustomEffectStep(Red, 1, 0), new CustomEffectStep(Blue, 1, 0)])];
        api.Lighting[""] = api.Lighting[""] with { EffectId = effectId };
    });
}
