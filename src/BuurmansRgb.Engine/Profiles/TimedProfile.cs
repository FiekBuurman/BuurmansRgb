using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Profiles;

// A profile with times, for ProfileTimer: a scene by its name, or an internal profile of "Uit om"
// or "Aan om" (ProfileRole), whose Name is its role.
internal readonly record struct TimedProfile(string Name, ProfileRole Role, bool IsInternal, IReadOnlyList<LoadTime> Times);
