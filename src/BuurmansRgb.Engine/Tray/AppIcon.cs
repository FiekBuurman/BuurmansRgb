namespace BuurmansRgb.Engine.Tray;

internal static class AppIcon
{
    private const string ResourceName = "BuurmansRgb.ico";

    public static Icon LoadSmall()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        return new Icon(stream, SystemInformation.SmallIconSize);
    }
}
