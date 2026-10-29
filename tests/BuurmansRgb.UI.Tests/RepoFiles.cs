namespace BuurmansRgb.UI.Tests;

// The repo the tests are built in, for a test that reads a source file of the app itself.
internal static class RepoFiles
{
    // The folder with BuurmansRgb.slnx, above the output of the tests.
    public static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BuurmansRgb.slnx")))
            {
                directory = directory.Parent;
            }

            return Assert.IsType<DirectoryInfo>(directory).FullName;
        }
    }

    // The source of the settings app, src/BuurmansRgb.UI.
    public static string AppSource => Path.Combine(Root, "src", "BuurmansRgb.UI");
}
