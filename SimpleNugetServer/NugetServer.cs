using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Https.Http.Server;
using SimpleNugetServer.Attributes;
using SimpleNugetServer.Models;
using SimpleNugetServer.Package;


namespace SimpleNugetServer;

using Context = HttpRequestMessage;
using NugetResourceEndpointFunc = Action<HttpRequestMessage,HttpClientConnection,string, string[]>;
using EndpointMethod = ValueTuple<string,string[],Action<HttpRequestMessage,HttpClientConnection,string, string[]>>;

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
    private HttpServer _webserver;

    private bool Ssl => //_options.Certificate != null;//TODO: tls
        true;

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
        _webserver = new("cert", "key");
        _webserver.OnClientConnected = tuple =>
        {
            tuple.Connection.OnRequest = (list, method, arg3, arg4, arg5) =>
            {
                Handle(arg5,tuple.Connection);
                return ValueTask.CompletedTask;
            };
        };

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
            var lambda = Expression.Lambda<Action<Context,HttpClientConnection, string,string[]>>(call, parameters).Compile();
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

    /*private void RequestReceivedCallback(Context context)
    {
        Handle(context);
    }*/

    private void SetResponse(HttpRequestMessage ctx, HttpClientConnection connection, HttpStatusCode code, object? obj)
    {
        HttpResponseMessage msg = new();
        msg.StatusCode = code;
        //ctx.Response.StatusCode = (int)code;
        if (obj != null)
        {
            var serialized = JsonSerializer.Serialize(obj);
            StringContent content = new(serialized, Encoding.UTF8, "application/json");
            msg.Content = content;
            connection.SendResponse(msg, 0).AsTask().GetAwaiter().GetResult();
        }
        else
        {
            StringContent content = new("", Encoding.UTF8, "text/html");
            msg.Content = content;
            connection.SendResponse(msg, 0).AsTask().GetAwaiter().GetResult();
        }
        
    }

    private void SetResponseBinary(HttpRequestMessage ctx, HttpClientConnection connection, HttpStatusCode code,
        Memory<byte>? data)
    {
        HttpResponseMessage msg = new();
        msg.Content = new StringContent("");
        msg.StatusCode = code;
        
        

        if (data is null)
        {
            connection.SendResponse(msg, 0).AsTask().Wait();
            return;
        }

        msg.Content = new ReadOnlyMemoryContent(data.Value);
        msg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        connection.SendResponse(msg, 0).AsTask().Wait();
    }

    private void SendIndex(Context ctx,HttpClientConnection connection,string apiPath)
    {
        var index = CreateIndex(apiPath);
        SetResponse(ctx, connection,HttpStatusCode.OK, index);
    }

    private void Handle(Context ctx,HttpClientConnection connection)
    {
        var url = ctx.RequestUri!.Segments[1..].Select(u => u.TrimEnd('/')).ToArray();
        if (url.Length < 4)
        {
            SetResponse(ctx, connection,HttpStatusCode.NotFound, null);
            return;
        }

        var apiPath = url[0];
        var endpointName = url[3];
        var isAllowed = _options.CustomPathValidationCallback?.Invoke(apiPath) ?? apiPath == _options.HttpPath;
        
        if(isAllowed)
            CreateEndpoints(apiPath);
        if (url is [_, _, _, "index.json"] && isAllowed)
        {
            SendIndex(ctx,connection,apiPath);
            return;
        }

        if (!isAllowed || !_endpointsFuncs.TryGetValue(endpointName, out var endpointFunc))
        {
            SetResponse(ctx, connection,HttpStatusCode.NotFound, null);
            return;
        }

        Console.WriteLine($"handling endpoint: {endpointName}");
        try
        {
            endpointFunc(ctx, connection,apiPath,url[4..]);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

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
        _webserver.Start(_options.Host,_options.Port);
        /*_webserver.Prefixes.Add($"http{(Ssl ? "s" : "")}://{_options.Host}:{_options.Port}/"); //TODO:
        _webserver.Start();
        Task.Run(async () =>
        {
            while (true)
            {
                var ctx = await _webserver.GetContextAsync();
                RequestReceivedCallback(ctx);
            }
        });*/
    }
}