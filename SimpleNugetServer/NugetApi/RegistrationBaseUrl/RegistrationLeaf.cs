using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace SimpleNugetServer.NugetApi;

[UsedImplicitly]
public record RegistrationLeaf(
    [property: JsonPropertyName("@id")] string ElementId, 
    string packageContent)
{
    [JsonPropertyName("@type")] public string ElementType => "Package";
}