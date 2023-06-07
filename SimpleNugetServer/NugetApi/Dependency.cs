using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace SimpleNugetServer.NugetApi;

[UsedImplicitly]
public record class Dependency(
    string Id,
    string Range,
    string Registration)
{
    [JsonPropertyName("@id")] public string ObjectID { get; init; }

    [JsonPropertyName("@type")] public string Type { get; } = "PackageDependency";
}