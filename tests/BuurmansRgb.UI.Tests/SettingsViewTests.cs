using System.Windows.Controls;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The real SettingsPage, in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class SettingsViewTests
{
    // Whether saving worked is in the status line under every page, so the page has no card
    // "Opslaan" that says it again.
    [Fact]
    public async Task Page_HasNoCardOpslaan()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var texts = AppTheme.Descendants<TextBlock>(page).Select(text => text.Text).ToList();

            Assert.Contains(Strings.HotkeysHeader, texts);
            Assert.DoesNotContain("Opslaan", texts);
            Assert.DoesNotContain(Strings.SaveAutomatic, texts);
            Assert.DoesNotContain(viewModel.SaveStatus, texts);
        });
    }
}
