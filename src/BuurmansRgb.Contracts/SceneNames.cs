namespace BuurmansRgb.Contracts;

// The rules for the name of a saved scene, shared so the UI can check a name before it asks.
public static class SceneNames
{
    public const int MaxLength = 40;

    // Names match without regard to case, so "Gamen" and "gamen" are the same scene.
    public static StringComparer Comparer { get; } = StringComparer.CurrentCultureIgnoreCase;
}
