using JetBrains.Annotations;

namespace NugetServer.Package;

[UsedImplicitly]
public record NugetSpecificationDependency(
    string Id,
    string Version,
    string? Exclude);