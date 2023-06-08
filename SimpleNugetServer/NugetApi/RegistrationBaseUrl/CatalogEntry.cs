namespace SimpleNugetServer.NugetApi;

public record CatalogEntry(
    //[property: JsonPropertyName("@id")] string ElementId,
    string authors,
    DependencyGroup[] dependencyGroups,
    string description,
    string iconUrl,
    string id,
    string language,
    string licenseExpression,
    string licenseUrl,
    bool listed,
    string minClientVersion,
    string packageContent,
    string projectUrl,
    DateTime published,
    bool requireLicenseAcceptance,
    string summary,
    string[] tags,
    string title,
    string version
)
{
    //[JsonPropertyName("@type")] public string Type { get; } = "PackageDetails";
}