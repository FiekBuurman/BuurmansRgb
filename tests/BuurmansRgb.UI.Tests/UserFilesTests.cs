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

    // Up to the limit, also over more than one chunk of the read; one byte more is too much.
    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 10)]
    [InlineData(200_000, 200_000)]
    public async Task ReadAtMost_UpToTheLimit_GivesTheBytes(int length, int maxBytes)
    {
        var content = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
        var path = Write("scene.brgb.json", content);

        Assert.Equal(content, await UserFiles.ReadAtMostAsync(path, maxBytes));
    }

    [Theory]
    [InlineData(11, 10)]
    [InlineData(200_001, 200_000)]
    public async Task ReadAtMost_OverTheLimit_GivesNull(int length, int maxBytes)
    {
        var path = Write("scene.brgb.json", new byte[length]);

        Assert.Null(await UserFiles.ReadAtMostAsync(path, maxBytes));
    }

    [Fact]
    public async Task ReadAtMost_FileThatIsNotThere_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => UserFiles.ReadAtMostAsync(Path.Combine(_folder, "weg.brgb.json"), 10));
    }

    // Windows gives a length of 0 for a link; what counts is what it leads to.
    [Fact]
    public async Task ReadAtMost_LinkToALargeFile_GivesNull()
    {
        var target = Write("groot.brgb.json", new byte[100]);
        var link = Path.Combine(_folder, "link.brgb.json");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("Making a symbolic link needs developer mode or administrator rights.");
        }

        Assert.Equal(0, new FileInfo(link).Length);
        Assert.Null(await UserFiles.ReadAtMostAsync(link, 10));
    }

    private string Write(string name, byte[] content)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, content);
        return path;
    }
}
