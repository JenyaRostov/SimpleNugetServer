namespace SimpleNugetServer.Attributes;

public class NugetResourceEndpointAttribute : Attribute
{
    public NugetResourceEndpointAttribute(string[] ids, string comment, string endpointName)
    {
        Ids = ids;
        Comment = comment;
        EndpointName = endpointName;
    }

    public NugetResourceEndpointAttribute(string id, string comment, string endpointName)
        : this(new[] { id }, comment, endpointName){}

    public string[] Ids { get; set; }
    public string Comment { get; set; }
    public string EndpointName { get; set; }
    
}