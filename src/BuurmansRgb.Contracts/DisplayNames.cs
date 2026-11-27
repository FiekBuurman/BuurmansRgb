namespace BuurmansRgb.Contracts;

// The rules for a name the user gives to something that is shown and saved, such as a scene
// (SceneNames) or an own effect (CustomEffects), shared so the UI can check a name before it asks.
public static class DisplayNames
{
    // The name without surrounding spaces, or null when it is empty, longer than maxLength or holds
    // a control character such as a line break.
    public static string? Normalize(string? name, int maxLength)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength || trimmed.Any(char.IsControl) ? null : trimmed;
    }
}
