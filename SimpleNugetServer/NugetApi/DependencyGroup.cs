using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace SimpleNugetServer.NugetApi;

[UsedImplicitly]
public record class DependencyGroup(
    Dependency[] Dependencies)
{
    [JsonPropertyName("@id")] public string ObjectID { get; init; }

    [JsonPropertyName("@type")] public string Type { get; } = "PackageDependencyGroup";
}