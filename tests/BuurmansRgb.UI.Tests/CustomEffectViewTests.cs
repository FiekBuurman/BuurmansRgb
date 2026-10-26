using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The panel "Eigen effect" on the real ColorPage, in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class CustomEffectViewTests
{
    private static readonly CustomEffect Disco = new("custom:disco", "Disco",
        [new CustomEffectStep(Red, 1, 0), new CustomEffectStep([Green, Blue], 1, 0), new CustomEffectStep(Blue, 1, 0)]);

    // Each step a tile with its name and help for a screen reader; a pattern as a gradient.
    [Fact]
    public async Task Panel_OwnEffect_ShowsTheStepsWithTheirNames()
    {
        using var viewModel = await ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = ToolboxViewTests.Show(panel, viewModel);
            var tiles = StepTiles(page);

            Assert.True(Panel(page).IsVisible);
            Assert.Equal(viewModel.CustomEffectEditor.Steps.Select(step => step.Name), tiles.Select(AutomationProperties.GetName));
            Assert.All(tiles, tile => Assert.Equal(Strings.CustomEffectStepHelp, AutomationProperties.GetHelpText(tile)));
            Assert.IsType<LinearGradientBrush>(tiles[1].Background);

            // The tiles of a list of colors: 44 wide, 8 apart, the selected one marked.
            Assert.All(tiles, tile => Assert.Equal((44.0, new Thickness(0, 0, 8, 8)), (tile.ActualWidth, tile.Margin)));
            Assert.Equal(viewModel.CustomEffectEditor.Steps.Select(step => (object)step.IsSelected), tiles.Select(tile => tile.Tag));
            Assert.Equal(Strings.CustomEffectNameAccessibleName, AutomationProperties.GetName(NameBox(page)));
        });
    }

    [Fact]
    public async Task Panel_OtherEffect_IsHidden()
    {
        using var viewModel = await ConnectAsync(effectId: EffectIds.Static);

        AppTheme.Run(panel =>
        {
            var page = ToolboxViewTests.Show(panel, viewModel);

            Assert.False(Panel(page).IsVisible);
        });
    }

    // The focus goes to the step that takes the place of the one taken away.
    [Fact]
    public async Task StepTile_Delete_TakesTheStepAwayAndFocusesTheNext()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await ConnectAsync(api => engine = api);

        AppTheme.Run(panel =>
        {
            var page = ToolboxViewTests.Show(panel, viewModel);
            var tile = StepTiles(page)[1];
            Assert.True(tile.Focus());

            tile.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(tile), 0, Key.Delete) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            OffscreenWindow.WaitUntilIdle();

            var tiles = StepTiles(page);
            Assert.Equal(2, tiles.Count);
            Assert.True(tiles[1].IsKeyboardFocused);
            Assert.Equal(["custom-effect save custom:disco Disco #FF0000/1/0 #0000FF/1/0"], engine!.Calls);
        });
    }

    // "−" turns itself off at the last step; the focus goes to that step instead of nowhere.
    [Fact]
    public async Task RemoveButton_LastButOneStep_GivesTheFocusToTheStepLeft()
    {
        using var viewModel = await ConnectAsync(effect: Disco with { Steps = [.. Disco.Steps.Take(2)] });

        AppTheme.Run(panel =>
        {
            var page = ToolboxViewTests.Show(panel, viewModel);
            var remove = ButtonNamed(page, Strings.CustomEffectStepRemove);
            Assert.True(remove.Focus());

            remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            viewModel.CustomEffectEditor.RemoveStepCommand.Execute(null);
            OffscreenWindow.WaitUntilIdle();

            Assert.False(remove.IsEnabled);
            Assert.True(Assert.Single(StepTiles(page)).IsKeyboardFocused);
        });
    }

    // The panel hides; the focus goes to the effect choice, which now says Statisch.
    [Fact]
    public async Task DeleteButton_GivesTheFocusToTheEffectChoice()
    {
        using var viewModel = await ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = ToolboxViewTests.Show(panel, viewModel);
            var delete = ButtonNamed(page, Strings.CustomEffectDeleteAccessibleName);
            Assert.True(delete.Focus());

            ToolboxViewTests.Click(delete);

            Assert.False(Panel(page).IsVisible);
            var choice = AppTheme.Descendants<ComboBox>(page).Single(box => AutomationProperties.GetName(box) == Strings.EffectHeader);
            Assert.True(choice.IsKeyboardFocusWithin);
            Assert.Equal(EffectIds.Static, viewModel.SelectedEffect?.Id);
        });
    }

    // Enter keeps the name without leaving the box.
    [Fact]
    public async Task NameBox_Enter_SendsTheName()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await ConnectAsync(api => engine = api);

        AppTheme.Run(panel =>
        {
            var page = ToolboxViewTests.Show(panel, viewModel);
            var box = NameBox(page);
            Assert.True(box.Focus());
            box.Text = "Feest";

            box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(box), 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal("Feest", viewModel.SelectedEffect?.DisplayName);
            Assert.Single(engine!.Calls);
            Assert.True(box.IsKeyboardFocused);
        });
    }

    // Another name moves the effect to its place in the alphabet; it stays chosen, and the box keeps
    // the focus.
    [Fact]
    public async Task NameBox_NameThatMovesTheEffect_KeepsItChosen()
    {
        using var viewModel = await ConnectAsync(api => api.CustomEffects = [new CustomEffect("custom:avond", "Avond", [new CustomEffectStep(Red)]), Disco]);

        AppTheme.Run(panel =>
        {
            var page = ToolboxViewTests.Show(panel, viewModel);
            var box = NameBox(page);
            Assert.True(box.Focus());
            box.Text = "Aaa";

            box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(box), 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
            OffscreenWindow.WaitUntilIdle();

            var choice = AppTheme.Descendants<ComboBox>(page).Single(combo => AutomationProperties.GetName(combo) == Strings.EffectHeader);
            Assert.Equal(["Aaa", "Avond"], viewModel.Effects.Where(effect => effect.IsCustom).Select(effect => effect.DisplayName));
            Assert.Equal(Disco.Id, Assert.IsType<EffectOption>(choice.SelectedItem).Id);
            Assert.True(Panel(page).IsVisible);
            Assert.True(box.IsKeyboardFocused);
        });
    }

    [Fact]
    public async Task NewButton_MakesAnEffectAndShowsThePanel()
    {
        using var viewModel = await ConnectAsync(effectId: EffectIds.Static);

        AppTheme.Run(panel =>
        {
            var page = ToolboxViewTests.Show(panel, viewModel);

            ToolboxViewTests.Click(AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, Strings.CustomEffectNew)));

            Assert.True(Panel(page).IsVisible);
            Assert.Equal(UiText.Format(Strings.CustomEffectNewName, 1), NameBox(page).Text);
        });
    }

    private static Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null, CustomEffect? effect = null, string? effectId = null) =>
        ToolboxViewTests.ConnectAsync(api =>
        {
            effect ??= Disco;
            api.Effects = [EffectIds.Static, EffectIds.Rainbow];
            api.CustomEffects = [effect];
            api.Lighting[""] = api.Lighting[""] with { EffectId = effectId ?? effect.Id };
            arrange?.Invoke(api);
        });

    private static Border Panel(DependencyObject page) =>
        AppTheme.Descendants<Border>(page).Single(border => border.Name == "CustomEffectPanel");

    private static List<Button> StepTiles(DependencyObject page) =>
        [.. AppTheme.Descendants<Button>(page).Where(button => button.DataContext is CustomEffectStepItem)];

    private static TextBox NameBox(DependencyObject page) =>
        AppTheme.Descendants<TextBox>(page).Single(box => AutomationProperties.GetName(box) == Strings.CustomEffectNameAccessibleName);

    private static Button ButtonNamed(DependencyObject page, string name) =>
        AppTheme.Descendants<Button>(page).Single(button => AutomationProperties.GetName(button) == name);
}
