namespace BuurmansRgb.Contracts;

// The rules for the name of a saved profile, shared so the UI can check a name before it asks.
public static class ProfileNames
{
    public const int MaxLength = 40;

    // Names match without regard to case, so "Gamen" and "gamen" are the same profile.
    public static StringComparer Comparer { get; } = StringComparer.CurrentCultureIgnoreCase;

    // The name without surrounding spaces, or null when it is empty, too long or holds a control
    // character such as a line break.
    public static string? Normalize(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength || trimmed.Any(char.IsControl) ? null : trimmed;
    }
}
