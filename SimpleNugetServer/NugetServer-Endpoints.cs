using System.Net;
using HttpMultipartParser;
using JetBrains.Annotations;
using SimpleNugetServer.Attributes;
using SimpleNugetServer.NugetApi;
using SimpleNugetServer.NugetApi.SearchQueryService;
using SimpleNugetServer.Package;
using NugetPackage = SimpleNugetServer.Package.NugetPackage;
namespace SimpleNugetServer;
using Context = HttpListenerContext;

public partial class NugetServer
{
    [NugetResourceEndpoint(
        "PackagePublish/2.0.0",
        "",
        "PackagePublish")]
    [UsedImplicitly]
    private void PackagePublish(Context ctx, string apiPath,string[] urlParams)
    {
        if (ctx.Request.HttpMethod is "DELETE" && urlParams is [var id, var version])
        {
            var status = _packageManager.DeletePackage(id, version);
            SetResponse(ctx, status ? HttpStatusCode.OK : HttpStatusCode.NotFound, null);
            return;
        }

        if (ctx.Request.ContentType is null || !ctx.Request.ContentType.Contains("multipart/form-data") ||
            ctx.Request.HttpMethod != "PUT")
        {
            SetResponse(ctx, HttpStatusCode.BadRequest, null);
            return;
        }

        MemoryStream fullData = new();
        ctx.Request.InputStream.CopyTo(fullData);
        fullData.Position = 0;
        var parser = MultipartFormDataParser.Parse(fullData);

        if (parser.Files.Count is 0)
        {
            SetResponse(ctx, HttpStatusCode.BadRequest, null);
            return;
        }
        
        var file = parser.Files[0].Data!;
        var result =
            _packageManager.AddPackage(NugetPackage.FromStream(file, out var nuspecStream), file, nuspecStream);
        SetResponse(ctx, result is PackageAddResult.AlreadyExists ? HttpStatusCode.Conflict : HttpStatusCode.OK,
            null);
    }

    [NugetResourceEndpoint(
        "PackageBaseAddress/3.0.0",
        "",
        "PackageBaseAddress")]
    [UsedImplicitly]
    private void PackageBaseAddress(Context ctx, string apiPath,string[] urlParams)
    {
        switch (urlParams)
        {
            case [var packageName, "index.json"]:
            {
                var versions = _packageManager.GetPackageVersions(packageName);
                SetResponse(ctx, versions != null ? HttpStatusCode.OK : HttpStatusCode.NotFound,
                    versions != null
                        ? new
                        {
                            versions
                        }
                        : null);
                return;
            }
            case [var lowerId, var lowerVersion, var data] when data.EndsWith(".nupkg"):
            {
                var package = _packageManager.GetPackage(lowerId, lowerVersion);
                SetResponseBinary(ctx, package != null ? HttpStatusCode.OK : HttpStatusCode.NotFound, package);
                return;
            }
            case [var lowerId, var lowerVersion, var data] when data.EndsWith(".nuspec"):
            {
                var nuspec = _packageManager.GetNuspecBytes(lowerId, lowerVersion);
                SetResponseBinary(ctx, nuspec != null ? HttpStatusCode.OK : HttpStatusCode.NotFound, nuspec);
                return;
            }
            case [var lowerId, var lowerVersion, "icon"]:
            {
                var icon = _packageManager.GetIcon(lowerId, lowerVersion);
                SetResponseBinary(ctx, icon != null ? HttpStatusCode.OK : HttpStatusCode.NotFound, icon);
                return;
            }
            default:
                SetResponse(ctx, HttpStatusCode.BadRequest, null);
                break;
        }
    }

    [NugetResourceEndpoint(
        new[]
        {
            "SearchQueryService",
            "SearchQueryService/3.0.0-beta",
            "SearchQueryService/3.0.0-rc"
        },
        "",
        "SearchQueryService")]
    [UsedImplicitly]
    private void SearchQueryService(Context ctx, string apiPath,string[] urlParams) //TODO: Support unlisting packages
    {
        var queryElements = ctx.Request.QueryString;
        var specifications =
            _packageManager.FindPackages(
                queryElements["q"],
                int.Parse(queryElements["skip"] ?? "0"),
                int.Parse(queryElements["take"] ?? "1000"),
                bool.Parse(queryElements["prerelease"] ?? "false"),
                out var totalHits);
        List<NugetApi.SearchQueryService.NugetPackage> data = new();
        foreach (var spec in specifications)
        {
            var latestVer = spec.Value[^1];
            List<NugetPackageVersion> versions = new();
            foreach (var ver in spec.Value)
            {
                versions.Add(new NugetPackageVersion(GetRegistration(latestVer.Id, latestVer.Version,apiPath), ver.Version, 0));
            }

            var nugetPackage = new NugetApi.SearchQueryService.NugetPackage(
                GetRegistration(latestVer.Id, null,apiPath),
                GetRegistration(latestVer.Id, null,apiPath),
                latestVer.Name,
                latestVer.Version,
                latestVer.Description,
                "",
                GetIconUrl(latestVer.Id, latestVer.Version,apiPath),
                latestVer.LicenseUrl ?? "",
                latestVer.Tags,
                latestVer.Authors,
                Array.Empty<NugetPackageType>(),
                versions.ToArray()); //Package types is null for now
            data.Add(nugetPackage);

        }

        var result = new SearchResult(totalHits, data, new SearchContext(
            GetEndpoint(NugetEndpoint.RegistrationsBaseUrl,apiPath).ToString()));
        SetResponse(ctx, HttpStatusCode.OK, result);
    }

    private IEnumerable<DependencyGroup> GetDependencyGroups(NugetSpecification spec,string apiPath)
    {
        var nuspecDependencies = spec.Dependencies;
        foreach (var depGroup in nuspecDependencies)
        {
            List<Dependency> dependencies = new();

            foreach (var dependency in depGroup.Value)
            {
                var packageExists = _packageManager.DoesPackageExist(dependency.Id);
                dependencies.Add(new Dependency("",
                    dependency.Id,
                    $"[{dependency.Version}, )",
                    packageExists
                        ? GetRegistration(dependency.Id, null,apiPath)
                        : GetNugetOrgRegistration(dependency.Id)));
            }

            yield return new DependencyGroup("", depGroup.Key, dependencies.ToArray());
        }
    }

    private CatalogEntry GetCatalogEntry(string packageName, string version,string apiPath)
    {
        var nuspec = _packageManager.GetNuspec(packageName, version);
        var packagePublishTime = _packageManager.GetPackageUploadTime(packageName, version);

        var entry = new CatalogEntry(
            //"",
            nuspec.Authors,
            GetDependencyGroups(nuspec,apiPath).ToArray(),
            nuspec.Description,
            GetIconUrl(nuspec,apiPath),
            nuspec.Id,
            "",
            "",
            nuspec.LicenseUrl,
            true, //TODO: add unlisting
            "",
            GetContentUrl(packageName, version,apiPath),
            nuspec.ProjectUrl,
            packagePublishTime,
            false,
            "",
            nuspec.Tags,
            "",
            nuspec.Version
        );
        return entry;
    }

    private IEnumerable<RegistrationLeaf> GetLeafs(string packageName, string[] versions,string apiPath)
    {
        return versions.Select(ver => new RegistrationLeaf(
            GetRegistration(packageName, ver,apiPath),
            GetContentUrl(packageName, ver,apiPath),
            GetCatalogEntry(packageName, ver,apiPath)));
    }

    private RegistrationPageObject GetRegistrationPage(string packageName,string apiPath)
    {
        var versions = _packageManager.GetPackageVersions(packageName)!;
        var obj = new RegistrationPageObject(
            GetRegistration(packageName, null,apiPath),
            versions[0],
            versions[^1],
            GetLeafs(packageName, versions,apiPath).ToArray());
        return obj;
    }

    [NugetResourceEndpoint(
        new[]{"RegistrationsBaseUrl","RegistrationsBaseUrl/3.0.0-beta","RegistrationsBaseUrl/3.0.0-rc"},
        "",
        "RegistrationsBaseUrl")]
    [UsedImplicitly]
    private void RegistrationsBaseUrl(Context ctx, string apiPath,string[] urlParams)
    {
        if (urlParams is not [var packageName, "index.json"])
        {
            SetResponse(ctx, HttpStatusCode.NotFound, null);
            return;
        }
        
        if (!_packageManager.DoesPackageExist(packageName))
        {
            SetResponse(ctx, HttpStatusCode.NotFound, null);
            return;
        }

        var root = new RegistrationRoot(GetRegistration(packageName, null,apiPath),
            new[] { GetRegistrationPage(packageName,apiPath) });
        SetResponse(ctx, HttpStatusCode.OK, root);
    }

    
    /*[NugetResourceEndpoint(
        "Catalog/3.0.0",
        "",
        "Catalog")]
    [UsedImplicitly]
    private void Catalog(Context ctx, string[] urlParams)
    {
        
    }*/
}