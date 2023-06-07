using System.Runtime.InteropServices;
using System.Xml;

namespace SimpleNugetServer.Package;

public class NugetSpecification
{
    public string Name { get; set; }
    public string Id { get; set;}
    public string Version { get; set;}
    public string Authors { get; set;}
    public string? License { get; set;}
    public string LicenseUrl { get; set;}
    public string? Icon { get; set;}
    public string? IconUrl { get; set;}
    public string ProjectUrl { get; set;}
    public string Description { get; set;}
    public string ReleaseNotes { get; set;}
    public string Copyright { get; set;}
    public string[] Tags { get; set;}
    public Dictionary<string,NugetSpecificationDependency[]> Dependencies { get; set;}

    private static string? GetValue(XmlElement element, string name) => element[name]?.InnerXml;
    public static NugetSpecification FromStream(Stream stream)
    {
        XmlDocument document = new();
        document.Load(stream);
        var obj = document["package"]!["metadata"]!;

        NugetSpecification spec = new();
        spec.Name = GetValue(obj, "id")!;
        spec.Id = spec.Name!.ToLower();
        spec.Version = GetValue(obj, "version")!;
        spec.Authors = GetValue(obj, "authors")!;
        spec.License = GetValue(obj, "license");
        spec.LicenseUrl = GetValue(obj, "licenseUrl")!;
        spec.Icon = GetValue(obj, "icon");
        spec.IconUrl = GetValue(obj, "iconUrl");
        spec.ProjectUrl = GetValue(obj, "projectUrl")!;
        spec.Description = GetValue(obj, "description")!;
        spec.ReleaseNotes = GetValue(obj, "releaseNotes");
        spec.Copyright = GetValue(obj, "copyright")!;
        spec.Tags = GetValue(obj, "id")!.Split(" ");

        var dependencyGroups = obj["dependencies"]!;
        spec.Dependencies = new();
        foreach (XmlElement group in dependencyGroups.ChildNodes)
        {
            List<NugetSpecificationDependency> dependencies = new(group.ChildNodes.Count);
            foreach (XmlElement dependency in group.ChildNodes)
            {
                NugetSpecificationDependency dep = new(
                    dependency.GetAttribute("id"),
                    dependency.GetAttribute("version"),
                    dependency.HasAttribute("exclude") ? dependency.GetAttribute("exclude") : null);
                dependencies.Add(dep);
            }
            spec.Dependencies[group.GetAttribute("targetFramework")] = dependencies.ToArray();
        }

        return spec;
    }
}