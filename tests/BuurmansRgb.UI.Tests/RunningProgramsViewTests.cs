using System.Windows.Controls;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// "Kies uit wat nu draait" under "Aanzetten als dit programma start", on the real ScenesPage in a
// window off screen, with made-up programs. The list is filled without opening it, as its popup
// would show on the screen; closed, it has no items for UI Automation yet.
[Collection(AppThemeCollection.Name)]
public sealed class RunningProgramsViewTests
{
    // WPF shows a list item, and names it for a screen reader, by its ToString (see
    // AccessibleNamesViewTests): the name a user knows the program by with its exe name behind it,
    // or just the exe name.
    [Fact]
    public async Task TheList_NamesEachProgramWithItsExeName()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Profiles.Add("Mail"), [new("olk.exe", "Outlook"), new("game.exe")]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ScenesPage>(panel, viewModel);
            PageViews.Click(PageViews.ButtonNamed(page, string.Format(Strings.SceneEditAccessibleName, "Mail")));
            var list = PageViews.Named<ComboBox>(page, Strings.ProgramPickPlaceholder);
            viewModel.Profiles.Automation.FindProgramsCommand.Execute(null);
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(["Outlook (olk.exe)", "game.exe"], list.Items.Cast<object>().Select(item => item.ToString()));
            // Nothing else shows the items, so ToString is what a user sees and hears.
            Assert.Empty(list.DisplayMemberPath);
            Assert.Null(list.ItemTemplate);
        });
    }

    // The list shows "Outlook (olk.exe)"; the field gets the exe name, and the list its hint again.
    [Fact]
    public async Task PickingAProgram_PutsItsExeNameInTheField()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Profiles.Add("Mail"), [new("olk.exe", "Outlook"), new("game.exe")]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ScenesPage>(panel, viewModel);
            PageViews.Click(PageViews.ButtonNamed(page, string.Format(Strings.SceneEditAccessibleName, "Mail")));
            var list = PageViews.Named<ComboBox>(page, Strings.ProgramPickPlaceholder);
            viewModel.Profiles.Automation.FindProgramsCommand.Execute(null);
            OffscreenWindow.WaitUntilIdle();

            list.SelectedItem = list.Items[0];
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal("Outlook (olk.exe)", list.Items[0].ToString());
            Assert.Equal("olk.exe", viewModel.Profiles.Automation.ProgramText);
            Assert.Equal("olk.exe", PageViews.Named<TextBox>(page, Strings.ProfileProgramLabel).Text);
            Assert.Null(list.SelectedItem);
        });
    }
}
