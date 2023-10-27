using System.Text.Json.Serialization;

namespace NugetServer.NugetApi.RegistrationBaseUrl;

public record RegistrationRoot(
    [property: JsonPropertyName("@id")] string ElementId,
    RegistrationPageObject[] items
)
{
    [JsonPropertyName("count")] public int Count => items.Length;
}