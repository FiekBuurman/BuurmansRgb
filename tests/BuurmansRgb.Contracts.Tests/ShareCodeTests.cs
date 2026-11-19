using System.Buffers.Text;
using System.IO.Compression;
using System.Text;

namespace BuurmansRgb.Contracts.Tests;

public sealed class ShareCodeTests
{
    private static readonly byte[] Json = Encoding.UTF8.GetBytes("""{ "version": 1, "kind": "effect", "effects": [] }""");

    [Fact]
    public void Encode_ThenDecode_GivesTheJsonBack()
    {
        var code = ShareCode.Encode(Json);

        Assert.StartsWith(ShareCode.Prefix, code, StringComparison.Ordinal);
        Assert.DoesNotContain(code[ShareCode.Prefix.Length..], character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
        Assert.True(ShareCode.TryDecode(code, out var json, out _));
        Assert.Equal(Json, json);
    }

    // A mail or chat may wrap the code over lines or put spaces around it; the prefix in capitals
    // still counts.
    [Fact]
    public void Decode_WithSpacesAndLineBreaks_StillReads()
    {
        var code = ShareCode.Encode(Json);
        var wrapped = "  \r\n" + code[..10].ToUpperInvariant()[..ShareCode.Prefix.Length] + code[ShareCode.Prefix.Length..10] + "\r\n" + code[10..20] + " " + code[20..] + "\n";

        Assert.True(ShareCode.TryDecode(wrapped, out var json, out _));
        Assert.Equal(Json, json);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hallo")]
    [InlineData("brgb2:abc")]
    [InlineData("brgb1:")]
    [InlineData("brgb1:!!!")]
    [InlineData("brgb1:AAAA")]
    public void Decode_NoCode_IsNotShared(string? text)
    {
        Assert.False(ShareCode.TryDecode(text, out var json, out var error));
        Assert.Null(json);
        Assert.Equal(ShareImportOutcome.NotShared, error);
    }

    // A code that was cut off or got another character does not unpack to the JSON it had.
    [Fact]
    public void Decode_Damaged_IsNotTheJson()
    {
        var code = ShareCode.Encode(Encoding.UTF8.GetBytes(new string('a', 2000) + "einde"));

        var cut = ShareCode.TryDecode(code[..^4], out var json, out var error);

        Assert.True(!cut || !json!.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(new string('a', 2000) + "einde")));
        Assert.True(cut || error == ShareImportOutcome.NotShared);
    }

    [Fact]
    public void Decode_LongerThanTheMost_IsTooLargeBeforeItUnpacks()
    {
        var code = ShareCode.Prefix + new string('A', ShareCode.MaxLength);

        Assert.False(ShareCode.TryDecode(code, out _, out var error));
        Assert.Equal(ShareImportOutcome.TooLarge, error);
    }

    // A small code that unpacks to far more than a scene can be (a zip bomb) stops at the limit.
    [Fact]
    public void Decode_ThatUnpacksToTooMuch_IsTooLarge()
    {
        using var packed = new MemoryStream();
        using (var brotli = new BrotliStream(packed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var zeros = new byte[1024 * 1024];
            for (var i = 0; i < 64; i++)
            {
                brotli.Write(zeros);
            }
        }

        var code = ShareCode.Prefix + Base64Url.EncodeToString(packed.ToArray());

        Assert.True(code.Length < 10_000);
        Assert.False(ShareCode.TryDecode(code, out _, out var error));
        Assert.Equal(ShareImportOutcome.TooLarge, error);
    }

    [Fact]
    public void Decode_JustUnderTheLimit_Reads()
    {
        var json = new byte[SharedFiles.MaxJsonBytes];
        json.AsSpan().Fill((byte)' ');

        Assert.True(ShareCode.TryDecode(ShareCode.Encode(json), out var decoded, out _));
        Assert.Equal(SharedFiles.MaxJsonBytes, decoded.Length);
    }

    [Fact]
    public void LooksLikeCode_OnlyWithThePrefix()
    {
        Assert.True(ShareCode.LooksLikeCode(" brgb1:abc"));
        Assert.False(ShareCode.LooksLikeCode("{ \"version\": 1 }"));
        Assert.False(ShareCode.LooksLikeCode(null));
    }
}
