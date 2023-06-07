using System.Text.Json.Serialization;

namespace SimpleNugetServer.Models;

public record struct NugetIndex(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("resources")] NugetResource[] Resources,
    [property: JsonPropertyName("@context")] NugetContext Context
    );