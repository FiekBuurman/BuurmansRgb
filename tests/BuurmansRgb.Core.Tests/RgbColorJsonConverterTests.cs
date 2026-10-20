using System.Text.Json;

namespace BuurmansRgb.Core.Tests;

public sealed class RgbColorJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new RgbColorJsonConverter() } };

    [Fact]
    public void Write_GivesTheHexCode()
    {
        Assert.Equal("\"#FF8800\"", JsonSerializer.Serialize(Orange, Options));
    }

    [Fact]
    public void Read_HexCode_GivesTheColor()
    {
        Assert.Equal(Orange, JsonSerializer.Deserialize<RgbColor>("\"#FF8800\"", Options));
    }

    [Theory]
    [InlineData("\"orange\"")]
    [InlineData("12")]
    public void Read_NoHexCode_Throws(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RgbColor>(json, Options));
    }
}