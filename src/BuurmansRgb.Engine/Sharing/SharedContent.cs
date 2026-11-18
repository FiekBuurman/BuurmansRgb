using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Sharing;

// What SharedFileReader found: with Outcome Imported a Scene or, for an own effect, Effect; else
// neither, and why not.
internal sealed record SharedContent(ShareImportOutcome Outcome, SharedKind Kind = SharedKind.Scene, SharedScene? Scene = null, CustomEffect? Effect = null)
{
    public static SharedContent Refused(ShareImportOutcome outcome) => new(outcome);
}

// A shared scene as it came: its lighting by the device ids of the pc it came from (Devices says
// which device each is), the devices it is for (null for every device), and the own effects its
// lighting shows, by their ids there.
internal sealed record SharedScene(
    string Name,
    LightingState Lighting,
    IReadOnlyList<SharedDeviceRef> Devices,
    IReadOnlyList<string>? OnlyDevices,
    IReadOnlyList<CustomEffect> Effects);

// A device of a shared scene: its id on the pc it came from, and its type and name when that pc
// knew them.
internal sealed record SharedDeviceRef(string Id, DeviceType? Type, string? Name);
