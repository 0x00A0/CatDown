using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatDown.Core.Storage;

public static class CatDownJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // Keep CJK text readable in the files instead of \uXXXX escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // Same files on Windows and macOS.
        NewLine = "\n",
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("Empty JSON document.");
}
