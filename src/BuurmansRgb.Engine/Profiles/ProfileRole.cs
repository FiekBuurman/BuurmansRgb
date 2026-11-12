namespace BuurmansRgb.Engine.Profiles;

// What the engine uses a profile for besides a scene: "Uit om" and "Aan om" in the block Hele huis
// on Apparaten, which put on a profile at its times as any scene with a time. The role is saved in
// profiles.json ("role"), so the engine finds the profile also after the user renamed it by hand;
// a file without it, from before, has no profile with a role.
internal enum ProfileRole
{
    None,

    // "Uit om": the start scene "Alles uit", or an internal profile when that scene is gone, which
    // switches every device off and keeps its lighting, as "Alles uit" in the tray menu.
    AllOff,

    // "Aan om": always internal. Its lighting is what burned before "Alles uit" came on, which it
    // puts back with every device on (ProfileLibrary.LoadTimed).
    BackOn,
}

internal static class ProfileRoles
{
    public static string? Write(ProfileRole role) => role switch
    {
        ProfileRole.AllOff => "allOff",
        ProfileRole.BackOn => "backOn",
        _ => null,
    };

    // Without regard to case; anything else is None.
    public static ProfileRole Read(string? text) =>
        string.Equals(text, "allOff", StringComparison.OrdinalIgnoreCase) ? ProfileRole.AllOff
        : string.Equals(text, "backOn", StringComparison.OrdinalIgnoreCase) ? ProfileRole.BackOn
        : ProfileRole.None;
}
