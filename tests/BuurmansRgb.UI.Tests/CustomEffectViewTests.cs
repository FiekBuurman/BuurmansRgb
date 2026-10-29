using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views.Pages;

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
            var page = PageViews.Show<ColorPage>(panel, viewModel);
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
            var page = PageViews.Show<ColorPage>(panel, viewModel);

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
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var tile = StepTiles(page)[1];
            Assert.True(tile.Focus());

            PageViews.Press(tile, Key.Delete, Keyboard.PreviewKeyDownEvent);

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
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var remove = PageViews.ButtonNamed(page, Strings.CustomEffectStepRemove);
            Assert.True(remove.Focus());

            remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            viewModel.CustomEffectEditor.RemoveStepCommand.Execute(null);
            OffscreenWindow.WaitUntilIdle();

            Assert.False(remove.IsEnabled);
            Assert.True(Assert.Single(StepTiles(page)).IsKeyboardFocused);
        });
    }

    // "←" and "→" turn themselves off once the step is at an end; the focus goes to that step,
    // which stays selected, instead of nowhere.
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 2)]
    public async Task MoveButton_ToTheEnd_GivesTheFocusToTheMovedStep(bool later, int end)
    {
        using var viewModel = await ConnectAsync();
        var editor = viewModel.CustomEffectEditor;
        editor.SelectStepCommand.Execute(1);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var move = PageViews.ButtonNamed(page, later ? Strings.CustomEffectStepLater : Strings.CustomEffectStepEarlier);
            Assert.True(move.Focus());

            move.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            (later ? editor.MoveStepLaterCommand : editor.MoveStepEarlierCommand).Execute(null);
            OffscreenWindow.WaitUntilIdle();

            Assert.False(move.IsEnabled);
            Assert.Equal(end, editor.SelectedIndex);
            Assert.True(StepTiles(page)[end].IsKeyboardFocused);
        });
    }

    // Confirmed, the panel hides; the focus goes to the effect choice, which now says Statisch.
    [Fact]
    public async Task DeleteButton_GivesTheFocusToTheEffectChoice()
    {
        using var viewModel = await ConnectAsync();
        viewModel.CustomEffectEditor.ConfirmDelete = _ => true;

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var delete = PageViews.ButtonNamed(page, Strings.CustomEffectDeleteAccessibleName);
            Assert.True(delete.Focus());

            PageViews.Click(delete);

            Assert.False(Panel(page).IsVisible);
            var choice = PageViews.Named<ComboBox>(page, Strings.EffectHeader);
            Assert.True(choice.IsKeyboardFocusWithin);
            Assert.Equal(EffectIds.Static, viewModel.SelectedEffect?.Id);
        });
    }

    // Annuleren: the panel stays, and so does the focus on Verwijderen.
    [Fact]
    public async Task DeleteButton_NotConfirmed_KeepsThePanelAndTheFocus()
    {
        using var viewModel = await ConnectAsync();
        viewModel.CustomEffectEditor.ConfirmDelete = _ => false;

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var delete = PageViews.ButtonNamed(page, Strings.CustomEffectDeleteAccessibleName);
            Assert.True(delete.Focus());

            PageViews.Click(delete);

            Assert.True(Panel(page).IsVisible);
            Assert.True(delete.IsKeyboardFocused);
            Assert.Equal(Disco.Id, viewModel.SelectedEffect?.Id);
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
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var box = NameBox(page);
            Assert.True(box.Focus());
            box.Text = "Feest";

            PageViews.Press(box, Key.Enter);

            Assert.Equal("Feest", viewModel.SelectedEffect?.DisplayName);
            Assert.Single(engine!.Calls);
            Assert.True(box.IsKeyboardFocused);
        });
    }

    // Without Enter, the name counts once the focus goes, as with Tab.
    [Fact]
    public async Task NameBox_FocusGoes_SendsTheName()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await ConnectAsync(api => engine = api);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var box = NameBox(page);
            Assert.True(box.Focus());
            box.Text = " Feest ";

            Assert.True(StepTiles(page)[0].Focus());
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal("Feest", viewModel.SelectedEffect?.DisplayName);
            Assert.Equal("Feest", box.Text);
            Assert.Single(engine!.Calls);
        });
    }

    // Escape puts back the name of the effect, typed over with or without Enter, without a message
    // or a name sent; the box keeps the focus, and losing it later refuses nothing.
    [Theory]
    [InlineData("Feest", false)]
    [InlineData("regenboog", true)]
    public async Task NameBox_Escape_PutsBackTheName(string typed, bool enterFirst)
    {
        FakeEngineApi? engine = null;
        using var viewModel = await ConnectAsync(api => engine = api);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var box = NameBox(page);
            Assert.True(box.Focus());
            box.Text = typed;
            if (enterFirst)
            {
                PageViews.Press(box, Key.Enter);
                Assert.NotNull(viewModel.CustomEffectEditor.NameError);
            }

            PageViews.Press(box, Key.Escape);

            Assert.Equal("Disco", box.Text);
            Assert.True(box.IsKeyboardFocused);
            Assert.Null(viewModel.CustomEffectEditor.NameError);

            Assert.True(StepTiles(page)[0].Focus());
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal("Disco", viewModel.SelectedEffect?.DisplayName);
            Assert.Null(viewModel.CustomEffectEditor.NameError);
            Assert.Empty(engine!.Calls);
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
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var box = NameBox(page);
            Assert.True(box.Focus());
            box.Text = "Aaa";

            PageViews.Press(box, Key.Enter);

            var choice = PageViews.Named<ComboBox>(page, Strings.EffectHeader);
            Assert.Equal(["Aaa", "Avond"], viewModel.Effects.Where(effect => effect.IsCustom).Select(effect => effect.DisplayName));
            Assert.Equal(Disco.Id, Assert.IsType<EffectOption>(choice.SelectedItem).Id);
            Assert.True(Panel(page).IsVisible);
            Assert.True(box.IsKeyboardFocused);
        });
    }

    // An empty name or that of another effect says why under the box, also to a screen reader, and
    // the box keeps it while it has the focus, so the user can change it; once the focus goes, the
    // box shows the name the effect choice shows, and the message says which name was refused and
    // that Disco stays. A name typed without Enter gives that one message only.
    [Theory]
    [InlineData("", " ")]
    [InlineData("regenboog", "STATISCH")]
    public async Task NameBox_NameNotKept_SaysWhyAndShowsTheNameAgainWhenTheFocusGoes(string name, string other)
    {
        FakeEngineApi? engine = null;
        using var viewModel = await ConnectAsync(api => engine = api);
        var text = name.Length == 0 ? Strings.CustomEffectNameInvalid : Strings.CustomEffectNameTaken;
        string Kept(string refused) => refused.Trim().Length == 0
            ? UiText.Format(Strings.CustomEffectNameEmptyKept, "Disco")
            : UiText.Format(Strings.CustomEffectNameTakenKept, refused, "Disco");
        using var live = LiveRegionCapture.Start();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var box = NameBox(page);
            Assert.True(box.Focus());
            box.Text = name;

            PageViews.Press(box, Key.Enter);

            var error = AppTheme.Descendants<TextBlock>(page).Single(block => block.Text == text);
            Assert.True(error.IsVisible);
            Assert.Equal([error], live.Announced);
            Assert.Equal(name, box.Text);

            Assert.True(StepTiles(page)[0].Focus());
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal("Disco", box.Text);
            Assert.True(error.IsVisible);
            Assert.Equal(Kept(name), error.Text);
            Assert.Equal([error, error], live.Announced);
            Assert.Equal("Disco", viewModel.SelectedEffect?.DisplayName);
            Assert.Empty(engine!.Calls);

            // Another name refused without Enter is said once, also with the same message.
            Assert.True(box.Focus());
            box.Text = other;
            Assert.True(StepTiles(page)[0].Focus());
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(Kept(other), error.Text);
            Assert.Equal([error, error, error], live.Announced);
            Assert.Equal("Disco", box.Text);
            Assert.Empty(engine.Calls);
        });
    }

    // With Hele huis there are no LED colors of one device to take, so the link is off, and looks
    // it: grey and faded. With a device it is a blue link again.
    [Fact]
    public async Task TakePattern_WholeHouse_LooksDisabled()
    {
        using var viewModel = await ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var take = AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, Strings.CustomEffectTakePattern));

            Assert.False(take.IsEnabled);
            Assert.Same(page.FindResource("TextMutedBrush"), take.Foreground);
            Assert.True(take.Opacity < 1);

            viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "strip");
            OffscreenWindow.WaitUntilIdle();

            Assert.True(take.IsEnabled);
            Assert.Same(page.FindResource("LinkBrush"), take.Foreground);
            Assert.Equal(1.0, take.Opacity);
            Assert.Equal(Cursors.Hand, take.Cursor);
        });
    }

    [Fact]
    public async Task NewButton_MakesAnEffectAndShowsThePanel()
    {
        using var viewModel = await ConnectAsync(effectId: EffectIds.Static);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);

            PageViews.Click(AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, Strings.CustomEffectNew)));

            Assert.True(Panel(page).IsVisible);
            Assert.Equal(UiText.Format(Strings.CustomEffectNewName, 1), NameBox(page).Text);
        });
    }

    private static Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null, CustomEffect? effect = null, string? effectId = null) =>
        PageViews.ConnectAsync(api =>
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
        PageViews.Named<TextBox>(page, Strings.CustomEffectNameAccessibleName);
}
