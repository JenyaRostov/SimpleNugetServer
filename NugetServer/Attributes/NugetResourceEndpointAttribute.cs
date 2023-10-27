using NugetServer.Models;

namespace NugetServer.Attributes;

public class NugetResourceEndpointAttribute : Attribute
{
    public NugetResourceEndpointAttribute(string[] types, string comment, NugetEndpoint endpointType)
    {
        Types = types;
        Comment = comment;
        EndpointType = endpointType;
    }

    public NugetResourceEndpointAttribute(string type, string comment, NugetEndpoint endpointType)
        : this(new[] { type }, comment, endpointType){}

    public string[] Types { get; set; }
    public string Comment { get; set; }
    public NugetEndpoint EndpointType { get; set; }
    
}