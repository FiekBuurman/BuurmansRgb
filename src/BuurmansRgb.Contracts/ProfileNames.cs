namespace BuurmansRgb.Contracts;

// The rules for the name of a saved profile, shared so the UI can check a name before it asks.
public static class ProfileNames
{
    public const int MaxLength = 40;

    // Names match without regard to case, so "Gamen" and "gamen" are the same profile.
    public static StringComparer Comparer { get; } = StringComparer.CurrentCultureIgnoreCase;
}
