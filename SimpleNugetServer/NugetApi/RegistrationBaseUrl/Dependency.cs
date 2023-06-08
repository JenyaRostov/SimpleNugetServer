using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace SimpleNugetServer.NugetApi;

[UsedImplicitly]
public record Dependency(
    [property: JsonPropertyName("@id")] string ElementId,
    string id,
    string range,
    string registration)
{

    [JsonPropertyName("@type")] public string Type => "PackageDependency";
}