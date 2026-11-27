using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// How the engine gets on with the devices, in plain words ("5 apparaten verbonden", "Geen
// verbinding met je apparaten"), with the technical status (OpenRGB, its SDK server) and a tip
// behind it: in the tooltip, and spelled out on Instellingen. Without a connection, "Herstarten"
// does what the item in the tray menu does; this only asks, MainViewModel sends it to the engine.
public sealed partial class OpenRgbStatusViewModel : ObservableObject
{
    // Null while nothing is known about OpenRGB (no engine), so nothing stays shown.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text), nameof(TechnicalText), nameof(Hint), nameof(HasHint), nameof(ToolTip), nameof(IsDisconnected), nameof(IsConnectionLost), nameof(CanRestart))]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
    public partial ServerStatus? Status { get; private set; } = ServerStatus.Connecting;

    // Whether a real device was ever in the list on this pc (MainViewModel.HadRealDevices). Without,
    // OpenRGB out of reach is the normal state of someone who tries the app with the simulated
    // devices: nothing is broken, so the status stays calm and "Herstarten" stays away.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text), nameof(Hint), nameof(HasHint), nameof(ToolTip), nameof(IsConnectionLost), nameof(CanRestart))]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
    public partial bool DevicesExpected { get; set; }

    // Raised when the user asks for "Herstarten".
    public event EventHandler? RestartRequested;

    public string Text => Status switch
    {
        null => string.Empty,
        { State: ServerState.Connected, DeviceCount: 0 } => Strings.DevicesConnectedNone,
        { State: ServerState.Connected, DeviceCount: 1 } => Strings.DevicesConnectedOne,
        { State: ServerState.Connected } status => UiText.Format(Strings.DevicesConnected, status.DeviceCount),
        { State: ServerState.Unreachable } => DevicesExpected ? Strings.DevicesUnreachable : Strings.DevicesNoneYet,
        { State: ServerState.Failed } => Strings.DevicesFailed,
        _ => Strings.DevicesConnecting,
    };

    // The same status as the tooltip of the tray icon says it, with the address of the SDK server.
    public string TechnicalText => Status switch
    {
        null => string.Empty,
        { State: ServerState.Connected, DeviceCount: 0 } => UiText.Format(Strings.OpenRgbTechnical, Strings.OpenRgbConnectedNoDevices),
        { State: ServerState.Connected, DeviceCount: 1 } => UiText.Format(Strings.OpenRgbTechnical, Strings.OpenRgbConnectedOneDevice),
        { State: ServerState.Connected } status => UiText.Format(Strings.OpenRgbTechnical, UiText.Format(Strings.OpenRgbConnected, status.DeviceCount)),
        { State: ServerState.Unreachable } => UiText.Format(Strings.OpenRgbTechnical, Strings.OpenRgbUnreachable),
        { State: ServerState.Failed } => UiText.Format(Strings.OpenRgbTechnical, Strings.OpenRgbFailed),
        _ => UiText.Format(Strings.OpenRgbTechnical, Strings.OpenRgbConnecting),
    };

    // What to do about it, when there is something to do.
    public string? Hint => Status?.State switch
    {
        ServerState.Unreachable => DevicesExpected ? Strings.OpenRgbUnreachableHint : Strings.OpenRgbUnreachableNoDevicesHint,
        ServerState.Failed => Strings.OpenRgbFailedHint,
        _ => null,
    };

    public bool HasHint => Hint is not null;

    // The technical status with the tip, for the tooltip of the status and the ⓘ.
    public string? ToolTip => Status is null ? null : Hint is null ? TechnicalText : $"{TechnicalText}\n{Hint}";

    // Nothing answered at the SDK server, or it gave no good answer: the devices are out of reach.
    public bool IsDisconnected => Status?.State is ServerState.Unreachable or ServerState.Failed;

    // The connection with the devices is lost: out of reach on a pc that had real devices. Without,
    // the simulated devices are all there is, and the calm status at the top right says enough.
    public bool IsConnectionLost => DevicesExpected && IsDisconnected;

    // "Herstarten" only when the connection is the trouble: OpenRGB that answers wrongly, or out of
    // reach on a pc that had real devices. Connected or connecting it would only interrupt, and
    // without real devices there is nothing to bring back.
    public bool CanRestart => Status?.State is ServerState.Failed || (Status?.State is ServerState.Unreachable && DevicesExpected);

    // Without the engine there is nothing known about OpenRGB, so nothing stays shown.
    public void Clear() => Status = null;

    public void Show(ServerStatus status) => Status = status;

    [RelayCommand(CanExecute = nameof(CanRestart))]
    private void Restart() => RestartRequested?.Invoke(this, EventArgs.Empty);
}
