using System.Buffers.Text;
using System.IO.Compression;
using System.Text;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.ViewModels;
using StreamJsonRpc;

namespace BuurmansRgb.UI.Tests;

// "Exporteren…", "Code kopiëren", "Importeren…" and "Code plakken" through a fake engine, with fake
// dialogs, a clipboard of the test and files in a folder of the test: never the clipboard or the
// files of this pc.
public sealed class MainViewModelSharingTests : IDisposable
{
    private readonly FakeEngineConnector _connector = new();
    private readonly AppClipboard _clipboard = new();
    private readonly string _folder = Directory.CreateTempSubdirectory("BuurmansRgb.UI.Tests.").FullName;

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task Buttons_WaitForTheEngineAndAScene()
    {
        using var unconnected = new MainViewModel(_connector, clipboard: _clipboard);
        Assert.False(unconnected.Sharing.ImportCommand.CanExecute(null));
        Assert.False(unconnected.Sharing.PasteCodeCommand.CanExecute(null));
        Assert.False(unconnected.Sharing.ExportSceneCommand.CanExecute("Gamen"));

        using var viewModel = await ConnectAsync();

        Assert.True(viewModel.Sharing.ImportCommand.CanExecute(null));
        Assert.True(viewModel.Sharing.ExportSceneCommand.CanExecute("Gamen"));
        Assert.False(viewModel.Sharing.ExportSceneCommand.CanExecute(null));
        Assert.False(viewModel.Sharing.ExportEffectCommand.CanExecute(viewModel.CustomEffectEditor));

        _connector.Connection.Disconnect();

        Assert.False(viewModel.Sharing.CopySceneCodeCommand.CanExecute("Gamen"));
        Assert.False(viewModel.Sharing.PasteCodeCommand.CanExecute(null));
    }

    // The JSON of the engine goes into the file the user chose, with the name of the scene as the
    // suggested name.
    [Fact]
    public async Task ExportScene_WritesTheFileOfTheEngine()
    {
        using var viewModel = await ConnectAsync();
        var path = Path.Combine(_folder, "Gamen.brgb.json");
        (string Title, string Name)? asked = null;
        viewModel.Sharing.ChooseExportFile = (title, name) =>
        {
            asked = (title, name);
            return path;
        };

        await viewModel.Sharing.ExportSceneCommand.ExecuteAsync("Gamen");

        Assert.Equal((Strings.ShareExportSceneTitle, "Gamen.brgb.json"), asked);
        Assert.Equal(["export scene Gamen"], Api.Calls);
        Assert.Equal(Api.ExportedShare, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal(UiText.Format(Strings.ShareExported, "Gamen", "Gamen.brgb.json"), viewModel.StatusMessage);
        Assert.False(viewModel.Sharing.IsBusy);
    }

    [Fact]
    public async Task Export_Cancelled_AsksTheEngineNothing()
    {
        using var viewModel = await ConnectAsync();
        viewModel.Sharing.ChooseExportFile = (_, _) => null;

        await viewModel.Sharing.ExportSceneCommand.ExecuteAsync("Gamen");

        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task Export_FolderThatIsNotThere_SaysSo()
    {
        using var viewModel = await ConnectAsync();
        viewModel.Sharing.ChooseExportFile = (_, _) => Path.Combine(_folder, "weg", "Gamen.brgb.json");

        await viewModel.Sharing.ExportSceneCommand.ExecuteAsync("Gamen");

        Assert.Equal(Strings.ShareExportFailed, viewModel.StatusMessage);
    }

    [Theory]
    [InlineData("Gamen", "Gamen.brgb.json")]
    [InlineData("Film/TV", "Film-TV.brgb.json")]
    [InlineData("Wat? Nu*", "Wat- Nu-.brgb.json")]
    public void FileNameFor_PutsADashForWhatAFileNameCannotHave(string name, string fileName) =>
        Assert.Equal(fileName, SharingViewModel.FileNameFor(name));

    // The code on the clipboard is the JSON of the engine, packed.
    [Fact]
    public async Task CopySceneCode_PutsTheCodeOnTheClipboard()
    {
        using var viewModel = await ConnectAsync();

        await viewModel.Sharing.CopySceneCodeCommand.ExecuteAsync("Gamen");

        Assert.True(ShareCode.TryDecode(_clipboard.Text, out var json, out _));
        Assert.Equal(Api.ExportedShare, json);
        Assert.Equal(UiText.Format(Strings.ShareCodeCopied, "Gamen"), viewModel.StatusMessage);
    }

    [Fact]
    public async Task CopyEffectCode_ExportsTheEffectShown()
    {
        using var viewModel = await ConnectAsync(api => api.CustomEffects.Add(new CustomEffect("custom:1", "Disco", [new CustomEffectStep(Red)])));
        viewModel.CustomEffectEditor.Show(Api.CustomEffects[0]);

        await viewModel.Sharing.CopyEffectCodeCommand.ExecuteAsync(viewModel.CustomEffectEditor);

        Assert.Equal(["export effect custom:1"], Api.Calls);
        Assert.Equal(UiText.Format(Strings.ShareCodeCopied, "Disco"), viewModel.StatusMessage);
    }

    [Fact]
    public async Task CopyCode_ClipboardThatSaysNo_SaysSo()
    {
        using var viewModel = await ConnectAsync(clipboard: new RefusingClipboard());

        await viewModel.Sharing.CopySceneCodeCommand.ExecuteAsync("Gamen");

        Assert.Equal(Strings.ShareCopyFailed, viewModel.StatusMessage);
    }

    // The file goes to the engine as it is; the scene it added is chosen on Scènes.
    [Fact]
    public async Task Import_SendsTheFileAndChoosesTheScene()
    {
        using var viewModel = await ConnectAsync(api => api.Profiles.Add("Werk"));
        var path = WriteFile("Gamen.brgb.json", [1, 2, 3]);
        viewModel.Sharing.ChooseImportFile = () => path;

        await viewModel.Sharing.ImportCommand.ExecuteAsync(null);

        Assert.Equal([1, 2, 3], Assert.Single(Api.ImportedShares));
        Assert.Equal(UiText.Format(Strings.ShareSceneImported, "Gamen"), viewModel.StatusMessage);
        await Eventually.TrueAsync(() => viewModel.Profiles.SelectedName == "Gamen");
        Assert.Equal(["Gamen", "Werk"], viewModel.Profiles.Names);
    }

    // A file far larger than a scene is not read at all.
    [Fact]
    public async Task Import_FileTooLarge_IsNotRead()
    {
        using var viewModel = await ConnectAsync();
        var path = Path.Combine(_folder, "groot.brgb.json");
        using (var file = File.Create(path))
        {
            file.SetLength(SharedFiles.MaxJsonBytes + 1);
        }

        viewModel.Sharing.ChooseImportFile = () => path;

        await viewModel.Sharing.ImportCommand.ExecuteAsync(null);

        Assert.Equal(Strings.ShareTooLarge, viewModel.StatusMessage);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task Import_FileGone_SaysItCouldNotBeRead()
    {
        using var viewModel = await ConnectAsync();
        viewModel.Sharing.ChooseImportFile = () => Path.Combine(_folder, "weg.brgb.json");

        await viewModel.Sharing.ImportCommand.ExecuteAsync(null);

        Assert.Equal(Strings.ShareReadFailed, viewModel.StatusMessage);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task PasteCode_SendsTheJsonInTheCode()
    {
        using var viewModel = await ConnectAsync();
        var json = Encoding.UTF8.GetBytes("""{ "version": 1, "kind": "scene" }""");
        _clipboard.Text = "  " + ShareCode.Encode(json) + "\r\n";

        await viewModel.Sharing.PasteCodeCommand.ExecuteAsync(null);

        Assert.Equal(json, Assert.Single(Api.ImportedShares));
    }

    // Someone who pasted the file itself in a chat: the JSON goes to the engine as it is.
    [Fact]
    public async Task PasteCode_JsonOnTheClipboard_SendsIt()
    {
        using var viewModel = await ConnectAsync();
        _clipboard.Text = """{ "version": 1, "kind": "effect" }""";

        await viewModel.Sharing.PasteCodeCommand.ExecuteAsync(null);

        Assert.Equal(_clipboard.Text, Encoding.UTF8.GetString(Assert.Single(Api.ImportedShares)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("hallo")]
    [InlineData("brgb1:kapot!")]
    public async Task PasteCode_NoCode_SaysSoAndSendsNothing(string? text)
    {
        using var viewModel = await ConnectAsync();
        _clipboard.Text = text;

        await viewModel.Sharing.PasteCodeCommand.ExecuteAsync(null);

        Assert.Equal(Strings.ShareNoCode, viewModel.StatusMessage);
        Assert.Empty(Api.Calls);
    }

    // A short code that unpacks to far more than a scene (a zip bomb) stops before the engine.
    [Fact]
    public async Task PasteCode_ThatUnpacksToTooMuch_SendsNothing()
    {
        using var viewModel = await ConnectAsync();
        using var packed = new MemoryStream();
        using (var brotli = new BrotliStream(packed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            brotli.Write(new byte[SharedFiles.MaxJsonBytes + 1]);
        }

        _clipboard.Text = ShareCode.Prefix + Base64Url.EncodeToString(packed.ToArray());

        await viewModel.Sharing.PasteCodeCommand.ExecuteAsync(null);

        Assert.Equal(Strings.ShareTooLarge, viewModel.StatusMessage);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task Import_RejectedByTheEngine_SaysNothingWasAdded()
    {
        using var viewModel = await ConnectAsync();
        Api.SetException = new RemoteInvocationException("no", 0, errorData: null);
        _clipboard.Text = ShareCode.Encode("{}"u8);

        await viewModel.Sharing.PasteCodeCommand.ExecuteAsync(null);

        Assert.Equal(Strings.ShareImportFailed, viewModel.StatusMessage);
    }

    // What the engine did, in plain words; a refusal added nothing.
    public static TheoryData<ShareImportResult, string> Results() => new()
    {
        { new(ShareImportOutcome.Imported, SharedKind.Scene, "Gamen", "Gamen", UnmatchedDevices: [], AddedEffects: []), "Scène 'Gamen' toegevoegd." },
        {
            new(ShareImportOutcome.Imported, SharedKind.Scene, "Gamen (2)", "Gamen", UnmatchedDevices: ["K70 RGB", "Fan 2"], AddedEffects: ["Disco"]),
            "Scène 'Gamen (2)' toegevoegd; 'Gamen' had je al. Wat niet paste: K70 RGB, Fan 2; die apparaten zijn hier niet gevonden. Eigen effecten erbij: Disco."
        },
        { new(ShareImportOutcome.Imported, SharedKind.Effect, "Disco", "Disco"), "Eigen effect 'Disco' toegevoegd; je kiest het bij Effect op Kleur." },
        { new(ShareImportOutcome.Imported, SharedKind.Effect, "Disco (2)", "Disco"), "Eigen effect 'Disco (2)' toegevoegd; 'Disco' had je al." },
        { new(ShareImportOutcome.Imported, SharedKind.Effect, "Disco", "Disco", AlreadyThere: true), "Het eigen effect 'Disco' had je al, precies zo. Er is niets toegevoegd." },
        { ShareImportResult.Refused(ShareImportOutcome.NotShared), Strings.ShareNotShared },
        { ShareImportResult.Refused(ShareImportOutcome.NewerVersion), Strings.ShareNewerVersion },
        { ShareImportResult.Refused(ShareImportOutcome.TooLarge), Strings.ShareTooLarge },
        { ShareImportResult.Refused(ShareImportOutcome.Invalid), Strings.ShareInvalid },
        { ShareImportResult.Refused(ShareImportOutcome.NoDevices), Strings.ShareNoDevices },
        { ShareImportResult.Refused(ShareImportOutcome.TooManyEffects), "Er passen hooguit 32 eigen effecten. Verwijder er eerst een. Er is niets toegevoegd." },
    };

    [Theory]
    [MemberData(nameof(Results))]
    public async Task Import_SaysWhatTheEngineDid(ShareImportResult result, string message)
    {
        using var viewModel = await ConnectAsync();
        Api.ImportResult = result;
        _clipboard.Text = ShareCode.Encode("{}"u8);

        await viewModel.Sharing.PasteCodeCommand.ExecuteAsync(null);

        Assert.Equal(message, viewModel.StatusMessage);
    }

    private string WriteFile(string name, byte[] content)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? setUp = null, IClipboard? clipboard = null)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api => setUp?.Invoke(api)), clipboard: clipboard ?? _clipboard);
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }

    private sealed class RefusingClipboard : IClipboard
    {
        public bool TrySetText(string text) => false;

        public string? GetText() => null;
    }
}
