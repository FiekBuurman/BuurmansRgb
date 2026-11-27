using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Settings;

// "Instellingen bewaren…" and "Instellingen terugzetten…" (IEngineApi.ExportSettingsAsync and
// RestoreSettingsAsync), which SettingsTransfer does for EngineHost, as the host holds the savers of
// the files.
internal interface ISettingsTransfer
{
    byte[] Export();

    SettingsRestoreResult Restore(byte[] archive);
}
