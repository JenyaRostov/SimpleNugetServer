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
    //public X509Certificate2? Certificate { get; set; } = null;
}