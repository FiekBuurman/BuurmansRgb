using System.Diagnostics;
using System.IO;
using System.Text;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// Sharing a scene or own effect: "Exporteren…" and "Code kopiëren" at a chosen scene on Scènes and
// at an own effect on Kleur, "Importeren…" and "Code plakken" on both pages. The engine makes the
// JSON and reads one back with every check (ExportSceneAsync, ExportCustomEffectAsync,
// ImportSharedAsync); this app only picks the file and writes or reads it, puts the code on the
// clipboard (ShareCode) or reads it from there. The window gives the dialogs (ChooseExportFile,
// ChooseImportFile); without them, as in a test that sets none, nothing happens. What came of it
// goes to the status line (Message), a live region.
public sealed partial class SharingViewModel(Func<IEngineApi?> api, IClipboard clipboard) : ObservableObject
{
    // Raised with the message for the status line.
    public event EventHandler<string>? Message;

    // Raised with the result of an import that added a scene or own effect, so the lists show it.
    public event EventHandler<ShareImportResult>? Imported;

    // The save dialog with a title and a suggested file name; the path the user chose, or null.
    public Func<string, string, string?>? ChooseExportFile { get; set; }

    // The open dialog; the path the user chose, or null.
    public Func<string?>? ChooseImportFile { get; set; }

    // Only with an engine to ask.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportSceneCommand), nameof(CopySceneCodeCommand), nameof(ExportEffectCommand), nameof(CopyEffectCodeCommand), nameof(ImportCommand), nameof(PasteCodeCommand))]
    public partial bool IsConnected { get; set; }

    // While one runs, the buttons wait.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportSceneCommand), nameof(CopySceneCodeCommand), nameof(ExportEffectCommand), nameof(CopyEffectCodeCommand), nameof(ImportCommand), nameof(PasteCodeCommand))]
    public partial bool IsBusy { get; private set; }

    // "Gamen.brgb.json": the name, with a '-' for a character Windows does not take in a file name.
    public static string FileNameFor(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(character => invalid.Contains(character) ? '-' : character)).Trim().TrimEnd('.') + SharedFiles.Extension;
    }

    private bool CanRun => IsConnected && !IsBusy;

    private bool CanShareScene(string? name) => CanRun && !string.IsNullOrEmpty(name);

    private bool CanShareEffect(CustomEffectEditorViewModel? effect) => CanRun && effect is { EffectId.Length: > 0 };

    [RelayCommand(CanExecute = nameof(CanShareScene))]
    private Task ExportSceneAsync(string? name) =>
        ExportAsync(name!, Strings.ShareExportSceneTitle, engine => engine.ExportSceneAsync(name!));

    [RelayCommand(CanExecute = nameof(CanShareScene))]
    private Task CopySceneCodeAsync(string? name) =>
        CopyCodeAsync(name!, engine => engine.ExportSceneAsync(name!));

    [RelayCommand(CanExecute = nameof(CanShareEffect))]
    private Task ExportEffectAsync(CustomEffectEditorViewModel? effect)
    {
        var (id, name) = (effect!.EffectId, effect.Effect.Name);
        return ExportAsync(name, Strings.ShareExportEffectTitle, engine => engine.ExportCustomEffectAsync(id));
    }

    [RelayCommand(CanExecute = nameof(CanShareEffect))]
    private Task CopyEffectCodeAsync(CustomEffectEditorViewModel? effect)
    {
        var (id, name) = (effect!.EffectId, effect.Effect.Name);
        return CopyCodeAsync(name, engine => engine.ExportCustomEffectAsync(id));
    }

    // "Importeren…": a file of at most SharedFiles.MaxJsonBytes, read whole and sent to the engine.
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task ImportAsync()
    {
        if (ChooseImportFile?.Invoke() is not { } path || api() is not { } engine)
        {
            return;
        }

        IsBusy = true;
        try
        {
            byte[]? json;
            try
            {
                json = await UserFiles.ReadAtMostAsync(path, SharedFiles.MaxJsonBytes);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Trace.TraceError(exception.ToString());
                Say(Strings.ShareReadFailed);
                return;
            }

            if (json is null)
            {
                Say(Strings.ShareTooLarge);
                return;
            }

            await ImportOnAsync(engine, json);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // "Code plakken": the code on the clipboard (ShareCode), or the JSON of a file someone pasted.
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task PasteCodeAsync()
    {
        if (api() is not { } engine)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var text = clipboard.GetText();
            byte[] json;
            if (text is not null && text.AsSpan().TrimStart().StartsWith("{"))
            {
                if (Encoding.UTF8.GetByteCount(text) > SharedFiles.MaxJsonBytes)
                {
                    Say(Strings.ShareTooLarge);
                    return;
                }

                json = Encoding.UTF8.GetBytes(text);
            }
            else if (!ShareCode.TryDecode(text, out var decoded, out var error))
            {
                Say(error == ShareImportOutcome.TooLarge ? Strings.ShareTooLarge : Strings.ShareNoCode);
                return;
            }
            else
            {
                json = decoded;
            }

            await ImportOnAsync(engine, json);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal static string MessageFor(ShareImportResult result) => result.Outcome switch
    {
        ShareImportOutcome.Imported => ImportedMessage(result),
        ShareImportOutcome.NotShared => Strings.ShareNotShared,
        ShareImportOutcome.NewerVersion => Strings.ShareNewerVersion,
        ShareImportOutcome.TooLarge => Strings.ShareTooLarge,
        ShareImportOutcome.NoDevices => Strings.ShareNoDevices,
        ShareImportOutcome.TooManyEffects => UiText.Format(Strings.ShareTooManyEffects, CustomEffects.MaxEffects),
        _ => Strings.ShareInvalid,
    };

    // "Scène 'Gamen (2)' toegevoegd; 'Gamen' had je al. Wat niet paste: K70 RGB; niet gevonden op
    // deze pc. Eigen effecten erbij: Disco."
    private static string ImportedMessage(ShareImportResult result)
    {
        var name = result.Name ?? string.Empty;
        var message = result switch
        {
            { Kind: SharedKind.Effect, AlreadyThere: true } => UiText.Format(Strings.ShareEffectAlreadyThere, name),
            { Kind: SharedKind.Effect, IsRenamed: true } => UiText.Format(Strings.ShareEffectImportedRenamed, name, result.SharedName),
            { Kind: SharedKind.Effect } => UiText.Format(Strings.ShareEffectImported, name),
            { IsRenamed: true } => UiText.Format(Strings.ShareSceneImportedRenamed, name, result.SharedName),
            _ => UiText.Format(Strings.ShareSceneImported, name),
        };
        if (UnmatchedDevices(result) is { } unmatched)
        {
            message += " " + UiText.Format(Strings.ShareUnmatchedDevices, unmatched);
        }

        if (result.AddedEffects is { Count: > 0 } effects)
        {
            message += " " + UiText.Format(Strings.ShareAddedEffects, string.Join(", ", effects));
        }

        return message;
    }

    // "K70 RGB, Fan 2 en 1 apparaat zonder naam": the devices without a name are only counted, as
    // their id means nothing to the user. Null when every device was found.
    private static string? UnmatchedDevices(ShareImportResult result)
    {
        var named = string.Join(", ", result.UnmatchedDevices ?? []);
        var count = result.UnnamedUnmatchedDevices;
        var unnamed = count <= 0 ? null : UiText.ForCount(count, Strings.ShareUnnamedDevicesOne, Strings.ShareUnnamedDevices, count);
        return (named, unnamed) switch
        {
            ("", null) => null,
            (_, null) => named,
            ("", _) => unnamed,
            _ => UiText.Format(Strings.ShareNamedAndUnnamedDevices, named, unnamed),
        };
    }

    private async Task ExportAsync(string name, string title, Func<IEngineApi, Task<byte[]>> export)
    {
        if (ChooseExportFile?.Invoke(title, FileNameFor(name)) is not { } path || api() is not { } engine)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await UserFiles.WriteReplacingAsync(path, await export(engine));

            Say(UiText.Format(Strings.ShareExported, name, Path.GetFileName(path)));
        }
        catch (Exception exception)
        {
            // A folder without write access, a full disk, a scene deleted elsewhere, or the engine
            // that went meanwhile.
            Trace.TraceError(exception.ToString());
            Say(Strings.ShareExportFailed);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CopyCodeAsync(string name, Func<IEngineApi, Task<byte[]>> export)
    {
        if (api() is not { } engine)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var code = ShareCode.Encode(await export(engine));
            Say(clipboard.TrySetText(code) ? UiText.Format(Strings.ShareCodeCopied, name) : Strings.ShareCopyFailed);
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
            Say(Strings.ShareExportFailed);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Only an engine that refused the call added nothing for sure; one that went meanwhile or did
    // not answer in time may have added it.
    private async Task ImportOnAsync(IEngineApi engine, byte[] json)
    {
        ShareImportResult result;
        try
        {
            result = await engine.ImportSharedAsync(json);
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
            Say(EngineRpc.IsRejectedByEngine(exception) ? Strings.ShareImportFailed : Strings.ShareImportUnknown);
            return;
        }

        Say(MessageFor(result));
        if (result.Outcome == ShareImportOutcome.Imported)
        {
            Imported?.Invoke(this, result);
        }
    }

    private void Say(string message) => Message?.Invoke(this, message);
}
