﻿using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace SimpleNugetServer.NugetApi.SearchQueryService;

[UsedImplicitly]
public record NugetPackageVersion(
    [property: JsonPropertyName("@id")] string ElementId,
    string version,
    int downloads);