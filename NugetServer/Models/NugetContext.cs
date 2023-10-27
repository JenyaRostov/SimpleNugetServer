using System.Text.Json.Serialization;

namespace NugetServer.Models;

public class NugetContext
{
    [JsonPropertyName("@vocab")] public string Vocab { get; set; } = "http://schema.nuget.org/services#";
    [JsonPropertyName("comment")] public string Comment { get; set; } = "http://www.w3.org/2000/01/rdf-schema#comment";
}