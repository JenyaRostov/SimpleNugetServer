using System.Text.Json.Serialization;

namespace NugetServer.NugetApi.SearchQueryService;


public record SearchContext(
    [property: JsonPropertyName("@base")] string registrationUrl)
{
    [JsonPropertyName("@vocab")] public string Vocab => "http://schema.nuget.org/schema#";
}
public record SearchResult(
    int totalHits,
    object data,
    [property: JsonPropertyName("@context")] SearchContext context)
{
    
}