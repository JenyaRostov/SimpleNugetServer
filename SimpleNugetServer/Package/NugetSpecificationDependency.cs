using JetBrains.Annotations;

namespace SimpleNugetServer.Package;

[UsedImplicitly]
public record class NugetSpecificationDependency(
    string Id,
    string Version,
    string? Exclude);