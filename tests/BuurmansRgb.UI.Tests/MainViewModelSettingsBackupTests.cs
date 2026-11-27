using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;
using StreamJsonRpc;

namespace BuurmansRgb.UI.Tests;

// "Instellingen bewaren…" and "terugzetten…" through a fake engine, with fake dialogs and files in a
// folder of the test: never the settings of this pc.
public sealed class MainViewModelSettingsBackupTests : IDisposable
{
    private readonly FakeEngineConnector _connector = new();
    private readonly TempDirectory _folder = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly List<string> _confirmed = [];

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    public void Dispose() => _folder.Dispose();

    // Without an engine to ask nothing can be backed up or restored.
    [Fact]
    public void Buttons_WaitForTheEngine()
    {
        using var viewModel = new MainViewModel(_connector);

        Assert.False(viewModel.Backup.SaveCommand.CanExecute(null));
        Assert.False(viewModel.Backup.RestoreCommand.CanExecute(null));
    }

    [Fact]
    public async Task Buttons_GoWithTheConnection()
    {
        using var viewModel = await ConnectAsync();
        Assert.True(viewModel.Backup.SaveCommand.CanExecute(null));

        _connector.Connection.Disconnect();

        Assert.False(viewModel.Backup.SaveCommand.CanExecute(null));
        Assert.False(viewModel.Backup.RestoreCommand.CanExecute(null));
    }

    // The zip the engine made goes into the file the user chose, with the date of today as the
    // suggested name.
    [Fact]
    public async Task Save_WritesTheZipOfTheEngine()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 3, 23, 15, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        using var viewModel = await ConnectAsync(time);
        var path = _folder.File("bewaard.zip");
        string? suggested = null;
        _dialogs.ChooseBackupToSave = name =>
        {
            suggested = name;
            return path;
        };

        await viewModel.Backup.SaveCommand.ExecuteAsync(null);

        Assert.Equal("buurmansRgb-instellingen-2026-11-03.zip", suggested);
        Assert.Equal(Api.ExportedSettings, File.ReadAllBytes(path));
        Assert.Equal(UiText.Format(Strings.BackupSaved, "bewaard.zip"), viewModel.StatusMessage);
        Assert.True(viewModel.Backup.SaveCommand.CanExecute(null));
    }

    // A file the user chose to overwrite gets the new zip as a whole, without a temporary file left.
    [Fact]
    public async Task Save_OverAFile_ReplacesItWithoutATemporaryFile()
    {
        using var viewModel = await ConnectAsync();
        var path = WriteZip("bewaard.zip", [1, 2, 3]);
        _dialogs.ChooseBackupToSave = _ => path;

        await viewModel.Backup.SaveCommand.ExecuteAsync(null);

        Assert.Equal(Api.ExportedSettings, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    // A file that cannot be replaced (open elsewhere) keeps what it had: the zip goes next to it
    // first, and that temporary file goes again.
    [Fact]
    public async Task Save_FileThatCannotBeReplaced_KeepsIt()
    {
        using var viewModel = await ConnectAsync();
        var path = WriteZip("bewaard.zip", [1, 2, 3]);
        _dialogs.ChooseBackupToSave = _ => path;

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await viewModel.Backup.SaveCommand.ExecuteAsync(null);
        }

        Assert.Equal(Strings.BackupSaveFailed, viewModel.StatusMessage);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task Save_Cancelled_AsksTheEngineNothing()
    {
        using var viewModel = await ConnectAsync();
        _dialogs.ChooseBackupToSave = _ => null;

        await viewModel.Backup.SaveCommand.ExecuteAsync(null);

        Assert.Empty(Api.Calls);
    }

    // A folder that is not there (or not writable): a message, and the buttons work again.
    [Fact]
    public async Task Save_FileThatCannotBeWritten_SaysSo()
    {
        using var viewModel = await ConnectAsync();
        _dialogs.ChooseBackupToSave = _ => Path.Combine(_folder.Path, "weg", "bewaard.zip");

        await viewModel.Backup.SaveCommand.ExecuteAsync(null);

        Assert.Equal(Strings.BackupSaveFailed, viewModel.StatusMessage);
        Assert.False(viewModel.Backup.IsBusy);
    }

    // First the question with the name of the file; only Terugzetten sends the zip to the engine.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restore_AsksFirst(bool confirm)
    {
        using var viewModel = await ConnectAsync();
        var path = WriteZip("oud.zip", [7, 8, 9]);
        _dialogs.ChooseBackupToRestore = () => path;
        _dialogs.ConfirmRestore = fileName =>
        {
            _confirmed.Add(fileName);
            return confirm;
        };

        await viewModel.Backup.RestoreCommand.ExecuteAsync(null);

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
        _dialogs.ChooseBackupToRestore = () => null;
        _dialogs.ConfirmRestore = Refuse;

        await viewModel.Backup.RestoreCommand.ExecuteAsync(null);

        Assert.Empty(_confirmed);
        Assert.Empty(Api.Calls);
    }

    // A file larger than a backup is read no further than the limit: no question, nothing to the
    // engine.
    [Fact]
    public async Task Restore_FileTooLarge_IsNotSent()
    {
        using var viewModel = await ConnectAsync();
        var path = _folder.File("groot.zip");
        using (var file = File.Create(path))
        {
            file.SetLength(SettingsBackup.MaxArchiveBytes + 1);
        }

        _dialogs.ChooseBackupToRestore = () => path;
        _dialogs.ConfirmRestore = Refuse;

        await viewModel.Backup.RestoreCommand.ExecuteAsync(null);

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
        _dialogs.ChooseBackupToRestore = () => WriteZip("oud.zip", [1]);
        _dialogs.ConfirmRestore = _ => true;

        await viewModel.Backup.RestoreCommand.ExecuteAsync(null);

        var expected = outcome switch
        {
            SettingsRestoreOutcome.NotABackup => Strings.BackupNotABackup,
            SettingsRestoreOutcome.TooLarge => Strings.BackupTooLarge,
            SettingsRestoreOutcome.Unreadable => UiText.Format(Strings.BackupUnreadable, fileName),
            _ => Strings.BackupRestoreFailed,
        };
        Assert.Equal(expected, viewModel.StatusMessage);
    }

    // An engine that refused the call changed nothing.
    [Fact]
    public async Task Restore_RejectedByTheEngine_SaysNothingChanged()
    {
        using var viewModel = await ConnectAsync();
        Api.SetException = new RemoteInvocationException("no files", 0, errorData: null);
        _dialogs.ChooseBackupToRestore = () => WriteZip("oud.zip", [1]);
        _dialogs.ConfirmRestore = _ => true;

        await viewModel.Backup.RestoreCommand.ExecuteAsync(null);

        Assert.Equal(Strings.BackupRestoreNotDone, viewModel.StatusMessage);
    }

    // An engine that went meanwhile, or did not answer in time, may have restored it: no promise
    // that nothing changed, but where the copy is. Also an IOException of the pipe, which is not
    // one of the file.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restore_EngineGone_SaysWhereTheCopyIs(bool pipe)
    {
        using var viewModel = await ConnectAsync();
        Api.SetException = pipe ? new IOException("Pipe is broken.") : new ConnectionLostException("weg");
        _dialogs.ChooseBackupToRestore = () => WriteZip("oud.zip", [1]);
        _dialogs.ConfirmRestore = _ => true;

        await viewModel.Backup.RestoreCommand.ExecuteAsync(null);

        Assert.Equal(Strings.BackupRestoreUnknown, viewModel.StatusMessage);
    }

    [Fact]
    public async Task Restore_FileGone_SaysItCouldNotBeRead()
    {
        using var viewModel = await ConnectAsync();
        _dialogs.ChooseBackupToRestore = () => _folder.File("weg.zip");

        await viewModel.Backup.RestoreCommand.ExecuteAsync(null);

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
        var path = _folder.File(name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private Task<MainViewModel> ConnectAsync(TimeProvider? time = null) =>
        ConnectedViewModel.CreateAsync(connector: _connector, time: time, dialogs: _dialogs);
}

