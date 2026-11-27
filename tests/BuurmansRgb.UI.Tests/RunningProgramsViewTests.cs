using System.Windows.Controls;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// "Kies uit wat nu draait" under "Aanzetten als een van deze programma's start", on the real
// ScenesPage in a window off screen, with made-up programs. The list is filled without opening it,
// as its popup would show on the screen; closed, it has no items for UI Automation yet.
[Collection(AppThemeCollection.Name)]
public sealed class RunningProgramsViewTests
{
    // WPF shows a list item, and names it for a screen reader, by its ToString (see
    // AccessibleNamesViewTests): the name a user knows the program by with its exe name behind it,
    // or just the exe name.
    [Fact]
    public async Task TheList_NamesEachProgramWithItsExeName()
    {
        using var viewModel = await ConnectedViewModel.CreateAsync(api => api.Profiles.Add("Mail"), programs: [new("olk.exe", "Outlook"), new("game.exe")]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ScenesPage>(panel, viewModel);
            PageViews.Click(PageViews.ButtonNamed(page, string.Format(Strings.SceneEditAccessibleName, "Mail")));
            var list = PageViews.Named<ComboBox>(page, Strings.ProgramPickPlaceholder);
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(["Outlook (olk.exe)", "game.exe"], list.Items.Cast<object>().Select(item => item.ToString()));
            // Nothing else shows the items, so ToString is what a user sees and hears.
            Assert.Empty(list.DisplayMemberPath);
            Assert.Null(list.ItemTemplate);
        });
    }

    // The programs are read as soon as the settings of a scene show, so the list does not open
    // empty the first time; it is not open here.
    [Fact]
    public async Task TheList_IsReadBeforeItFirstOpens()
    {
        using var viewModel = await ConnectedViewModel.CreateAsync(api => api.Profiles.Add("Mail"), programs: [new("olk.exe", "Outlook")]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ScenesPage>(panel, viewModel);
            Assert.Empty(viewModel.Profiles.Automation.RunningPrograms);

            PageViews.Click(PageViews.ButtonNamed(page, string.Format(Strings.SceneEditAccessibleName, "Mail")));
            OffscreenWindow.WaitUntilIdle();

            var list = PageViews.Named<ComboBox>(page, Strings.ProgramPickPlaceholder);
            Assert.False(list.IsDropDownOpen);
            Assert.Equal(["Outlook (olk.exe)"], list.Items.Cast<object>().Select(item => item.ToString()));
        });
    }

    // The list shows "Outlook (olk.exe)"; the field gets the exe name, and the list its hint again.
    // A second program comes after the first, with a comma between them.
    [Fact]
    public async Task PickingAProgram_PutsItsExeNameInTheField()
    {
        using var viewModel = await ConnectedViewModel.CreateAsync(api => api.Profiles.Add("Mail"), programs: [new("olk.exe", "Outlook"), new("game.exe")]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ScenesPage>(panel, viewModel);
            PageViews.Click(PageViews.ButtonNamed(page, string.Format(Strings.SceneEditAccessibleName, "Mail")));
            var list = PageViews.Named<ComboBox>(page, Strings.ProgramPickPlaceholder);
            OffscreenWindow.WaitUntilIdle();

            list.SelectedItem = list.Items[0];
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal("Outlook (olk.exe)", list.Items[0].ToString());
            Assert.Equal("olk.exe", viewModel.Profiles.Automation.ProgramText);
            Assert.Equal("olk.exe", PageViews.Named<TextBox>(page, Strings.SceneProgramLabel).Text);
            Assert.Null(list.SelectedItem);

            list.SelectedItem = list.Items[1];
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal("olk.exe, game.exe", PageViews.Named<TextBox>(page, Strings.SceneProgramLabel).Text);
            Assert.Null(list.SelectedItem);
        });
    }

    // "Bladeren…" opens the program dialog of the window; the file name of the chosen .exe goes
    // into the field, and the focus stays on the button.
    [Fact]
    public async Task Browsing_PutsTheChosenExeNameInTheField()
    {
        var dialogs = new FakeDialogs { ChooseProgram = () => @"C:\Games\ELDEN RING\Game\eldenring.exe" };
        using var viewModel = await ConnectedViewModel.CreateAsync(api => api.Profiles.Add("Gamen"), dialogs: dialogs);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ScenesPage>(panel, viewModel);
            PageViews.Click(PageViews.ButtonNamed(page, string.Format(Strings.SceneEditAccessibleName, "Gamen")));
            OffscreenWindow.WaitUntilIdle();
            var browse = AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, Strings.ProgramBrowse));
            browse.Focus();

            PageViews.Click(browse);
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal("eldenring.exe", PageViews.Named<TextBox>(page, Strings.SceneProgramLabel).Text);
            Assert.True(browse.IsKeyboardFocused);
        });
    }
}
