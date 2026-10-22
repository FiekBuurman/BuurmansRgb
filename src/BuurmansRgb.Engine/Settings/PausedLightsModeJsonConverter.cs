using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Settings;

// "pausedLights" in settings.json: "keep", "dim" or "off". A word this version does not know, for
// example from a newer version, reads as keep, as does any other value, such as a number, null or
// an object: keep is what the lights did before there was a choice.
internal sealed class PausedLightsModeJsonConverter() : LenientEnumJsonConverter<PausedLightsMode>(PausedLightsMode.Keep);
