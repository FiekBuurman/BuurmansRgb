namespace BuurmansRgb.Contracts;

// RGB software besides OpenRGB that may drive the same LEDs and so overwrite the colors, such as
// SignalRGB or iCUE, as the engine found it running (IEngineApi.GetOtherRgbSoftwareAsync). Id stays
// the same between versions: the engine keeps the ones the user dismissed by it in settings.json.
// Name is the name of the program as its maker writes it, for the message in the app.
public sealed record OtherRgbSoftware(string Id, string Name)
{
    // A second OpenRGB in the session of a user, next to the service OpenRGB that the installer
    // sets up: the app words that message in its own way.
    public const string OpenRgbId = "openrgb";
}
