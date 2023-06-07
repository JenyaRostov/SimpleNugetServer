using System.Text.Json.Serialization;

namespace SimpleNugetServer.Models;

public record struct NugetResource(
    [property: JsonPropertyName("@id")] string Id,
    [property: JsonPropertyName("@type")] string Type,
    [property: JsonPropertyName("comment")] string Comment
    );