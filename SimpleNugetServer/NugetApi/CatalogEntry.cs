using System.Text.Json.Serialization;

namespace SimpleNugetServer.NugetApi;

public record class CatalogEntry(
    string Authors,
    string Description,
    string IconUrl,
    string Id,
    string Language,
    string LicenseExpression,
    string LicenseUrl,
    bool Listed,
    object MinClientVersion,
    string PackageContent,
    string ProjectUrl,
    DateTime Published,
    bool RequireLicenseAcceptance,
    string Summary,
    string[] Tags,
    string Title,
    string Version
)
{
    [JsonPropertyName("@id")] public string PackageId { get; init; }
    [JsonPropertyName("@type")] public string Type { get; } = "PackageDetails";
}