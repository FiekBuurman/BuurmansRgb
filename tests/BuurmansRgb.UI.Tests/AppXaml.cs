using System.Text.RegularExpressions;

namespace BuurmansRgb.UI.Tests;

// The XAML of the settings app as text, read from the repo, for the tests that check how it refers
// to its resources.
internal static partial class AppXaml
{
    // Every .xaml file under src/BuurmansRgb.UI, by its path from there, with its text.
    public static IEnumerable<(string Name, string Xaml)> Files()
    {
        var source = RepoFiles.AppSource;
        return Directory.EnumerateFiles(source, "*.xaml", SearchOption.AllDirectories)
            .Select(file => (Path.GetRelativePath(source, file), File.ReadAllText(file)));
    }

    // The keys a XAML refers to with StaticResource, as a markup extension in an attribute (with or
    // without ResourceKey=) or as an element of its own, where ResourceKey may be on another line.
    public static IEnumerable<(string Key, int Index)> StaticReferences(string xaml) =>
        StaticResourcePattern().Matches(xaml).Select(match => (match.Groups["key"].Value, match.Index));

    [GeneratedRegex("""\{StaticResource\s+(?:ResourceKey\s*=\s*)?(?<key>[^\s,}]+)\s*\}|<StaticResource(?:Extension)?\b[^>]*?\bResourceKey\s*=\s*"(?<key>[^"]+)"|<StaticResource(?:Extension)?\b[^>]*?\bResourceKey\s*=\s*'(?<key>[^']+)'""")]
    private static partial Regex StaticResourcePattern();

    // The line, counted from 1, that index lies on.
    public static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;
}
