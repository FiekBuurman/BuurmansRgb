using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Profiles;

// All the lighting, saved under a name to go back to later, and when the engine loads it by itself.
// Role says what else the engine uses it for ("Uit om" and "Aan om" of Hele huis, see
// ProfileRole). An internal one is no scene of the user: the engine keeps it for its role only, so
// it is in no list of names, the tray menu or --scene, and Name is no more than its role.
internal sealed record Profile(string Name, LightingState Lighting)
{
    public ProfileAutomation Automation { get; init; } = ProfileAutomation.None;

    public ProfileRole Role { get; init; }

    public bool IsInternal { get; init; }

    // The devices the scene is for, by their id, in the order the user saw them; null for every
    // device, also one that comes later. A device that is not in it keeps its own lighting when the
    // scene comes on (LightingOf). Never empty, and always null for "Alles uit" (ProfileRole.AllOff),
    // which switches every device off for "Uit om" and "Aan om".
    public IReadOnlyList<string>? DeviceIds { get; init; }

    // An internal profile for role, which has no name of the user.
    public static Profile Internal(ProfileRole role, LightingState lighting, ProfileAutomation automation) =>
        new(ProfileRoles.Write(role)!, lighting) { Role = role, IsInternal = true, Automation = automation };

    // What burns once the scene is on over now: its whole lighting, or for a scene of some devices
    // that of those devices over now (LightingState.WithDevicesFrom).
    public LightingState LightingOver(LightingState now) => DeviceIds is null ? Lighting : now.WithDevicesFrom(Lighting, DeviceIds);
}
