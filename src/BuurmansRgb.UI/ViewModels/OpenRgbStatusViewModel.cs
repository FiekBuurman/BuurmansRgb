using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// How the engine gets on with OpenRGB, in plain words, with a tip when that helps the user.
public sealed partial class OpenRgbStatusViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Text { get; private set; } = Strings.OpenRgbConnecting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHint))]
    public partial string? Hint { get; private set; }

    public bool HasHint => Hint is not null;

    // Without the engine there is nothing known about OpenRGB, so nothing stays shown.
    public void Clear() => (Text, Hint) = (string.Empty, null);

    public void Show(ServerStatus status)
    {
        (Text, Hint) = status.State switch
        {
            ServerState.Connected when status.DeviceCount == 0 => (Strings.OpenRgbConnectedNoDevices, null),
            ServerState.Connected when status.DeviceCount == 1 => (Strings.OpenRgbConnectedOneDevice, null),
            ServerState.Connected => (UiText.Format(Strings.OpenRgbConnected, status.DeviceCount), null),
            ServerState.Unreachable => (Strings.OpenRgbUnreachable, Strings.OpenRgbUnreachableHint),
            ServerState.Failed => (Strings.OpenRgbFailed, Strings.OpenRgbFailedHint),
            _ => (Strings.OpenRgbConnecting, (string?)null),
        };
    }
}
