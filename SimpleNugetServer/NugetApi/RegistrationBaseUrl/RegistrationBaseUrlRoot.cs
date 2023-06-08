﻿using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace SimpleNugetServer.NugetApi;

[UsedImplicitly]
public record RegistrationBaseUrlRoot(
    [property: JsonPropertyName("@id")] string ElementId,
    RegistrationPageObject[] items)
{
    [JsonPropertyName("@type")] public string[] ElementType { get; init; } = new[] { "PackageRegistration" };
    public int count => items.Length;
}