using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Transactions;
using System.Web;
using HttpMultipartParser;
using JetBrains.Annotations;
using SimpleNugetServer.Attributes;
using SimpleNugetServer.NugetApi;
using SimpleNugetServer.NugetApi.SearchQueryService;
using SimpleNugetServer.Package;
using HttpMethod = System.Net.Http.HttpMethod;
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
    private async Task PackagePublish(Context ctx,string[] urlParams)
    {
        
        if (ctx.Request.ContentType is null || !ctx.Request.ContentType.Contains("multipart/form-data") || ctx.Request.HttpMethod != "PUT")
        {
            SetResponse(ctx, HttpStatusCode.BadRequest, null);
            return;
        }
        
       
        MultipartFormDataParser parser = null;
        MemoryStream fullData = new();
        await ctx.Request.InputStream.CopyToAsync(fullData);
        fullData.Position = 0;
        parser = await MultipartFormDataParser.ParseAsync(fullData);
        /*if (ctx.Request.DataAsBytes != null)
            parser = await MultipartFormDataParser.ParseAsync(new MemoryStream(ctx.Request.DataAsBytes));
        else
        {
            if (!ctx.Request.ChunkedTransfer)
            {
                await SetResponse(ctx, HttpStatusCode.BadRequest, null);
                return;
            }

            MemoryStream data = new();
            Chunk chunk;
            do
            {
                chunk = await ctx.Request.ReadChunk();
                data.Write(chunk.Data);
            } while (chunk.IsFinalChunk);

            data.Position = 0;
            parser = await MultipartFormDataParser.ParseAsync(data);

        }*/
        /*if (ctx.Request.DataAsBytes is null)
        {
            await SetResponse(ctx, HttpStatusCode.Continue, null);
            return;
        }*/

        if (parser.Files.Count == 0)
        {
            SetResponse(ctx, HttpStatusCode.BadRequest, null);
            return;
        }
        

        var file = parser.Files[0].Data!;
        var result = _packageManager.AddPackage(NugetPackage.FromStream(file,out var nuspecStream), file,nuspecStream);
        SetResponse(ctx, result is PackageAddResult.AlreadyExists ? HttpStatusCode.Conflict : HttpStatusCode.OK,
            null);
    }

    [NugetResourceEndpoint(
        "PackageBaseAddress/3.0.0",
        "",
        "PackageBaseAddress")]
    [UsedImplicitly]
    private void PackageBaseAddress(Context ctx,string[] urlParams)
    {
        if (urlParams is [var packageName, "index.json"])
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

        if (urlParams is [var lowerId, var lowerVersion, var data])
        {
            if(data.EndsWith(".nupkg"))
            {
                var package = _packageManager.GetPackage(lowerId, lowerVersion);
                SetResponseBinary(ctx, package != null ? HttpStatusCode.OK : HttpStatusCode.NotFound, package);
                return;
            } 
            else if (data.EndsWith(".nuspec"))
            {
                var nuspec = _packageManager.GetNuspecBytes(lowerId,lowerVersion);
                SetResponseBinary(ctx, nuspec != null ? HttpStatusCode.OK : HttpStatusCode.NotFound, nuspec);
                return;
            }
            else if (data == "icon")
            {
                var icon = _packageManager.GetIcon(lowerId, lowerVersion);
                SetResponseBinary(ctx, icon != null ? HttpStatusCode.OK : HttpStatusCode.NotFound, icon);
                return;
            }
        }

        SetResponse(ctx, HttpStatusCode.BadRequest, null);

    }

    [NugetResourceEndpoint(
        new []
        {
            "SearchQueryService",
            "SearchQueryService/3.0.0-beta",
            "SearchQueryService/3.0.0-rc"
        },
        "",
        "SearchQueryService")]
    [UsedImplicitly]
    private void SearchQueryService(Context ctx, string[] urlParams)
    {
        
        var queryElements = ctx.Request.QueryString ?? new NameValueCollection();
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
                versions.Add(new NugetPackageVersion(GetRegistration(latestVer.Id,latestVer.Version),ver.Version,0));
            }

            var nugetPackage = new NugetApi.SearchQueryService.NugetPackage(
                GetRegistration(latestVer.Id,null),
                GetRegistration(latestVer.Id, null),
                latestVer.Name,
                latestVer.Version,
                latestVer.Description,
                "",
                GetIconUrl(latestVer.Id, latestVer.Version),
                latestVer.LicenseUrl,
                latestVer.Tags,
                latestVer.Authors,
                Array.Empty<NugetPackageType>(),
                versions.ToArray());//Package types is null for now
            data.Add(nugetPackage);

        }

        var response = new
        {
            totalHits,
            data
        };
        SetResponse(ctx, HttpStatusCode.OK, response);
    }

    private IEnumerable<RegistrationLeaf> GetLeafs(string id,string[] versions)
    {
        return versions.Select(ver => new RegistrationLeaf(
            GetRegistration(id, ver),
            GetContentUrl(id, ver)));
    }

    private RegistrationPageObject GetRegistrationPage(string id)
    {
        var versions = _packageManager.GetPackageVersions(id)!;
        var obj = new RegistrationPageObject(
            GetRegistration(id, null),
            versions[0],
            versions[^1],
            GetLeafs(id,versions).ToArray());
        return obj;
    }
    [NugetResourceEndpoint(
        "RegistrationsBaseUrl",
        "",
        "RegistrationsBaseUrl")]
    [UsedImplicitly]
    private void RegistrationsBaseUrl(Context ctx, string[] urlParams)
    {
        if (urlParams is not [var id, "index.json"])
        {
            SetResponse(ctx, HttpStatusCode.NotFound, null);
            return;
        }
        if (!_packageManager.DoesPackageExist(id))
        {
            SetResponse(ctx, HttpStatusCode.NotFound, null);
            return;
        }

        SetResponse(ctx, HttpStatusCode.OK, GetRegistrationPage(id));
        
    }
}