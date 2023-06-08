using JetBrains.Annotations;

namespace SimpleNugetServer.Package;

[UsedImplicitly]
public record NugetSpecificationDependency(
    string Id,
    string Version,
    string? Exclude);