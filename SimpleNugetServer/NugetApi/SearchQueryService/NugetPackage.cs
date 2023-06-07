using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace SimpleNugetServer.NugetApi.SearchQueryService;

[UsedImplicitly]
public record NugetPackage(
    [property: JsonPropertyName("@id")] string ElementId,
    string registration,
    string id,
    string version,
    string description,
    string summary,
    string iconUrl,
    string licenseUrl,
    string[] tags,
    string authors,
    NugetPackageType[] packageTypes,
    NugetPackageVersion[] versions
    )
{
    [JsonPropertyName("@type")] public string ElementType => "Package";

    public string title => id;
};