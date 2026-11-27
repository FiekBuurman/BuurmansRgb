using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;
using Microsoft.Win32;

namespace BuurmansRgb.UI.Tests;

// The dialogs of the window, in the window of the test, which stands in for the main window. The
// test answers each dialog itself instead of showing it, as a shown dialog would take the focus of
// the desktop; ConfirmDialogViewTests shows the questions themselves.
[Collection(AppThemeCollection.Name)]
public sealed class WindowDialogsTests
{
    // Each question opens over the window, with what it asks about, and its answer comes back.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Questions_HaveTheWindowAsOwner_AndGiveTheAnswer(bool answer)
    {
        AppTheme.Run(panel =>
        {
            var owner = Window.GetWindow(panel)!;
            var asked = new List<ConfirmDialog>();
            var dialogs = new WindowDialogs(owner, dialog => { asked.Add(dialog); return answer; }, (_, _) => null);
            try
            {
                Assert.Equal(answer, dialogs.ConfirmDeleteScene("Avond"));
                Assert.Equal(answer, dialogs.ConfirmUpdateScene("Avond"));
                Assert.Equal(answer, dialogs.ConfirmDeleteCustomEffect("Disco"));
                Assert.Equal(answer, dialogs.ConfirmRestore("oud.zip"));

                Assert.Equal(4, asked.Count);
                Assert.All(asked, dialog => Assert.Same(owner, dialog.Owner));
                Assert.Equal(UiText.Format(Strings.ProfileDeleteConfirm, "Avond"), asked[0].Title);
                Assert.Equal(UiText.Format(Strings.SceneUpdateConfirm, "Avond"), asked[1].Title);
                Assert.Equal(UiText.Format(Strings.CustomEffectDeleteConfirm, "Disco"), asked[2].Title);
                Assert.Equal(Strings.BackupRestoreConfirm, asked[3].Title);

                // The detail and the button tell the questions apart when the title is alike.
                Assert.Equal(string.Empty, AutomationProperties.GetHelpText(asked[0]));
                Assert.Equal(Strings.ConfirmDelete, asked[0].ConfirmButton.Content);
                Assert.Equal(Strings.SceneUpdateConfirmDetail, AutomationProperties.GetHelpText(asked[1]));
                Assert.Equal(Strings.SceneUpdateConfirmButton, asked[1].ConfirmButton.Content);
                Assert.Equal(Strings.CustomEffectDeleteConfirmDetail, AutomationProperties.GetHelpText(asked[2]));
                Assert.Equal(Strings.ConfirmDelete, asked[2].ConfirmButton.Content);
                Assert.Equal(UiText.Format(Strings.BackupRestoreConfirmDetail, "oud.zip"), AutomationProperties.GetHelpText(asked[3]));
                Assert.Equal(Strings.BackupRestoreConfirmButton, asked[3].ConfirmButton.Content);
            }
            finally
            {
                asked.ForEach(dialog => dialog.Close());
            }
        });
    }

    // A backup is saved as a zip with the date as its name, and a file that is there is only
    // replaced after Windows asked.
    [Fact]
    public void ChooseBackupToSave_OverTheWindow_WithTheSuggestedName()
    {
        Choose(dialogs => dialogs.ChooseBackupToSave("buurmansrgb-2026-11-27.zip"), (owner, shown, window) =>
        {
            Assert.Same(owner, window);
            var dialog = Assert.IsType<SaveFileDialog>(shown);
            Assert.Equal(Strings.BackupSaveTitle, dialog.Title);
            Assert.Equal(Strings.BackupFilter, dialog.Filter);
            Assert.Equal("buurmansrgb-2026-11-27.zip", dialog.FileName);
            // WPF keeps the extension without its dot.
            Assert.Equal(SettingsBackup.Extension.TrimStart('.'), dialog.DefaultExt);
            Assert.True(dialog.AddExtension);
            Assert.True(dialog.OverwritePrompt);
        });
    }

    [Fact]
    public void ChooseBackupToRestore_OverTheWindow_OnlyAFileThatIsThere()
    {
        Choose(dialogs => dialogs.ChooseBackupToRestore(), (owner, shown, window) =>
        {
            Assert.Same(owner, window);
            var dialog = Assert.IsType<OpenFileDialog>(shown);
            Assert.Equal(Strings.BackupRestoreTitle, dialog.Title);
            Assert.Equal(Strings.BackupFilter, dialog.Filter);
            Assert.True(dialog.CheckFileExists);
        });
    }

    // Sharing gives the title itself: one scene or one own effect.
    [Fact]
    public void ChooseShareToSave_OverTheWindow_WithTheTitleAndName()
    {
        Choose(dialogs => dialogs.ChooseShareToSave("Scène delen", "Avond.buurmansrgb"), (owner, shown, window) =>
        {
            Assert.Same(owner, window);
            var dialog = Assert.IsType<SaveFileDialog>(shown);
            Assert.Equal("Scène delen", dialog.Title);
            Assert.Equal(Strings.ShareFilter, dialog.Filter);
            Assert.Equal("Avond.buurmansrgb", dialog.FileName);
            Assert.True(dialog.AddExtension);
            Assert.True(dialog.OverwritePrompt);
        });
    }

    [Fact]
    public void ChooseShareToImport_OverTheWindow_OnlyAFileThatIsThere()
    {
        Choose(dialogs => dialogs.ChooseShareToImport(), (owner, shown, window) =>
        {
            Assert.Same(owner, window);
            var dialog = Assert.IsType<OpenFileDialog>(shown);
            Assert.Equal(Strings.ShareImportTitle, dialog.Title);
            Assert.Equal(Strings.ShareFilter, dialog.Filter);
            Assert.True(dialog.CheckFileExists);
        });
    }

    // "Bladeren…" at "Aanzetten als een van deze programma's start".
    [Fact]
    public void ChooseProgram_OverTheWindow_OnlyAProgramThatIsThere()
    {
        Choose(dialogs => dialogs.ChooseProgram(), (owner, shown, window) =>
        {
            Assert.Same(owner, window);
            var dialog = Assert.IsType<OpenFileDialog>(shown);
            Assert.Equal(Strings.ProgramBrowseTitle, dialog.Title);
            Assert.Equal(Strings.ProgramBrowseFilter, dialog.Filter);
            Assert.True(dialog.CheckFileExists);
        });
    }

    // OK gives the chosen file, Annuleren (or the cross) nothing.
    [Theory]
    [InlineData(true, @"C:\Temp\gekozen.zip")]
    [InlineData(false, null)]
    [InlineData(null, null)]
    public void FileDialogs_GiveTheChosenFileOnlyAfterOk(bool? result, string? expected)
    {
        AppTheme.Run(panel =>
        {
            var dialogs = new WindowDialogs(Window.GetWindow(panel)!, _ => false, (dialog, _) =>
            {
                dialog.FileName = @"C:\Temp\gekozen.zip";
                return result;
            });

            Assert.Equal(expected, dialogs.ChooseBackupToSave("voorstel.zip"));
            Assert.Equal(expected, dialogs.ChooseBackupToRestore());
            Assert.Equal(expected, dialogs.ChooseShareToSave("Delen", "voorstel.buurmansrgb"));
            Assert.Equal(expected, dialogs.ChooseShareToImport());
            Assert.Equal(expected, dialogs.ChooseProgram());
        });
    }

    // After each dialog the button that opened it has the focus again, so Tab goes on from there.
    [Fact]
    public void AfterEachDialog_TheButtonHasTheFocusAgain()
    {
        AppTheme.Run(panel =>
        {
            var button = new Button { Content = "Openen" };
            var elsewhere = new TextBox();
            panel.Children.Add(button);
            panel.Children.Add(elsewhere);
            OffscreenWindow.WaitUntilIdle();

            // The dialog takes the focus away, as a shown one does.
            var dialogs = new WindowDialogs(
                Window.GetWindow(panel)!,
                dialog => { elsewhere.Focus(); dialog.Close(); return false; },
                (_, _) => { elsewhere.Focus(); return false; });

            Action[] opens =
            [
                () => dialogs.ConfirmDeleteScene("Avond"),
                () => dialogs.ConfirmUpdateScene("Avond"),
                () => dialogs.ConfirmDeleteCustomEffect("Disco"),
                () => dialogs.ConfirmRestore("oud.zip"),
                () => dialogs.ChooseBackupToSave("voorstel.zip"),
                () => dialogs.ChooseBackupToRestore(),
                () => dialogs.ChooseShareToSave("Delen", "voorstel.buurmansrgb"),
                () => dialogs.ChooseShareToImport(),
                () => dialogs.ChooseProgram(),
            ];
            foreach (var open in opens)
            {
                Assert.True(button.Focus());
                open();
                Assert.True(button.IsKeyboardFocused);
            }
        });
    }

    // A button that went with the answer, such as the card of a deleted scene, does not get the
    // focus back; the page moves it on itself.
    [Fact]
    public void AfterADialog_AButtonThatWentDoesNotGetTheFocus()
    {
        AppTheme.Run(panel =>
        {
            var button = new Button { Content = "Verwijderen" };
            var elsewhere = new TextBox();
            panel.Children.Add(button);
            panel.Children.Add(elsewhere);
            OffscreenWindow.WaitUntilIdle();
            var dialogs = new WindowDialogs(Window.GetWindow(panel)!, dialog =>
            {
                elsewhere.Focus();
                button.Visibility = Visibility.Collapsed;
                dialog.Close();
                return true;
            }, (_, _) => null);

            Assert.True(button.Focus());
            Assert.True(dialogs.ConfirmDeleteScene("Avond"));

            Assert.True(elsewhere.IsKeyboardFocused);
        });
    }

    // One file dialog, answered with OK on what it suggests; check gets the owner, the dialog and
    // the window it was shown over.
    private static void Choose(Func<WindowDialogs, string?> choose, Action<Window, FileDialog, Window> check) => AppTheme.Run(panel =>
    {
        var owner = Window.GetWindow(panel)!;
        FileDialog? shown = null;
        Window? shownOver = null;
        var dialogs = new WindowDialogs(owner, _ => false, (dialog, window) =>
        {
            shown = dialog;
            shownOver = window;
            return true;
        });

        var chosen = choose(dialogs);
        Assert.NotNull(shown);
        Assert.Equal(shown.FileName, chosen);
        check(owner, shown, shownOver!);
    });
}
