using System.Text.Json;

namespace NovaisFPS.Core;

/// <summary>
/// Helper para output JSON dos módulos C# (para consumo futuro pela GUI).
/// </summary>
public static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ToJson<T>(T obj) => JsonSerializer.Serialize(obj, Options);

    public static void WriteJson<T>(T obj) => Console.WriteLine(ToJson(obj));
}
