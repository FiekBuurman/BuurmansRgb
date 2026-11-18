using System.IO;
using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

// Writing and reading a file the user chose: whole or not at all.
public sealed class UserFilesTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("BuurmansRgb.UI.Tests.").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task WriteReplacing_WritesTheFile_AlsoOverAnOldOne()
    {
        var path = Path.Combine(_folder, "Gamen.brgb.json");
        File.WriteAllBytes(path, [9, 9, 9, 9]);

        await UserFiles.WriteReplacingAsync(path, [1, 2, 3]);

        Assert.Equal([1, 2, 3], File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFiles(_folder));
    }

    // The move that fails, here over a folder of that name, leaves no temporary file behind.
    [Fact]
    public async Task WriteReplacing_ThatFails_LeavesNoTemporaryFile()
    {
        var path = Path.Combine(_folder, "Gamen.brgb.json");
        Directory.CreateDirectory(path);

        var exception = await Record.ExceptionAsync(() => UserFiles.WriteReplacingAsync(path, [1, 2, 3]));

        Assert.True(exception is IOException or UnauthorizedAccessException, exception?.ToString());
        Assert.Empty(Directory.GetFiles(_folder));
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public async Task WriteReplacing_FolderThatIsNotThere_Throws()
    {
        var path = Path.Combine(_folder, "weg", "Gamen.brgb.json");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => UserFiles.WriteReplacingAsync(path, [1, 2, 3]));
    }
}
