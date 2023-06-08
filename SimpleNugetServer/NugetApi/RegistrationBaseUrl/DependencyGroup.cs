using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace SimpleNugetServer.NugetApi;

[UsedImplicitly]
public record DependencyGroup(
    [property: JsonPropertyName("@id")] string ElementId,
    string targetFramework,
    Dependency[] dependencies)
{

    [JsonPropertyName("@type")] public string Type => "PackageDependencyGroup";
}