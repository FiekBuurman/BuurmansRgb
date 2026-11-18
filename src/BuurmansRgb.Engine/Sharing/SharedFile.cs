using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Sharing;

// The JSON of a shared scene or own effect (Contracts.SharedFiles), version 1. Read strictly, unlike
// the files of the engine: it comes from someone else, so a field the engine does not know, a
// name twice in one object, a value of the wrong type or a missing one that is required refuses
// the whole file (SharingJsonContext), and SharedFileReader checks every value. A scene:
//
// { "version": 1, "kind": "scene", "name": "Gamen",
//   "lighting": { "color": "#FF0000", "brightness": 1, "effect": "custom:1a2b3c4d" },
//   "devices": [ { "id": "openrgb:k70-1a2b3c4d", "type": "keyboard", "name": "K70",
//                  "lighting": { ... }, "ledColors": [ "#FF0000", null ], "zoneLengths": [ 104 ] } ],
//   "onlyDevices": [ "openrgb:k70-1a2b3c4d" ],
//   "effects": [ { "id": "custom:1a2b3c4d", "name": "Disco",
//                  "steps": [ { "colors": [ "#FF0000" ], "hold": 1, "fade": 1 } ] } ] }
//
// An own effect: { "version": 1, "kind": "effect", "effects": [ { ... } ] }, with one effect.
internal sealed record SharedFile
{
    public required int Version { get; init; }

    public required string Kind { get; init; }

    public string? Name { get; init; }

    public SharedLighting? Lighting { get; init; }

    public List<SharedDevice>? Devices { get; init; }

    public List<string>? OnlyDevices { get; init; }

    public List<SharedEffect>? Effects { get; init; }
}

// The lighting of all devices of a scene, or of one device; as DeviceLighting. Left out is the
// default: speed EffectSpeed.Default, along the LEDs, on, the default loop and no own colors.
internal sealed record SharedLighting
{
    public required RgbColor Color { get; init; }

    public required double Brightness { get; init; }

    public required string Effect { get; init; }

    public double? Speed { get; init; }

    public int? Direction { get; init; }

    public bool? On { get; init; }

    public SharedColorLoop? ColorLoop { get; init; }

    public Dictionary<string, List<RgbColor>>? EffectColors { get; init; }
}

internal sealed record SharedColorLoop
{
    public required List<RgbColor> Colors { get; init; }

    public required bool Spread { get; init; }
}

// A device of a scene: its id on the pc it came from and, when that pc knew it then, its type and
// name, by which another pc finds its own; its lighting when it differs from that of the scene, and
// its color per LED with the zone lengths they were chosen for.
internal sealed record SharedDevice
{
    public required string Id { get; init; }

    public string? Type { get; init; }

    public string? Name { get; init; }

    public SharedLighting? Lighting { get; init; }

    public List<RgbColor?>? LedColors { get; init; }

    public List<int>? ZoneLengths { get; init; }
}

internal sealed record SharedEffect
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required List<SharedStep> Steps { get; init; }
}

internal sealed record SharedStep
{
    public required List<RgbColor> Colors { get; init; }

    public required double Hold { get; init; }

    public required double Fade { get; init; }
}
