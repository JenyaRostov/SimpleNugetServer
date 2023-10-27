using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NugetServer.NugetApi.RegistrationBaseUrl;

[UsedImplicitly]
public record RegistrationPageObject(
    [property: JsonPropertyName("@id")] string ElementId,
    string lower,
    string upper,
    RegistrationLeaf[] items)
{
    public int count => items.Length;
}