using System.Net;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using NugetServer.Attributes;
using NugetServer.Models;
using NugetServer.NugetApi.RegistrationBaseUrl;
using NugetServer.NugetApi.SearchQueryService;
using NugetServer.Package;
using NugetPackage = NugetServer.Package.NugetPackage;

namespace NugetServer.Controllers;

[ApiController]
[Route("api/v3/")]
public class NugetController : ControllerBase
{
    private const string PackagesPath = "packages";
    private static NugetIndex Index;
    private static Dictionary<NugetEndpoint, string> NugetEndpoints = new();
    private static Uri GlobalUri;
    
    private PackageManager _manager = new(PackagesPath);
    internal static void InitNuget(IEnumerable<ControllerActionDescriptor> descriptors,ConfigurationManager configuration)
    {
        GlobalUri = new Uri(configuration["urls"]!);
        //var controller = typeof(NugetController);
        /*var methods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<NugetResourceEndpointAttribute>() != null);*/
        NugetIndex index = new();
        index.Version = "3.0.0";
        index.Context = new();
        List<NugetResource> resources = new();
        foreach (var controller in descriptors)
        {
            var nugetAttribute = controller.MethodInfo.GetCustomAttribute<NugetResourceEndpointAttribute>();
            if (nugetAttribute == null)
                continue;

            var endpoint = string.Join('/', controller.AttributeRouteInfo!.Template.Split("/")
                .TakeWhile(s => s.All(char.IsLetterOrDigit)));
            //var endpoint = controller.AttributeRouteInfo!.Template!.Split("/").Last(s => s.All(char.IsLetterOrDigit));
            foreach (var id in nugetAttribute.Types)
            { 
                resources.Add(new NugetResource(new Uri(GlobalUri,$"/{endpoint}").ToString(),id,nugetAttribute.Comment));
            }

            NugetEndpoints[nugetAttribute.EndpointType] = endpoint;
        }

        index.Resources = resources.ToArray();
        
        Index = index;
        /*foreach (var method in methods)
        {
            var resource = new NugetResource()
        }*/
    }

    private Uri GetEndpoint(NugetEndpoint endpoint)
    {
        return new Uri(GlobalUri, NugetEndpoints[endpoint]);
    }
    private string GetRegistration(string packageName, string? version)
    {
        var endpoint = GetEndpoint(NugetEndpoint.RegistrationsBaseUrl);
        
        return new Uri(endpoint, $"{packageName}/{(version ?? "index")}.json").ToString();
    }
    
    private string GetNugetOrgRegistration(string packageName)
    {
        return $"https://api.nuget.org/v3/registration5-semver1/{packageName.ToLower()}/index.json";
    }
    
    private string GetIconUrl(NugetSpecification nuspec)
    {
        if (nuspec.IconUrl != null)
            return nuspec.IconUrl;

        var endpoint = GetEndpoint(NugetEndpoint.PackageBaseAddress);
        
        var packageName = nuspec.Id.ToLower();
        var version = nuspec.Version.ToLower();
        
        return new Uri(endpoint, $"{packageName}/{version}/icon").ToString();
    }
    
    private string GetIconUrl(string packageName, string version) =>
        GetIconUrl(_manager.GetNuspec(packageName, version));
    
    private string GetContentUrl(string packageName, string version)
    {
        (packageName, version) = (packageName.ToLower(), version.ToLower());
        
        var endpoint = GetEndpoint(NugetEndpoint.PackageBaseAddress);
        return new Uri(endpoint, $"{packageName}/{version}/{packageName}.{version}.nupkg").ToString();
    }
    
    [HttpGet("index.json"),Authorize]
    public IActionResult GetIndex()
    {
        return Ok(Index);
    }
    
    [NugetResourceEndpoint(
        new[]
        {
            "SearchQueryService",
            "SearchQueryService/3.0.0-beta",
            "SearchQueryService/3.0.0-rc"
        },
        "",
        NugetEndpoint.SearchQueryService)]
    [HttpGet("SearchQueryService"),Authorize]
    public IActionResult SearchQuery([FromQuery()] string q = "",[FromQuery] int skip = 0,[FromQuery] int take = 1000,[FromQuery] bool prerelease = false)
    {
        var specifications =
            _manager.FindPackages(
                q,
                skip,
                take,
                prerelease,
                out var totalHits);
        List<NugetApi.SearchQueryService.NugetPackage> data = new();
        foreach (var spec in specifications)
        {
            var latestVer = spec.Value[^1];
            List<NugetPackageVersion> versions = new();
            foreach (var ver in spec.Value)
            {
                versions.Add(new NugetPackageVersion(GetRegistration(latestVer.Id, latestVer.Version), ver.Version, 0));
            }

            var nugetPackage = new NugetApi.SearchQueryService.NugetPackage(
                GetRegistration(latestVer.Id, null),
                GetRegistration(latestVer.Id, null),
                latestVer.Name,
                latestVer.Version,
                latestVer.Description,
                "",
                GetIconUrl(latestVer.Id, latestVer.Version),
                latestVer.LicenseUrl ?? "",
                latestVer.Tags,
                latestVer.Authors,
                Array.Empty<NugetPackageType>(),
                versions.ToArray()); //Package types is null for now
            data.Add(nugetPackage);

        }

        var result = new SearchResult(totalHits, data, new SearchContext(
            GetEndpoint(NugetEndpoint.RegistrationsBaseUrl).ToString()));

        return Ok(result);
    }

    [NugetResourceEndpoint(
        "PackageBaseAddress/3.0.0",
        "",
        NugetEndpoint.PackageBaseAddress)]
    [HttpGet("PackageBaseAddress/{packageName}/index.json"),Authorize]
    public IActionResult PackageBaseAddress(string packageName)
    {
        var versions = _manager.GetPackageVersions(packageName);
        
        return versions != null ? Ok(versions) : NotFound();
    }
    
    [NugetResourceEndpoint(
        "PackageBaseAddress/3.0.0",
        "",
        NugetEndpoint.PackageBaseAddress)]
    [HttpGet("PackageBaseAddress/{lowerId}/{lowerVersion}/{data}"),Authorize]
    public IActionResult PackageBaseAddress(string lowerId,string lowerVersion,string data)
    {
        if (data is "icon")
        {
            var icon = _manager.GetIcon(lowerId, lowerVersion);
            return icon != null
                ? File(icon.Value.ToArray(), "image/jpeg")
                : NotFound();
        }

        if (data.EndsWith(".nupkg"))
        {
            var package = _manager.GetPackage(lowerId, lowerVersion);
            return package != null
                ? File(package.Value.ToArray(),"application/octet-stream")
                : NotFound();
        } else if (data.EndsWith(".nuspec"))
        {
            var nuspec = _manager.GetNuspecBytes(lowerId, lowerVersion);
            return nuspec != null
                ? File(nuspec.Value.ToArray(),"application/octet-stream")
                : NotFound();
        }

        return BadRequest();
    }

    [NugetResourceEndpoint(
        "PackagePublish/2.0.0",
        "",
        NugetEndpoint.PackagePublish)]
    [HttpGet("PackagePublish/{id}/{version}"),Authorize]
    public IActionResult PackagePublishDelete(string id = "", string version = "")
    {
        if (HttpContext.Request.Method is not "DELETE")
        {
            return BadRequest();

        }

        var status = _manager.DeletePackage(id, version);
        return status ? Ok() : NotFound();

    }

    [NugetResourceEndpoint(
        "PackagePublish/2.0.0",
        "",
        NugetEndpoint.PackagePublish)]
    [HttpGet("PackagePublish"),Authorize]
    public IActionResult PackagePublishUpload([FromForm] IFormFile file)
    {
        if (!HttpContext.Request.Headers.ContentType.Contains("multipart/form-data") ||
            HttpContext.Request.Method != "PUT")
            return BadRequest();


        MemoryStream fullData = new();
        file.CopyTo(fullData);

        fullData.Position = 0;
        var result = _manager.AddPackage(NugetPackage.FromStream(fullData, out var nuspecStream), fullData,
            nuspecStream);

        return result is PackageAddResult.AlreadyExists
            ? Conflict()
            : Ok();

    }
    
    private IEnumerable<DependencyGroup> GetDependencyGroups(NugetSpecification spec)
    {
        var nuspecDependencies = spec.Dependencies;
        foreach (var depGroup in nuspecDependencies)
        {
            List<Dependency> dependencies = new();

            foreach (var dependency in depGroup.Value)
            {
                var packageExists = _manager.DoesPackageExist(dependency.Id);
                dependencies.Add(new Dependency("",
                    dependency.Id,
                    $"[{dependency.Version}, )",
                    packageExists
                        ? GetRegistration(dependency.Id, null)
                        : GetNugetOrgRegistration(dependency.Id)));
            }

            yield return new DependencyGroup("", depGroup.Key, dependencies.ToArray());
        }
    }

    private CatalogEntry GetCatalogEntry(string packageName, string version)
    {
        var nuspec = _manager.GetNuspec(packageName, version);
        var packagePublishTime = _manager.GetPackageUploadTime(packageName, version);

        var entry = new CatalogEntry(
            //"",
            nuspec.Authors,
            GetDependencyGroups(nuspec).ToArray(),
            nuspec.Description,
            GetIconUrl(nuspec),
            nuspec.Id,
            "",
            "",
            nuspec.LicenseUrl,
            true, //TODO: add unlisting
            "",
            GetContentUrl(packageName, version),
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

    private IEnumerable<RegistrationLeaf> GetLeafs(string packageName, string[] versions)
    {
        return versions.Select(ver => new RegistrationLeaf(
            GetRegistration(packageName, ver),
            GetContentUrl(packageName, ver),
            GetCatalogEntry(packageName, ver)));
    }

    private RegistrationPageObject GetRegistrationPage(string packageName)
    {
        var versions = _manager.GetPackageVersions(packageName)!;
        var obj = new RegistrationPageObject(
            GetRegistration(packageName, null),
            versions[0],
            versions[^1],
            GetLeafs(packageName, versions).ToArray());
        return obj;
    }

    [NugetResourceEndpoint(
        new[]{"RegistrationsBaseUrl","RegistrationsBaseUrl/3.0.0-beta","RegistrationsBaseUrl/3.0.0-rc"},
        "",
        NugetEndpoint.RegistrationsBaseUrl)]
    [HttpGet("RegistrationsBaseUrl/{packageName}/index.json"),Authorize]
    public IActionResult RegistrationsBaseUrl(string packageName)
    {
        
        if (!_manager.DoesPackageExist(packageName))
        {
            return NotFound();
        }

        var root = new RegistrationRoot(GetRegistration(packageName, null),
            new[] { GetRegistrationPage(packageName) });
        return Ok(root);
    }
}