using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyFXBoard.Core.Storage;

public static class JsonOptions
{
    public static readonly JsonSerializerOptions File = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
