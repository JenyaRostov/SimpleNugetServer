using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using SimpleNugetServer.Attributes;
using SimpleNugetServer.Models;
using SimpleNugetServer.Package;


namespace SimpleNugetServer;

using Context = HttpListenerContext;
using NugetResourceEndpointFunc = Action<HttpListenerContext,string, string[]>;
using EndpointMethod = ValueTuple<string,string[],Action<HttpListenerContext,string, string[]>>;
public enum NugetEndpoint
{
    Catalog,
    PackageBaseAddress,
    PackageDetailsUriTemplate,
    PackagePublish,
    RegistrationsBaseUrl,
    ReportAbuseUriTemplate,
    RepositorySignatures,
    SearchAutocompleteService,
    SearchQueryService,
    SymbolPackagePublish
}

internal record CompiledNugetEndpointMethodInfo(string Id, string EndpointUrl,string Comment,NugetResourceEndpointFunc Func);

internal record NugetEndpointInfo(string Id, string FullUrl, string Comment);
public partial class NugetServer
{
    private const string Version = "3.0.0";
    private NugetServerOptions _options;
    private HttpListener _webserver;

    private bool Ssl => //_options.Certificate != null;//TODO: tls
        false;

    //ApiPath -> AllEndpoints
    private Dictionary<string, Dictionary<NugetEndpoint, NugetEndpointInfo>> _endpoints = new();
    
    //EndPointName -> Func
    private Dictionary<string, NugetResourceEndpointFunc> _endpointsFuncs = new();

    private CompiledNugetEndpointMethodInfo[] _endpointInfos;
    //private Dictionary<string, NugetResourceEndpointFunc> _endpoints = new();
    //private Dictionary<string,Dictionary<NugetEndpoint, NugetEndpointInfo>> _endpointsUrls = new();
    //private EndpointMethod[] _endpointMethods;
    private PackageManager _packageManager;
    
    internal static Dictionary<string, NugetEndpoint> NugetEndpoints = new()
    {
        { "PackageBaseAddress/3.0.0", NugetEndpoint.PackageBaseAddress },
        { "PackagePublish/2.0.0", NugetEndpoint.PackagePublish },
        { "RegistrationsBaseUrl", NugetEndpoint.RegistrationsBaseUrl },
        { "RegistrationsBaseUrl/3.0.0-beta", NugetEndpoint.RegistrationsBaseUrl },
        { "RegistrationsBaseUrl/3.0.0-rc", NugetEndpoint.RegistrationsBaseUrl },
        { "RegistrationsBaseUrl/3.6.0", NugetEndpoint.RegistrationsBaseUrl },
        { "SearchQueryService", NugetEndpoint.SearchQueryService },
        { "SearchQueryService/3.0.0-beta", NugetEndpoint.SearchQueryService },
        { "SearchQueryService/3.0.0-rc", NugetEndpoint.SearchQueryService },
        { "Catalog/3.0.0", NugetEndpoint.Catalog}
    };

    public NugetServer(NugetServerOptions options)
    {
        _options = options;
        _webserver = new HttpListener();

        //CreateEndpoints(out var methods);
        _endpointInfos = CompileEndpointMethods().ToArray();

        _packageManager = new PackageManager(options.PackagesPath);
    }
    
    private string GetPort()
    {
        return _options.Port is not 443 and not 80 ? $":{_options.Port}" : "";
    }

    private IEnumerable<CompiledNugetEndpointMethodInfo> CompileEndpointMethods()
    {
        var methods = GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<NugetResourceEndpointAttribute>() != null).ToArray();

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<NugetResourceEndpointAttribute>()!;
            var parameters = method.GetParameters()
                .Select(p => Expression.Parameter(p.ParameterType, p.Name))
                .ToArray();

            var call = Expression.Call(Expression.Constant(this), method, parameters);
            var lambda = Expression.Lambda<Action<Context, string,string[]>>(call, parameters).Compile();
            _endpointsFuncs[attr.EndpointName] = lambda;
            foreach(var id in attr.Ids)
                yield return new CompiledNugetEndpointMethodInfo(id, attr.EndpointName, attr.Comment, lambda);
        }
    }

    private string CreateEndpointUrl(string apiPath, string endpointName) =>
        $"http{(Ssl ? "s" : "")}://{_options.HostName}{GetPort()}/{apiPath}/api/v3/{endpointName}";
    private void CreateEndpoints(string apiPath)
    {
        

        if (_endpoints.TryGetValue(apiPath, out var endpoints))
            return;
        
        endpoints ??= new Dictionary<NugetEndpoint, NugetEndpointInfo>();

        foreach (var endpointInfo in _endpointInfos)
        {
            endpoints[NugetEndpoints[endpointInfo.Id]] =
                new NugetEndpointInfo(endpointInfo.Id,
                    $"http{(Ssl ? "s" : "")}://{_options.HostName}{GetPort()}/{apiPath}/api/v3/{endpointInfo.EndpointUrl}",
                    endpointInfo.Comment);
        }

        _endpoints[apiPath] = endpoints;

    }

    private NugetIndex CreateIndex(string apiPath)
    {
        List<NugetResource> resources = new();

        foreach (var endpoint in _endpointInfos)
        {
            NugetResource resource = new(CreateEndpointUrl(apiPath,endpoint.EndpointUrl), endpoint.Id, endpoint.Comment);
            resources.Add(resource);
        }


        return new NugetIndex(Version, resources.ToArray(), new NugetContext());
    }

    private void RequestReceivedCallback(Context context)
    {
        Handle(context);
    }

    private void SetResponse(Context ctx, HttpStatusCode code, object? obj)
    {
        ctx.Response.StatusCode = (int)code;
        if (obj != null)
        {
            var serialized = JsonSerializer.Serialize(obj);
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentType = "application/json";
            ctx.Response.OutputStream.Write(Encoding.UTF8.GetBytes(serialized));
        }
        else
        {
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentType = "text/html";
        }

        ctx.Response.OutputStream.Close();
    }

    private void SetResponseBinary(Context ctx, HttpStatusCode code, Memory<byte>? data)
    {
        ctx.Response.StatusCode = (int)code;
        ctx.Response.ContentType = "application/octet-stream";

        if (data is null)
        {
            ctx.Response.OutputStream.Close();
            return;
        }

        ctx.Response.OutputStream.Write(data.Value.Span);
        ctx.Response.OutputStream.Close();
    }

    private void SendIndex(Context ctx,string apiPath)
    {
        var index = CreateIndex(apiPath);
        SetResponse(ctx, HttpStatusCode.OK, index);
    }

    private void Handle(Context ctx)
    {
        var url = ctx.Request.Url!.Segments[1..].Select(u => u.TrimEnd('/')).ToArray();
        if (url.Length < 4)
        {
            SetResponse(ctx, HttpStatusCode.NotFound, null);
            return;
        }

        var apiPath = url[0];
        var endpointName = url[3];
        var isAllowed = _options.CustomPathValidationCallback?.Invoke(apiPath) ?? apiPath == _options.HttpPath;
        
        if(isAllowed)
            CreateEndpoints(apiPath);
        if (url is [_, _, _, "index.json"] && isAllowed)
        {
            SendIndex(ctx,apiPath);
            return;
        }

        if (!isAllowed || !_endpointsFuncs.TryGetValue(endpointName, out var endpointFunc))
        {
            SetResponse(ctx, HttpStatusCode.NotFound, null);
            return;
        }

        endpointFunc(ctx, apiPath,url[4..]);
    }

    protected Uri GetEndpoint(NugetEndpoint endpoint,string apiPath) => new($"{_endpoints[apiPath][endpoint].FullUrl}/");
    
    protected string GetRegistration(string packageName, string? version,string apiPath)
    {
        var endpoint = GetEndpoint(NugetEndpoint.RegistrationsBaseUrl,apiPath);

        return new Uri(endpoint, $"{packageName}/{(version ?? "index")}.json").ToString();
    }

    protected string GetNugetOrgRegistration(string packageName)
    {
        return $"https://api.nuget.org/v3/registration5-semver1/{packageName.ToLower()}/index.json";
    }

    protected string GetIconUrl(NugetSpecification nuspec,string apiPath)
    {
        if (nuspec.IconUrl != null)
            return nuspec.IconUrl;

        var endpoint = GetEndpoint(NugetEndpoint.PackageBaseAddress,apiPath);
        
        var packageName = nuspec.Id.ToLower();
        var version = nuspec.Version.ToLower();
        
        return new Uri(endpoint, $"{packageName}/{version}/icon").ToString();
    }

    protected string GetIconUrl(string packageName, string version,string apiPath) =>
        GetIconUrl(_packageManager.GetNuspec(packageName, version),apiPath);
    
    protected string GetContentUrl(string packageName, string version,string apiPath)
    {
        (packageName, version) = (packageName.ToLower(), version.ToLower());
        
        var endpoint = GetEndpoint(NugetEndpoint.PackageBaseAddress,apiPath);
        return new Uri(endpoint, $"{packageName}/{version}/{packageName}.{version}.nupkg").ToString();
    }

    protected string GetCatalogUrl(string packageName, string version,string apiPath)
    {
        (packageName, version) = (packageName.ToLower(), version.ToLower());
        
        var endpoint = GetEndpoint(NugetEndpoint.Catalog,apiPath);
        var updateTime = _packageManager.GetPackageUploadTime(packageName, version)
            .ToString("yyyy.MM.dd.hh.mm.ss");
        return new Uri(endpoint, $"{updateTime}/{packageName.ToLower()}.{version}.json").ToString();
    }
    public void Start()
    {
        _webserver.Prefixes.Add($"http{(Ssl ? "s" : "")}://{_options.Host}:{_options.Port}/"); //TODO:
        _webserver.Start();
        Task.Run(async () =>
        {
            while (true)
            {
                var ctx = await _webserver.GetContextAsync();
                RequestReceivedCallback(ctx);
            }
        });
    }
}