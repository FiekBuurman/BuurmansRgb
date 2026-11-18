using System.Windows.Controls;
using System.Windows.Input;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The name of a new scene on the real pages, in a window off screen: the same card on Scènes, after
// "+ Nieuwe scène".
[Collection(AppThemeCollection.Name)]
public sealed class SceneNameCardViewTests
{
    // The focus goes to the name, and after Enter back to the button, which shows again; else it
    // would be nowhere once the card goes. The scene is saved and on.
    [Fact]
    public async Task ScenesPage_NewSceneSavedWithEnter_GivesTheFocusBackToTheButton()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await PageViews.ConnectAsync(api => engine = api);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ScenesPage>(panel, viewModel);
            var button = AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, Strings.NewScene));
            Assert.True(button.Focus());

            PageViews.Click(button);
            var box = NameBox(page);
            Assert.True(box.IsKeyboardFocused);
            box.Text = "Klusmodus";
            PageViews.Press(box, Key.Enter);

            Assert.Equal(["profile save Klusmodus"], engine!.Calls);
            Assert.False(box.IsVisible);
            Assert.True(button.IsVisible);
            Assert.True(button.IsKeyboardFocused);
            Assert.True(Assert.Single(viewModel.Profiles.Cards).IsActive);
        });
    }

    private static TextBox NameBox(System.Windows.DependencyObject page) => PageViews.Named<TextBox>(page, Strings.ProfileNameLabel);
}
