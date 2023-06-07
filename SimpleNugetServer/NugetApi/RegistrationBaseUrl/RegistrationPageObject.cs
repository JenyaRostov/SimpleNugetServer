using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace SimpleNugetServer.NugetApi;

[UsedImplicitly]
public record RegistrationPageObject(
    [property: JsonPropertyName("@id")] string ElementId,
    string lower,
    string upper,
    RegistrationLeaf[] items)
{
    [JsonPropertyName("@type")]
    public string ElementType { get; } = "catalog:CatalogPage";

    public int count => items.Length;
}