using System.Collections.Concurrent;

namespace SimpleNugetServer.Package;

public enum PackageAddResult
{
    Ok,
    Invalid,
    AlreadyExists
}
public class PackageManager
{
    private string _packagesPath;
    private ConcurrentDictionary<string, SemaphoreSlim> _packagesSlims = new();
    
    public PackageManager(string packagesPath)
    {
        _packagesPath = packagesPath;
    }

    private void WriteFile(string path, Stream stream)
    {
        var file = new FileStream(path, FileMode.Create);
        if(stream.CanSeek)
            stream.Position = 0;
        stream.CopyTo(file);
        file.Flush();
        file.Close();
    }

    private void WriteFile(string path, Memory<byte> data) => File.WriteAllBytes(path, data.ToArray());
    private void CreatePackageEntry(string path,string packageId,NugetPackage package,Stream packageStream,Stream nuspecStream)
    {
        WriteFile(Path.Join(path,$"{packageId}.nupkg"),packageStream);
        WriteFile(Path.Join(path,$"{packageId}.nuspec"),nuspecStream);
        
        if(package.Icon != null)
            WriteFile(Path.Join(path,"icon.png"),package.Icon.Value);
    }

    private bool DoesPackageExist(string id, string version,string packagePath)
    {
        //var slim = _packagesSlims.GetOrAdd(id, new SemaphoreSlim(1));
        //slim.Wait();
        var exists = Directory.Exists(Path.Join(packagePath, version));
        //slim.Release();
        return exists;
    }

    public bool DoesPackageExist(string id)
    {
        return Directory.Exists(Path.Join(_packagesPath, id));
    }
    public string[]? GetPackageVersions(string name)
    {
        var packagePath = Path.Join(_packagesPath, name.ToLower());
        return Directory.Exists(packagePath) 
            ? Directory.GetDirectories(packagePath).Select(d => new DirectoryInfo(d).Name).ToArray()
            : null;
    }

    public Memory<byte>? GetPackage(string name, string version)
    {
        var packagePath = Path.Join(_packagesPath, name.ToLower(),version);
        return Directory.Exists(packagePath) ? File.ReadAllBytes(Path.Join(packagePath, $"{name}.nupkg")) : null;
    }

    public Memory<byte>? GetNuspecBytes(string name, string version)
    {
        var path = Path.Join(_packagesPath, name, version);
        return Directory.Exists(path) ? File.ReadAllBytes(Path.Join(path, $"{name}.nuspec")) : null;
    }

    public NugetSpecification GetNuspec(string name, string version)
    {
        var path = Path.Join(_packagesPath, name, version,$"{name}.nuspec");
        return NugetSpecification.FromStream(File.OpenRead(path));
    }

    public Memory<byte>? GetIcon(string name, string version)
    {
        var path = Path.Join(_packagesPath, name, version, "icon.png");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
    public PackageAddResult AddPackage(NugetPackage package,Stream packageStream,Stream nuspecStream)
    {
        var nuspec = package.Nuspec;
        var packageName = nuspec.Id;
        var packagePath = Path.Join(_packagesPath, packageName);
        var packageExists = DoesPackageExist(packageName, nuspec.Version, packagePath);
        
        if (packageExists)
            return PackageAddResult.AlreadyExists;
        
        Directory.CreateDirectory(packagePath);
        var packageVersionPath = Path.Join(packagePath, nuspec.Version);
        Directory.CreateDirectory(packageVersionPath);
        
        CreatePackageEntry(packageVersionPath,packageName,package,packageStream,nuspecStream);

        return PackageAddResult.Ok;
        
    }

    public Dictionary<string,List<NugetSpecification>> FindPackages(string? searchPattern,int skip,int take,bool prerelease,out int totalHits)
    {
        totalHits = 0;
        Dictionary<string, List<NugetSpecification>> specifications = new();
        var directories = Directory.GetDirectories(_packagesPath)
            .Skip(skip)
            .Take(take);
        if(!string.IsNullOrEmpty(searchPattern))
            directories = directories.Where(d => d.Contains(searchPattern));
        foreach (var dir in directories)
        {
            totalHits++;
            var packageName = new DirectoryInfo(dir).Name;
            specifications.TryAdd(packageName, new List<NugetSpecification>());
            var specs = specifications[packageName];
            foreach(var verDir in Directory.GetDirectories(dir))
            {
                if (verDir.Contains('-') && !prerelease)
                    continue;
                using var fs = File.OpenRead(Path.Join(verDir, $"{packageName}.nuspec"));
                specs.Add(NugetSpecification.FromStream(fs));
            }
        }

        return specifications;
    }
}