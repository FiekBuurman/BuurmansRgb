using System.Text.Json;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Settings;

// "simulatedDevices" in settings.json: "automatic", "always" or "never". A word leaves room for more
// modes later; one this version does not know, for example from a newer version, reads as
// automatic. Older files hold true or false. True was the default that the engine wrote itself, not
// a choice of the user, so it reads as automatic; false stays never. Any other value, such as a
// number, null or an object, also reads as automatic: throwing would make the engine run on the
// defaults and the next save would overwrite all the lighting in the file.
internal sealed class SimulatedDevicesModeJsonConverter() : LenientEnumJsonConverter<SimulatedDevicesMode>(SimulatedDevicesMode.Automatic)
{
    protected override SimulatedDevicesMode? ReadOther(JsonTokenType token) => token switch
    {
        JsonTokenType.True => SimulatedDevicesMode.Automatic,
        JsonTokenType.False => SimulatedDevicesMode.Never,
        _ => null,
    };
}
