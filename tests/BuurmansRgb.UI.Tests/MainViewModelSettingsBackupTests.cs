using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.UI.Tests;

// "Instellingen bewaren…" and "terugzetten…" through a fake engine, with fake dialogs and files in a
// folder of the test: never the settings of this pc.
public sealed class MainViewModelSettingsBackupTests : IDisposable
{
    private readonly FakeEngineConnector _connector = new();
    private readonly string _folder = Directory.CreateTempSubdirectory("BuurmansRgb.UI.Tests.").FullName;
    private readonly List<string> _confirmed = [];

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // Without an engine to ask nothing can be backed up or restored.
    [Fact]
    public void Buttons_WaitForTheEngine()
    {
        using var viewModel = new MainViewModel(_connector);

        Assert.False(viewModel.SettingsBackup.SaveCommand.CanExecute(null));
        Assert.False(viewModel.SettingsBackup.RestoreCommand.CanExecute(null));
    }

    [Fact]
    public async Task Buttons_GoWithTheConnection()
    {
        using var viewModel = await ConnectAsync();
        Assert.True(viewModel.SettingsBackup.SaveCommand.CanExecute(null));

        _connector.Connection.Disconnect();

        Assert.False(viewModel.SettingsBackup.SaveCommand.CanExecute(null));
        Assert.False(viewModel.SettingsBackup.RestoreCommand.CanExecute(null));
    }

    // The zip the engine made goes into the file the user chose, with the date of today as the
    // suggested name.
    [Fact]
    public async Task Save_WritesTheZipOfTheEngine()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 3, 23, 15, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        using var viewModel = await ConnectAsync(time);
        var path = Path.Combine(_folder, "bewaard.zip");
        string? suggested = null;
        viewModel.SettingsBackup.ChooseSaveFile = name =>
        {
            suggested = name;
            return path;
        };

        await viewModel.SettingsBackup.SaveCommand.ExecuteAsync(null);

        Assert.Equal("buurmansRgb-instellingen-2026-11-03.zip", suggested);
        Assert.Equal(Api.ExportedSettings, File.ReadAllBytes(path));
        Assert.Equal(UiText.Format(Strings.BackupSaved, "bewaard.zip"), viewModel.StatusMessage);
        Assert.True(viewModel.SettingsBackup.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Save_Cancelled_AsksTheEngineNothing()
    {
        using var viewModel = await ConnectAsync();
        viewModel.SettingsBackup.ChooseSaveFile = _ => null;

        await viewModel.SettingsBackup.SaveCommand.ExecuteAsync(null);

        Assert.Empty(Api.Calls);
    }

    // A folder that is not there (or not writable): a message, and the buttons work again.
    [Fact]
    public async Task Save_FileThatCannotBeWritten_SaysSo()
    {
        using var viewModel = await ConnectAsync();
        viewModel.SettingsBackup.ChooseSaveFile = _ => Path.Combine(_folder, "weg", "bewaard.zip");

        await viewModel.SettingsBackup.SaveCommand.ExecuteAsync(null);

        Assert.Equal(Strings.BackupSaveFailed, viewModel.StatusMessage);
        Assert.False(viewModel.SettingsBackup.IsBusy);
    }

    // First the question with the name of the file; only Terugzetten sends the zip to the engine.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restore_AsksFirst(bool confirm)
    {
        using var viewModel = await ConnectAsync();
        var path = WriteZip("oud.zip", [7, 8, 9]);
        viewModel.SettingsBackup.ChooseRestoreFile = () => path;
        viewModel.SettingsBackup.ConfirmRestore = fileName =>
        {
            _confirmed.Add(fileName);
            return confirm;
        };

        await viewModel.SettingsBackup.RestoreCommand.ExecuteAsync(null);

        Assert.Equal(["oud.zip"], _confirmed);
        if (confirm)
        {
            Assert.Equal([7, 8, 9], Assert.Single(Api.RestoredArchives));
            Assert.Equal(Strings.BackupRestored, viewModel.StatusMessage);
        }
        else
        {
            Assert.Empty(Api.Calls);
        }
    }

    [Fact]
    public async Task Restore_Cancelled_AsksNothing()
    {
        using var viewModel = await ConnectAsync();
        viewModel.SettingsBackup.ChooseRestoreFile = () => null;
        viewModel.SettingsBackup.ConfirmRestore = Refuse;

        await viewModel.SettingsBackup.RestoreCommand.ExecuteAsync(null);

        Assert.Empty(_confirmed);
        Assert.Empty(Api.Calls);
    }

    // A file far larger than a backup is not read at all: no question, nothing to the engine.
    [Fact]
    public async Task Restore_FileTooLarge_IsNotRead()
    {
        using var viewModel = await ConnectAsync();
        var path = Path.Combine(_folder, "groot.zip");
        using (var file = File.Create(path))
        {
            file.SetLength(SettingsBackup.MaxArchiveBytes + 1);
        }

        viewModel.SettingsBackup.ChooseRestoreFile = () => path;
        viewModel.SettingsBackup.ConfirmRestore = Refuse;

        await viewModel.SettingsBackup.RestoreCommand.ExecuteAsync(null);

        Assert.Equal(Strings.BackupTooLarge, viewModel.StatusMessage);
        Assert.Empty(_confirmed);
        Assert.Empty(Api.Calls);
    }

    // What the engine found wrong, in plain words; it replaced nothing then.
    [Theory]
    [InlineData(SettingsRestoreOutcome.NotABackup, null)]
    [InlineData(SettingsRestoreOutcome.TooLarge, null)]
    [InlineData(SettingsRestoreOutcome.Unreadable, SettingsBackup.ProfilesFileName)]
    [InlineData(SettingsRestoreOutcome.Failed, null)]
    public async Task Restore_RefusedByTheEngine_SaysWhy(SettingsRestoreOutcome outcome, string? fileName)
    {
        using var viewModel = await ConnectAsync();
        Api.RestoreResult = new SettingsRestoreResult(outcome, fileName);
        viewModel.SettingsBackup.ChooseRestoreFile = () => WriteZip("oud.zip", [1]);
        viewModel.SettingsBackup.ConfirmRestore = _ => true;

        await viewModel.SettingsBackup.RestoreCommand.ExecuteAsync(null);

        var expected = outcome switch
        {
            SettingsRestoreOutcome.NotABackup => Strings.BackupNotABackup,
            SettingsRestoreOutcome.TooLarge => Strings.BackupTooLarge,
            SettingsRestoreOutcome.Unreadable => UiText.Format(Strings.BackupUnreadable, fileName),
            _ => Strings.BackupRestoreFailed,
        };
        Assert.Equal(expected, viewModel.StatusMessage);
    }

    // The engine that went, or a file gone between the dialog and the read.
    [Fact]
    public async Task Restore_EngineFails_SaysNothingChanged()
    {
        using var viewModel = await ConnectAsync();
        Api.SetException = new InvalidOperationException("weg");
        viewModel.SettingsBackup.ChooseRestoreFile = () => WriteZip("oud.zip", [1]);
        viewModel.SettingsBackup.ConfirmRestore = _ => true;

        await viewModel.SettingsBackup.RestoreCommand.ExecuteAsync(null);

        Assert.Equal(Strings.BackupRestoreNotDone, viewModel.StatusMessage);
    }

    [Fact]
    public async Task Restore_FileGone_SaysItCouldNotBeRead()
    {
        using var viewModel = await ConnectAsync();
        viewModel.SettingsBackup.ChooseRestoreFile = () => Path.Combine(_folder, "weg.zip");

        await viewModel.SettingsBackup.RestoreCommand.ExecuteAsync(null);

        Assert.Equal(Strings.BackupReadFailed, viewModel.StatusMessage);
        Assert.Empty(Api.Calls);
    }

    // A ConfirmRestore that notes the question and answers no.
    private bool Refuse(string fileName)
    {
        _confirmed.Add(fileName);
        return false;
    }

    private string WriteZip(string name, byte[] content)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private async Task<MainViewModel> ConnectAsync(TimeProvider? time = null)
    {
        var viewModel = new MainViewModel(_connector, time);
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}

