using System.Globalization;

namespace BuurmansRgb.Contracts;

// Sharing a scene or an own effect with someone else: as a file (Extension) or as a short code to
// paste (ShareCode), both the same JSON, which the engine makes and reads
// (IEngineApi.ExportSceneAsync, ExportCustomEffectAsync and ImportSharedAsync). The limits are here
// so the settings app refuses a file or code that is too large before it reads or sends it, and
// the engine refuses it again.
public static class SharedFiles
{
    // The extension the dialogs suggest and filter on.
    public const string Extension = ".brgb.json";

    // A scene with a color per LED on twenty devices and a few own effects is some tens of KB; an
    // own effect at its largest (CustomEffects.MaxSteps steps of CustomEffects.MaxLeds colors) is
    // about 350 KB. More is not a scene or effect of the app, and is not read.
    public const int MaxJsonBytes = 2 * 1024 * 1024;

    // The most devices and own effects one scene names; more devices than a pc has, and as many
    // effects as the engine keeps (CustomEffects.MaxEffects).
    public const int MaxDevices = 64;

    public const int MaxEffects = CustomEffects.MaxEffects;

    // The most LEDs of a device with a color per LED, and the most zones they were chosen for.
    public const int MaxLedsPerDevice = 4096;

    public const int MaxZones = 64;

    // The longest id and name of a device; longer ones are no device of OpenRGB or the simulation.
    public const int MaxDeviceIdLength = 200;

    public const int MaxDeviceNameLength = 200;

    // Characters of a path ("C:\", "..\", "a/b"), which a shared name may not have: the app makes a
    // file name of it. A scene or effect of this pc with one of them is shared with a '-' in its
    // place (ForSharing).
    private static readonly char[] PathCharacters = ['\\', '/', ':'];

    // Whether the name has a character of a path in it.
    public static bool IsPathLike(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.IndexOfAny(PathCharacters) >= 0;
    }

    // The name, or when that is taken the first of "name (2)", "name (3)" and so on that is free,
    // with the end of the name cut off where the number would make it longer than maxLength, but not
    // in the middle of a character.
    public static string FreeName(string name, int maxLength, Func<string, bool> isTaken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(isTaken);
        if (!isTaken(name))
        {
            return name;
        }

        for (var number = 2; ; number++)
        {
            var suffix = string.Create(CultureInfo.InvariantCulture, $" ({number})");
            // Not between the two halves of a character outside the BMP, such as an emoji.
            var length = Math.Min(name.Length, maxLength - suffix.Length);
            if (length is > 0 && length < name.Length && char.IsHighSurrogate(name[length - 1]))
            {
                length--;
            }

            var candidate = name[..length].TrimEnd() + suffix;
            if (!isTaken(candidate))
            {
                return candidate;
            }
        }
    }

    // The name with a '-' for each character of a path.
    public static string ForSharing(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return string.Create(name.Length, name, (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = Array.IndexOf(PathCharacters, source[i]) >= 0 ? '-' : source[i];
            }
        });
    }
}
