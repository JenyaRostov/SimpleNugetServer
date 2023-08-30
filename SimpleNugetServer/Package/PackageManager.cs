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
    
    public PackageManager(string packagesPath)
    {
        _packagesPath = packagesPath;
    }

    private void EnsurePackagesDirectoryExists()
    {
        if (!Directory.Exists(_packagesPath))
            Directory.CreateDirectory(_packagesPath);
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

    private string GetPackagePath(string name, string? version) => Path.Join(_packagesPath, name.ToLower(), version?.ToLower() ?? "");

    private bool DoesPackageExist(string id, string version) => Directory.Exists(GetPackagePath(id,version));

    private void UpdateCommit(string packageName,string version)
    {
        var commitPath = Path.Join(GetPackagePath(packageName, version), "commit.txt");
        File.WriteAllText(commitPath,Guid.NewGuid().ToString());
    }

    private (DateTime lastWriteTime, string value) GetCommit(string packageName,string version)
    {
        var path = Path.Join(GetPackagePath(packageName, version), "commit.txt");
        return (File.GetLastWriteTime(path).ToUniversalTime(), File.ReadAllText(path));
    }
    
    public bool DoesPackageExist(string id) => Directory.Exists(GetPackagePath(id,null));

    public bool DeletePackage(string id, string version)
    {
        id = id.ToLower();
        version = version.ToLower();
        if (!DoesPackageExist(id, version))
            return false;
        
        Directory.Delete(GetPackagePath(id,version),true);
        return true;
    }
    
    public string[]? GetPackageVersions(string name)
    {
        var packagePath = GetPackagePath(name, null);
        return Directory.Exists(packagePath) 
            ? Directory.GetDirectories(packagePath).Select(d => new DirectoryInfo(d).Name).ToArray()
            : null;
    }

    public Memory<byte>? GetPackage(string name, string version)
    {
        var packagePath = GetPackagePath(name, version);
        return Directory.Exists(packagePath) ? File.ReadAllBytes(Path.Join(packagePath, $"{name}.nupkg")) : null;
    }

    public Memory<byte>? GetNuspecBytes(string name, string version)
    {
        var nuspecPath = GetPackagePath(name,version);
        return Directory.Exists(nuspecPath) ? File.ReadAllBytes(Path.Join(nuspecPath, $"{name}.nuspec")) : null;
    }

    public NugetSpecification GetNuspec(string name, string version)
    {
        var nuspecPath = Path.Join(GetPackagePath(name, version), $"{name}.nuspec");
        return NugetSpecification.FromStream(File.OpenRead(nuspecPath));
    }

    public Memory<byte>? GetIcon(string name, string version)
    {
        var iconPath = Path.Join(GetPackagePath(name,version), "icon.png");
        return File.Exists(iconPath) ? File.ReadAllBytes(iconPath) : null;
    }

    public DateTime GetPackageUploadTime(string name, string version) => GetCommit(name, version).lastWriteTime;
    public PackageAddResult AddPackage(NugetPackage package,Stream packageStream,Stream nuspecStream)
    {
        var nuspec = package.Nuspec;
        var packageName = nuspec.Id;
        var packagePath = GetPackagePath(packageName, null);
        if (DoesPackageExist(packageName, nuspec.Version))
            return PackageAddResult.AlreadyExists;
        
        Directory.CreateDirectory(packagePath);
        var packageVersionPath = Path.Join(packagePath, nuspec.Version);
        Directory.CreateDirectory(packageVersionPath);
        UpdateCommit(packageName,nuspec.Version);
        
        CreatePackageEntry(packageVersionPath,packageName,package,packageStream,nuspecStream);

        return PackageAddResult.Ok;
        
    }

    public Dictionary<string,List<NugetSpecification>> FindPackages(string? searchPattern,int skip,int take,bool prerelease,out int totalHits)
    {
        EnsurePackagesDirectoryExists();
        totalHits = 0;
        Dictionary<string, List<NugetSpecification>> specifications = new();
        searchPattern ??= searchPattern?.ToLower();
        var directories = Directory.GetDirectories(_packagesPath)
            .Skip(skip)
            .Take(take);
        if(!string.IsNullOrEmpty(searchPattern))
            directories = directories.Where(d => d.Contains(searchPattern));
        
        foreach (var dir in directories)
        {
            var versionDirectories = Directory.GetDirectories(dir);
            if (versionDirectories.Length is 0)
                continue;
            
            var packageName = new DirectoryInfo(dir).Name;
            specifications.TryAdd(packageName, new List<NugetSpecification>());
            var specs = specifications[packageName];

            bool noStable = true;
            foreach(var verDir in versionDirectories)
            {
                if (verDir.Contains('-') && !prerelease)
                    continue;
                noStable = false;
                using var fs = File.OpenRead(Path.Join(verDir, $"{packageName}.nuspec"));
                specs.Add(NugetSpecification.FromStream(fs));
            }

            if (noStable)
            {
                specifications.Remove(packageName);
                break;
            }
            totalHits++;
        }

        return specifications;
    }
}