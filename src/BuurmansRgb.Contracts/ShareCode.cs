using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;

namespace BuurmansRgb.Contracts;

// The short code of a shared scene or own effect, to paste in a chat or mail: Prefix and then the
// JSON of the file (SharedFiles), packed with Brotli and written as base64url, so it has no characters
// a chat changes and is a few hundred characters for a scene. A code from elsewhere is not trusted:
// it is refused when longer than MaxLength before anything is unpacked, and when it unpacks to more
// than SharedFiles.MaxJsonBytes (read in steps up to that limit, so a small code that unpacks to
// gigabytes stops there). What is in the JSON the engine checks, as for a file.
public static class ShareCode
{
    // The 1 is the version of the code itself, not of the JSON in it.
    public const string Prefix = "brgb1:";

    // Far more than the code of the largest scene that packs well; a code of a few MB is no code.
    public const int MaxLength = 1024 * 1024;

    private const int ReadChunkBytes = 16 * 1024;

    public static string Encode(ReadOnlySpan<byte> json)
    {
        using var packed = new MemoryStream();
        using (var brotli = new BrotliStream(packed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            brotli.Write(json);
        }

        return Prefix + Base64Url.EncodeToString(packed.GetBuffer().AsSpan(0, (int)packed.Length));
    }

    // Whether the text starts as a code, after spaces and line breaks.
    public static bool LooksLikeCode([NotNullWhen(true)] string? text) =>
        text is not null && text.AsSpan().TrimStart().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    // The JSON in the code; else false with why: NotShared for a text that is no code, or one that
    // was cut off or changed, and TooLarge. Spaces and line breaks in it, as a mail may add, do not
    // count.
    public static bool TryDecode(string? text, [NotNullWhen(true)] out byte[]? json, out ShareImportOutcome error)
    {
        json = null;
        error = ShareImportOutcome.NotShared;
        if (!LooksLikeCode(text))
        {
            return false;
        }

        if (text.Length > MaxLength)
        {
            error = ShareImportOutcome.TooLarge;
            return false;
        }

        var start = text.Length - text.AsSpan().TrimStart().Length + Prefix.Length;
        var body = string.Concat(text.Skip(start).Where(character => !char.IsWhiteSpace(character)));
        byte[] packed;
        try
        {
            packed = Base64Url.DecodeFromChars(body);
        }
        catch (FormatException)
        {
            return false;
        }

        try
        {
            using var brotli = new BrotliStream(new MemoryStream(packed, writable: false), CompressionMode.Decompress);
            using var unpacked = new MemoryStream();
            var chunk = new byte[ReadChunkBytes];
            int read;
            while ((read = brotli.Read(chunk)) > 0)
            {
                if (unpacked.Length + read > SharedFiles.MaxJsonBytes)
                {
                    error = ShareImportOutcome.TooLarge;
                    return false;
                }

                unpacked.Write(chunk, 0, read);
            }

            if (unpacked.Length == 0)
            {
                return false;
            }

            json = unpacked.ToArray();
            return true;
        }
        catch (InvalidDataException)
        {
            // Cut off, or a character changed: not data Brotli made.
            return false;
        }
    }
}
