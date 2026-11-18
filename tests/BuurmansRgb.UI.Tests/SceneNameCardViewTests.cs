using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The name of a new scene on the real pages, in a window off screen: the same card on Scènes, after
// "+ Nieuwe scène", and on Kleur, after "Bewaar als scène".
[Collection(AppThemeCollection.Name)]
public sealed class SceneNameCardViewTests
{
    // The button has the help of "Bewaar als scène" for a screen reader. The focus goes to the name,
    // and after Enter back to the button, which shows again; else it would be nowhere once the card
    // goes. The scene is saved and on.
    [Fact]
    public async Task ScenesPage_NewSceneSavedWithEnter_GivesTheFocusBackToTheButton()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await PageViews.ConnectAsync(api => engine = api);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ScenesPage>(panel, viewModel);
            var button = AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, Strings.NewScene));
            Assert.Equal(Strings.NewSceneHint, AutomationProperties.GetHelpText(button));
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

    // "Bewaar als scène" opens the card of "+ Nieuwe scène" in its place, with the focus in the name;
    // its name and help tell a screen reader that it keeps all devices. A name a scene already has
    // says "Overschrijven", as on Scènes, and the help of the name says so; Enter saves it over that
    // scene, which is then on, also on Scènes, and the focus goes back to the button.
    [Fact]
    public async Task ColorPage_SaveAsSceneOverAScene_PutsItOnAndGivesTheFocusBack()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await PageViews.ConnectAsync(api =>
        {
            engine = api;
            api.Profiles.AddRange(["Avond", "Nacht"]);
            api.ActiveProfile = "Nacht";
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var button = SaveAsScene(page);
            Assert.Equal(Strings.SaveAsScene, new ButtonAutomationPeer(button).GetName());
            Assert.Equal(Strings.NewSceneHint, AutomationProperties.GetHelpText(button));
            Assert.True(PageViews.ReachedWithTab(page, button));

            PageViews.Click(button);
            var box = NameBox(page);
            Assert.True(box.IsKeyboardFocused);
            Assert.False(button.IsVisible);
            Assert.True(SaveButton(page, Strings.ProfileSave).IsVisible);
            box.Text = "Avond";
            OffscreenWindow.WaitUntilIdle();
            Assert.True(SaveButton(page, Strings.ProfileOverwrite).IsVisible);
            Assert.Equal(UiText.Format(Strings.SceneNameOverwrites, "Avond"), AutomationProperties.GetHelpText(box));
            PageViews.Press(box, Key.Enter);

            Assert.Equal(["profile save Avond"], engine!.Calls.TakeLast(1));
            Assert.Equal("Avond", viewModel.Profiles.ActiveName);
            Assert.Equal([true, false], viewModel.Profiles.Cards.Select(card => card.IsActive));
            Assert.Equal(UiText.Format(Strings.ProfileSaved, "Avond"), viewModel.StatusMessage);
            Assert.True(button.IsVisible);
            Assert.True(button.IsKeyboardFocused);

            panel.Children.Clear();
            var scenes = PageViews.Show<ScenesPage>(panel, viewModel);
            Assert.Equal(Strings.SceneActiveStatus, AutomationProperties.GetItemStatus(PageViews.ButtonNamed(scenes, UiText.Format(Strings.SceneLoadAccessibleName, "Avond"))));
            Assert.Equal(string.Empty, AutomationProperties.GetItemStatus(PageViews.ButtonNamed(scenes, UiText.Format(Strings.SceneLoadAccessibleName, "Nacht"))));
        });
    }

    // Escape, as Annuleren, saves nothing; the focus goes back to the button.
    [Fact]
    public async Task ColorPage_SaveAsSceneCancelled_SavesNothingAndGivesTheFocusBack()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await PageViews.ConnectAsync(api => engine = api);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var button = SaveAsScene(page);
            PageViews.Click(button);
            var box = NameBox(page);
            box.Text = "Klusmodus";

            PageViews.Press(box, Key.Escape);

            Assert.DoesNotContain(engine!.Calls, call => call.StartsWith("profile save", StringComparison.Ordinal));
            Assert.False(box.IsVisible);
            Assert.True(button.IsKeyboardFocused);
        });
    }

    // One card: a name half typed on Kleur is still there on Scènes, in place of "+ Nieuwe scène",
    // and Escape there closes it without saving.
    [Fact]
    public async Task ColorPageThenScenesPage_KeepsTheCardWithTheName()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await PageViews.ConnectAsync(api => engine = api);

        AppTheme.Run(panel =>
        {
            var color = PageViews.Show<ColorPage>(panel, viewModel);
            PageViews.Click(SaveAsScene(color));
            NameBox(color).Text = "Klusmodus";
            OffscreenWindow.WaitUntilIdle();

            panel.Children.Clear();
            var scenes = PageViews.Show<ScenesPage>(panel, viewModel);
            var box = NameBox(scenes);
            var newScene = SaveButton(scenes, Strings.NewScene);
            Assert.True(box.IsVisible);
            Assert.Equal("Klusmodus", box.Text);
            Assert.False(newScene.IsVisible);

            Assert.True(box.Focus());
            PageViews.Press(box, Key.Escape);

            Assert.DoesNotContain(engine!.Calls, call => call.StartsWith("profile save", StringComparison.Ordinal));
            Assert.False(box.IsVisible);
            Assert.True(newScene.IsVisible);
            Assert.True(newScene.IsKeyboardFocused);
        });
    }

    private static Button SaveAsScene(DependencyObject page) => SaveButton(page, Strings.SaveAsScene);

    private static TextBox NameBox(DependencyObject page) => PageViews.Named<TextBox>(page, Strings.ProfileNameLabel);

    private static Button SaveButton(DependencyObject page, string label) =>
        AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, label));
}
