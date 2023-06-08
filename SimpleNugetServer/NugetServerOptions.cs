using System.Text.Json.Serialization;

namespace SimpleNugetServer;

public class NugetServerOptions
{
    public NugetServerOptions(string packagesPath, string httpPath, string host, string hostName, int port/*, X509Certificate2? certificate = null*/)
    {
        PackagesPath = packagesPath;
        HttpPath = httpPath;
        Host = host;
        HostName = hostName;
        Port = port;
        //Certificate = certificate;
    }

    public string PackagesPath { get; set;}
    public string HttpPath { get; set;}
    public string Host { get; set;}
    public string HostName { get; set;}
    public int Port { get; set;}

    /// <summary>
    /// A callback that can be used to validate incoming requests' paths<br/>
    /// It allows to implement custom logic to validate requests<br/>
    /// For example it can be assigned as lambda like (string path) => path is "SecretCode" or "Code2"<br/>
    /// and all paths that are not SecretCode or Code2 will not go through<br/>
    /// SecretCode/api/v3/SomeEndpoint will work while
    /// AAA/api/v3/SomeEndpoint will not
    /// Setting this to anything other than null will override <see cref="HttpPath"/>
    /// </summary>
    [JsonIgnore]
    public Func<string, bool>? CustomPathValidationCallback { get; set; }= null;
    //public X509Certificate2? Certificate { get; set; } = null;
}