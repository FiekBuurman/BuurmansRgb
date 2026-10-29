using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
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

    // "Starten met Windows" is a switch with its name and explanation for a screen reader, that
    // shows what the engine has and sends a toggle to it (the fake one: never the registry).
    [Fact]
    public async Task AutoStart_IsASwitchThatFollowsTheEngine()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await PageViews.ConnectAsync(api =>
        {
            engine = api;
            api.AutoStart = true;
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var autoStart = PageViews.Named<ToggleButton>(page, Strings.AutoStartLabel);
            Assert.True(autoStart.IsChecked);
            Assert.Equal(Strings.AutoStartHint, AutomationProperties.GetHelpText(autoStart));
            Assert.Contains(Strings.AutoStartHeader, AppTheme.Descendants<TextBlock>(page).Select(text => text.Text));

            var toggle = (IToggleProvider)new ToggleButtonAutomationPeer(autoStart).GetPattern(PatternInterface.Toggle);
            toggle.Toggle();
            OffscreenWindow.WaitUntilIdle();

            Assert.False(viewModel.AutoStart.IsOn);
            Assert.Equal(["autostart False"], engine!.Calls);
        });
    }

    // As the item in the tray menu, the button opens the log of the engine (here into opened).
    [Fact]
    public async Task OpenLog_OpensTheLogOfTheEngine()
    {
        var opened = new List<string>();
        using var viewModel = await PageViews.ConnectAsync(opened: opened);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var button = PageViews.ButtonNamed(page, Strings.OpenLog);
            Assert.Equal(Strings.LogHint, AutomationProperties.GetHelpText(button));

            PageViews.Click(button);

            Assert.Equal([FileLog.PathOf(FileLog.EngineName)], opened);
        });
    }

    // At the very bottom, after the last card: the version, and no link to the guide while there
    // is no guide every user can open (AppInfoViewModel.GuideAddress).
    [Fact]
    public async Task Bottom_ShowsTheVersionWithoutAGuideToOpen()
    {
        using var viewModel = await PageViews.ConnectAsync(guideAddress: null);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var texts = AppTheme.Descendants<TextBlock>(page).Where(text => text.IsVisible).ToList();
            var version = texts.Single(text => AutomationProperties.GetAutomationId(text) == "AppVersion");
            Assert.Equal(viewModel.AppInfo.VersionText, version.Text);
            Assert.False(PageViews.ButtonNamed(page, Strings.GuideLink).IsVisible);

            var logButton = PageViews.ButtonNamed(page, Strings.OpenLog);
            Assert.True(version.TranslatePoint(default, page).Y > logButton.TranslatePoint(default, page).Y);
        });
    }

    // With an address, the link shows after the version and opens the guide (here into opened).
    [Fact]
    public async Task Bottom_WithAGuide_OpensIt()
    {
        const string guideAddress = @"C:\Program Files\BuurmansRgb\handleiding.html";
        var opened = new List<string>();
        using var viewModel = await PageViews.ConnectAsync(opened: opened, guideAddress: guideAddress);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var guide = PageViews.ButtonNamed(page, Strings.GuideLink);
            Assert.True(guide.IsVisible);

            PageViews.Click(guide);

            Assert.Equal([guideAddress], opened);
        });
    }
}
