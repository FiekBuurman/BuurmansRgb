using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BuurmansRgb.Contracts;
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

    // What the devices do while the pc sleeps or is locked is the first block, under a heading, with
    // the choice in plain words and the heading as its name; picking one sends it to the engine.
    [Fact]
    public async Task SleepBlock_IsFirst_WithTheChoiceInPlainWords()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await PageViews.ConnectAsync(api =>
        {
            engine = api;
            api.PausedLights = PausedLightsMode.Dim;
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var choice = PageViews.ById<ComboBox>(page, "PausedLights");
            var header = AppTheme.Descendants<TextBlock>(page).Single(text => text.Text == Strings.SleepHeader);

            Assert.Equal(AutomationHeadingLevel.Level2, AutomationProperties.GetHeadingLevel(header));
            Assert.Equal(Strings.SleepHeader, AutomationProperties.GetName(choice));
            Assert.Equal(Strings.PausedLightsHint, AutomationProperties.GetHelpText(choice));
            Assert.Equal(["Uitzetten", "Dimmen", "Laten branden"], choice.Items.Cast<object>().Select(item => item.ToString()));
            Assert.Equal(Strings.PausedLightsMode_Dim, choice.Text);
            Assert.True(choice.IsEnabled);
            Assert.False(PageViews.ById<TextBlock>(page, "PausedLightsNeedsPause").IsVisible);
            var openRgb = PageViews.ById<TextBlock>(page, "OpenRgbStatus");
            Assert.True(choice.TranslatePoint(default, page).Y < openRgb.TranslatePoint(default, page).Y);
            var pause = PageViews.Named<ToggleButton>(page, Strings.PauseOnLockLabel);
            Assert.True(pause.TranslatePoint(default, page).Y < choice.TranslatePoint(default, page).Y);

            choice.SelectedIndex = 0;
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(["paused lights Off"], engine!.Calls);
        });
    }

    // Without the pause the choice does nothing: grey, with a line that says why, which a screen
    // reader also hears on the choice; the switch it needs is above it.
    [Fact]
    public async Task SleepBlock_WithoutThePause_GreysTheChoiceAndSaysWhy()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.PauseOnLockAndSleep = false);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var choice = PageViews.ById<ComboBox>(page, "PausedLights");
            var pause = PageViews.Named<ToggleButton>(page, Strings.PauseOnLockLabel);

            Assert.False(choice.IsEnabled);
            Assert.True(PageViews.ById<TextBlock>(page, "PausedLightsNeedsPause").IsVisible);
            Assert.StartsWith(Strings.PausedLightsNeedsPause, AutomationProperties.GetHelpText(choice), StringComparison.Ordinal);
            Assert.True(pause.IsVisible);
            Assert.True(pause.TranslatePoint(default, page).Y < choice.TranslatePoint(default, page).Y);
        });
    }

    // "Na een herstart" comes right under the sleep block, under a heading, and says what comes back:
    // the scene that is on, the brightness and the sleep choice. "Starten met Windows" is off, so it
    // says the lighting does not come back by itself; the button switches it on as the switch does,
    // and goes, and the focus goes to that switch.
    [Fact]
    public async Task AfterRestartBlock_SaysWhatComesBackAndTurnsOnAutoStart()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await PageViews.ConnectAsync(api =>
        {
            engine = api;
            api.Profiles.Add("Gamen");
            api.ActiveProfile = "Gamen";
            api.PausedLights = PausedLightsMode.Dim;
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var header = AppTheme.Descendants<TextBlock>(page).Single(text => text.Text == Strings.AfterRestartHeader);
            var sleep = AppTheme.Descendants<TextBlock>(page).Single(text => text.Text == Strings.SleepHeader);
            var openRgb = PageViews.ById<TextBlock>(page, "OpenRgbStatus");
            var lighting = PageViews.ById<TextBlock>(page, "AfterRestartLighting");
            var autoStartText = PageViews.ById<TextBlock>(page, "AfterRestartAutoStart");
            var button = PageViews.ById<Button>(page, "AfterRestartTurnOnAutoStart");
            var autoStart = PageViews.Named<ToggleButton>(page, Strings.AutoStartLabel);

            Assert.Equal(AutomationHeadingLevel.Level2, AutomationProperties.GetHeadingLevel(header));
            Assert.True(sleep.TranslatePoint(default, page).Y < header.TranslatePoint(default, page).Y);
            Assert.True(header.TranslatePoint(default, page).Y < openRgb.TranslatePoint(default, page).Y);
            Assert.Equal("de scène 'Gamen', die nu aan staat", lighting.Text);
            Assert.EndsWith("\"Dimmen\"", PageViews.ById<TextBlock>(page, "AfterRestartSleep").Text, StringComparison.Ordinal);
            Assert.Contains(Strings.AfterRestartBrightness, AppTheme.Descendants<TextBlock>(page).Select(text => text.Text));
            Assert.Equal(Strings.AfterRestartAutoStartOff, autoStartText.Text);
            Assert.True(button.IsVisible);
            Assert.Equal(Strings.AfterRestartTurnOnAutoStart, AutomationProperties.GetName(button));

            Assert.True(button.Focus());
            PageViews.Click(button);
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(["autostart True"], engine!.Calls);
            Assert.True(autoStart.IsChecked);
            Assert.False(button.IsVisible);
            Assert.True(autoStart.IsKeyboardFocused);
            Assert.Equal(Strings.AfterRestartAutoStartOn, autoStartText.Text);
        });
    }

    // A color of the Kleur page ends the scene: the block says the lighting of every device again.
    [Fact]
    public async Task AfterRestartBlock_FollowsTheSceneThatEnds()
    {
        FakeEngineApi? engine = null;
        using var viewModel = await PageViews.ConnectAsync(api =>
        {
            engine = api;
            api.Profiles.Add("Gamen");
            api.ActiveProfile = "Gamen";
            api.AutoStart = true;
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var lighting = PageViews.ById<TextBlock>(page, "AfterRestartLighting");
            Assert.False(PageViews.ById<Button>(page, "AfterRestartTurnOnAutoStart").IsVisible);

            engine!.ActiveProfile = null;
            engine.RaiseLightingChanged();
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(Strings.AfterRestartLighting, lighting.Text);
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

    // Beside it, the folder with the settings and the logs, in Verkenner (here into opened).
    [Fact]
    public async Task OpenDataFolder_OpensTheFolderOfTheEngine()
    {
        var opened = new List<string>();
        using var viewModel = await PageViews.ConnectAsync(opened: opened);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var button = PageViews.ButtonNamed(page, Strings.OpenDataFolder);
            Assert.Equal(Strings.DataFolderHint, AutomationProperties.GetHelpText(button));

            PageViews.Click(button);

            Assert.Equal([DataFolder.Path], opened);
        });
    }

    // Saving and restoring: two buttons with their names and explanations for a screen reader, that
    // work once the app is connected.
    [Fact]
    public async Task Backup_HasItsTwoButtons()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var save = PageViews.ButtonNamed(page, Strings.BackupSave);
            var restore = PageViews.ButtonNamed(page, Strings.BackupRestore);

            Assert.Equal(Strings.BackupSaveHint, AutomationProperties.GetHelpText(save));
            Assert.Equal(Strings.BackupRestoreHint, AutomationProperties.GetHelpText(restore));
            Assert.True(save.IsEnabled);
            Assert.True(restore.IsEnabled);
            Assert.Same(viewModel.Backup.SaveCommand, save.Command);
            Assert.Same(viewModel.Backup.RestoreCommand, restore.Command);
            Assert.Contains(Strings.BackupHeader, AppTheme.Descendants<TextBlock>(page).Select(text => text.Text));
        });
    }

    // The memory and CPU of the engine: a line with its text as its name and the explanation as
    // its help, but no live region, so a screen reader does not read it out at every reading.
    [Fact]
    public async Task EngineUsage_HasANameButIsNoLiveRegion()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var usage = PageViews.ById<TextBlock>(page, "EngineUsage");
            var peer = new TextBlockAutomationPeer(usage);

            Assert.Equal(viewModel.EngineUsage.Text, peer.GetName());
            Assert.Equal(Strings.UsageHint, AutomationProperties.GetHelpText(usage));
            Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(usage));
            Assert.Contains(Strings.UsageHeader, AppTheme.Descendants<TextBlock>(page).Select(text => text.Text));
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
            var version = PageViews.ById<TextBlock>(page, "AppVersion");
            Assert.True(version.IsVisible);
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
