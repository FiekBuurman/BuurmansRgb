using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

// The zip of "Instellingen bewaren…" and every check before "terugzetten…" replaces anything.
public sealed class SettingsArchiveTests
{
    private static readonly byte[] Settings = Encoding.UTF8.GetBytes("""{ "brightness": 0.5, "framesPerSecond": 15 }""");
    private static readonly byte[] Profiles = Encoding.UTF8.GetBytes("""{ "profiles": [ { "name": "Nacht" } ] }""");
    private static readonly byte[] Effects = Encoding.UTF8.GetBytes("""{ "effects": [] }""");

    [Fact]
    public void Create_ThenRead_GivesTheSameFiles()
    {
        var archive = SettingsArchive.Create(new Dictionary<string, byte[]>
        {
            [SettingsBackup.SettingsFileName] = Settings,
            [SettingsBackup.ProfilesFileName] = Profiles,
            [SettingsBackup.EffectsFileName] = Effects,
        });

        var content = SettingsArchive.Read(archive);

        Assert.Null(content.Error);
        Assert.Equal(Settings, content.Files[SettingsBackup.SettingsFileName]);
        Assert.Equal(Profiles, content.Files[SettingsBackup.ProfilesFileName]);
        Assert.Equal(Effects, content.Files[SettingsBackup.EffectsFileName]);
    }

    // Each file at the top of the zip under its own name, so the zip opens in Verkenner as the
    // three files; one that is not there (no scenes yet) is left out.
    [Fact]
    public void Create_PutsTheFilesThereAreAtTheTop()
    {
        var archive = SettingsArchive.Create(new Dictionary<string, byte[]> { [SettingsBackup.SettingsFileName] = Settings });

        using var zip = new ZipArchive(new MemoryStream(archive));
        Assert.Equal([SettingsBackup.SettingsFileName], zip.Entries.Select(entry => entry.FullName));
    }

    // A zip with only some of the files restores those.
    [Fact]
    public void Read_OnlySomeFiles_GivesThose()
    {
        var content = SettingsArchive.Read(Zip((SettingsBackup.ProfilesFileName, Profiles)));

        Assert.Null(content.Error);
        Assert.Equal([SettingsBackup.ProfilesFileName], content.Files.Keys);
    }

    // As Windows makes no difference: SETTINGS.JSON is settings.json.
    [Fact]
    public void Read_NameInAnotherCase_IsTheFile()
    {
        var content = SettingsArchive.Read(Zip(("Settings.JSON", Settings)));

        Assert.Equal(Settings, content.Files[SettingsBackup.SettingsFileName]);
    }

    [Fact]
    public void Read_NotAZip_IsNotABackup()
    {
        var content = SettingsArchive.Read(Settings);

        Assert.Equal(new SettingsRestoreResult(SettingsRestoreOutcome.NotABackup), content.Error);
        Assert.Empty(content.Files);
    }

    [Fact]
    public void Read_EmptyZip_IsNotABackup()
    {
        Assert.Equal(SettingsRestoreOutcome.NotABackup, SettingsArchive.Read(Zip()).Error?.Outcome);
    }

    // Only the three names, at the top: anything else, also beside a good file, a folder or a path
    // that would land elsewhere (zip slip), refuses the whole zip.
    [Theory]
    [InlineData("readme.txt")]
    [InlineData("backup/settings.json")]
    [InlineData("../settings.json")]
    [InlineData("..\\settings.json")]
    [InlineData("C:/Windows/settings.json")]
    [InlineData("/settings.json")]
    [InlineData("logs/")]
    [InlineData("settings.json.tmp")]
    public void Read_OtherEntry_IsNotABackup(string name)
    {
        var content = SettingsArchive.Read(Zip((SettingsBackup.ProfilesFileName, Profiles), (name, Settings)));

        Assert.Equal(SettingsRestoreOutcome.NotABackup, content.Error?.Outcome);
        Assert.Empty(content.Files);
    }

    [Fact]
    public void Read_FileTwice_IsNotABackup()
    {
        var content = SettingsArchive.Read(Zip((SettingsBackup.SettingsFileName, Settings), ("SETTINGS.json", Settings)));

        Assert.Equal(SettingsRestoreOutcome.NotABackup, content.Error?.Outcome);
    }

    [Fact]
    public void Read_ArchiveTooLarge_IsRefusedUnread()
    {
        var content = SettingsArchive.Read(new byte[SettingsBackup.MaxArchiveBytes + 1]);

        Assert.Equal(SettingsRestoreOutcome.TooLarge, content.Error?.Outcome);
    }

    // A zip bomb: a small zip whose file unpacks to more than the limit is refused, also when its
    // header lies about the size: the engine stops reading at the limit.
    [Fact]
    public void Read_FileThatUnpacksTooLarge_IsRefused()
    {
        var huge = new byte[SettingsBackup.MaxFileBytes + 1];
        huge.AsSpan().Fill((byte)' ');
        var archive = Zip((SettingsBackup.SettingsFileName, huge));
        Assert.True(archive.Length < 1024 * 1024);

        Assert.Equal(SettingsRestoreOutcome.TooLarge, SettingsArchive.Read(archive).Error?.Outcome);
        // .NET stops at the size of the header then, and what it read is no file of a backup.
        Assert.NotNull(SettingsArchive.Read(WithLyingSize(archive, size: 100)).Error);
    }

    // A file the stores cannot read refuses the zip, with the name of the file.
    [Theory]
    [InlineData(SettingsBackup.SettingsFileName, "not json")]
    [InlineData(SettingsBackup.SettingsFileName, "[1, 2]")]
    [InlineData(SettingsBackup.SettingsFileName, "null")]
    [InlineData(SettingsBackup.SettingsFileName, "")]
    [InlineData(SettingsBackup.SettingsFileName, """{ "brightness": 1, "brightness": 0.5 }""")]
    [InlineData(SettingsBackup.ProfilesFileName, """{ "profiles": """)]
    [InlineData(SettingsBackup.EffectsFileName, "42")]
    public void Read_FileThatDoesNotRead_IsUnreadable(string name, string json)
    {
        var good = name == SettingsBackup.ProfilesFileName ? (SettingsBackup.EffectsFileName, Effects) : (SettingsBackup.ProfilesFileName, Profiles);

        var content = SettingsArchive.Read(Zip(good, (name, Encoding.UTF8.GetBytes(json))));

        Assert.Equal(new SettingsRestoreResult(SettingsRestoreOutcome.Unreadable, name), content.Error);
        Assert.Empty(content.Files);
    }

    // As the stores read it: one value of the wrong type is only left out there, and a byte order
    // mark is no reason to refuse.
    [Fact]
    public void Read_FileWithAWrongValueOrAByteOrderMark_Reads()
    {
        byte[] withMark = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("""{ "brightness": "high" }""")];

        var content = SettingsArchive.Read(Zip((SettingsBackup.SettingsFileName, withMark)));

        Assert.Null(content.Error);
    }

    // What belongs to this pc replaces what the backup says; everything else stays, also a key
    // this engine does not know.
    [Fact]
    public void WithThisPc_TakesAutoStartAndRealDevicesOfThisPc()
    {
        var json = Encoding.UTF8.GetBytes("""{ "brightness": 0.5, "autoStartChosen": false, "HadRealDevices": true, "future": [1] }""");

        var root = JsonNode.Parse(SettingsArchive.WithThisPc(json, autoStartChosen: true, hadRealDevices: false))!.AsObject();

        Assert.Equal(0.5, root["brightness"]!.GetValue<double>());
        Assert.True(root["autoStartChosen"]!.GetValue<bool>());
        Assert.DoesNotContain(root, pair => pair.Key.Equals("hadRealDevices", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("[1]", root["future"]!.ToJsonString());
    }

    // A pc whose settings are from before autoStartChosen has none, and gets none.
    [Fact]
    public void WithThisPc_WithoutAChoice_LeavesItOut()
    {
        var json = Encoding.UTF8.GetBytes("""{ "autoStartChosen": true }""");

        var root = JsonNode.Parse(SettingsArchive.WithThisPc(json, autoStartChosen: null, hadRealDevices: true))!.AsObject();

        Assert.False(root.ContainsKey("autoStartChosen"));
        Assert.True(root["hadRealDevices"]!.GetValue<bool>());
    }

    internal static byte[] Zip(params (string Name, byte[] Content)[] files)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                using var stream = zip.CreateEntry(name, CompressionLevel.SmallestSize).Open();
                stream.Write(content);
            }
        }

        return buffer.ToArray();
    }

    // The zip with the uncompressed size of its only file in both headers set to size.
    private static byte[] WithLyingSize(byte[] archive, uint size)
    {
        var patched = archive.ToArray();
        for (var i = 0; i + 4 <= patched.Length; i++)
        {
            var signature = BitConverter.ToUInt32(patched, i);
            if (signature == 0x04034b50)
            {
                BitConverter.TryWriteBytes(patched.AsSpan(i + 22, 4), size);
            }
            else if (signature == 0x02014b50)
            {
                BitConverter.TryWriteBytes(patched.AsSpan(i + 24, 4), size);
            }
        }

        return patched;
    }
}
