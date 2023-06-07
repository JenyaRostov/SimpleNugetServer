
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
using NugetResourceEndpointFunc = Action<HttpListenerContext,string[]>;

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
public partial class NugetServer
{

    private const string Version = "3.0.0";
    private NugetServerOptions _options;
    private HttpListener _webserver;
    //private Server _webserver;
    private NugetIndex _index;

    private bool Ssl => //_options.Certificate != null;
        false;
    private Dictionary<string, NugetResourceEndpointFunc> _endpoints = new();
    private Dictionary<NugetEndpoint, string> _endpointsUrls = new();
    private PackageManager _packageManager;

    internal static Dictionary<string, NugetEndpoint> NugetEndpoints = new()
    {
        { "PackageBaseAddress/3.0.0", NugetEndpoint.PackageBaseAddress },
        { "PackagePublish/2.0.0", NugetEndpoint.PackagePublish },
        { "RegistrationsBaseUrl", NugetEndpoint.RegistrationsBaseUrl },
        { "RegistrationsBaseUrl/3.0.0-beta", NugetEndpoint.RegistrationsBaseUrl },
        { "RegistrationsBaseUrl/3.0.0-rc", NugetEndpoint.RegistrationsBaseUrl },
        { "SearchQueryService", NugetEndpoint.SearchQueryService },
        { "SearchQueryService/3.0.0-beta", NugetEndpoint.SearchQueryService },
        { "SearchQueryService/3.0.0-rc", NugetEndpoint.SearchQueryService }
    };
    public NugetServer(NugetServerOptions options)
    {
        _options = options;
        _webserver = new HttpListener();
        //_webserver = new Server(options.HostName, options.Port, false, RequestReceivedCallback);
        //_webserver = new Webserver(options.HostName, options.Port, RequestReceivedCallback,options.Certificate);
        /*_webserver.Events.Exception += (sender, args) =>
        {
            Debugger.Break();
        };*/
        CreateEndpoints(out var methods);
        _index = CreateIndex(methods);
        _packageManager = new PackageManager(options.PackagesPath);
    }

    private string GetPort()
    {
        if (_options.Port is not 443 and not 80)
            return $":{_options.Port}";
        return "";
    }

    private void CreateEndpoints(out MethodInfo[] methods)
    {
        methods = this.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<NugetResourceEndpointAttribute>() != null).ToArray();
        
        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<NugetResourceEndpointAttribute>()!;
            var parameters = method.GetParameters()
                .Select(p => Expression.Parameter(p.ParameterType, p.Name))
                .ToArray();

            var call = Expression.Call(Expression.Constant(this), method, parameters);
            var lambda = Expression.Lambda<Action<Context,string[]>>(call, parameters).Compile();
            _endpoints[attr.EndpointName] = lambda;
            foreach (var id in attr.Ids)
            {
                _endpointsUrls[NugetEndpoints[id]] =
                    $"http://{_options.Host}:{_options.Port}/{_options.HttpPath}/api/v3/{attr.EndpointName}";
            }

        }
    }
    private NugetIndex CreateIndex(MethodInfo[] methods)
    {
        List<NugetResource> resources = new();
        foreach (var method in methods)
        {
            var nugetResourceAttr = method.GetCustomAttribute<NugetResourceEndpointAttribute>()!;
            foreach (var id in nugetResourceAttr.Ids)
            {
                NugetResource resource = new($"http{(Ssl ? "s" : "")}://{_options.HostName}{GetPort()}/{_options.HttpPath}/api/v3/{nugetResourceAttr.EndpointName}/",
                    id, nugetResourceAttr.Comment);
                resources.Add(resource);
            }
           
            
        }

        return new NugetIndex(Version, resources.ToArray(), new NugetContext());
    }
    
    private void RequestReceivedCallback(Context context)
    {
        Handle(context);
    }
    private void SetResponse(Context ctx, HttpStatusCode code,object? obj)
    {
        ctx.Response.StatusCode = (int)code;
        if (obj != null)
        {
            var serialized = JsonSerializer.Serialize(obj);
            ctx.Response.OutputStream.Write(System.Text.Encoding.UTF8.GetBytes(serialized));
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentType = "application/json";
            ctx.Response.Close();
        }
        else
        {
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentType = "text/html";
            ctx.Response.Close();
        }

    }

    private void SetResponseBinary(Context ctx, HttpStatusCode code, Memory<byte>? data)
    {
        ctx.Response.StatusCode = (int)code;
        ctx.Response.ContentType = "application/octet-stream";
        
        if (data is null)
        {
            ctx.Response.Close();
            return;
        }
        ctx.Response.OutputStream.Write(data.Value.Span);
        ctx.Response.Close();
    }

    private void SendIndex(Context ctx)
    {
        SetResponse(ctx, HttpStatusCode.OK, _index);
    }

    private void Handle(Context ctx)
    {
        var url = ctx.Request.Url!.Segments[1..].Select(u=> u.TrimEnd('/')).ToArray();
        if (url.Length < 4)
        {
            SetResponse(ctx, HttpStatusCode.NotFound, null);
            return;
        }

        var apiPath = url[0];
        var endpointName = url[3];
        if (url is [_,_,_,"index.json"] && apiPath == _options.HttpPath)
        {
            SendIndex(ctx);
            return;
        }
        if (apiPath != _options.HttpPath || !_endpoints.TryGetValue(endpointName, out var endpointFunc))
        {
            SetResponse(ctx, HttpStatusCode.NotFound, null);
            return;
        }
        
        endpointFunc(ctx,url[4..]);
    }

    protected Uri GetEndpoint(NugetEndpoint endpoint) => new Uri($"{_endpointsUrls[endpoint]}/");
    protected string GetRegistration(string packageName, string? version)
    {
        var endpoint = GetEndpoint(NugetEndpoint.RegistrationsBaseUrl);

        return new Uri(endpoint, $"{packageName}/{(version ?? "index")}.json").ToString();
    }

    protected string GetIconUrl(string packageName,string version)
    {
        var nuspec = _packageManager.GetNuspec(packageName, version);
        if (nuspec.IconUrl != null)
            return nuspec.IconUrl;
        
        var endpoint = GetEndpoint(NugetEndpoint.PackageBaseAddress);

        return new Uri(endpoint, $"{packageName}/{version}/icon").ToString();
    }

    protected string GetContentUrl(string packageName, string version)
    {
        var endpoint = GetEndpoint(NugetEndpoint.PackageBaseAddress);
        return new Uri(endpoint, $"{packageName}/{version}/{packageName}.{version}.nupkg").ToString();
    }
    public void Start()
    {
        _webserver.Prefixes.Add($"http{(Ssl ? "s" : "")}://{_options.Host}:{_options.Port}/");//TODO:
        _webserver.Start();
        Task.Run(async () =>
        {
            while(true)
            {
                var ctx = await _webserver.GetContextAsync();
                RequestReceivedCallback(ctx);
            }
        });
    }
}