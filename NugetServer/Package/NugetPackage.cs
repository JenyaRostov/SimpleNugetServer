using System.IO.Compression;

namespace NugetServer.Package;

public struct NugetPackage
{
    /*private byte[] _rels;
    private byte[] _coreProperties;
    private byte[] _contentTypes;*/

    public NugetSpecification Nuspec { get; private set; }
    public Memory<byte>? Icon { get; private set; }
    //private static HashSet<string> BannedFolders = new HashSet<string>() { "_rels", "package" };
    private Dictionary<string, Stream> _files;


    
    public static NugetPackage FromStream(Stream stream,out MemoryStream nuspecStream)
    {
        nuspecStream = new MemoryStream();
        NugetPackage package = new();
        package._files = new();
        var archive = new ZipArchive(stream);
        var nuspecEntry = archive.Entries.First(e => e.Name.EndsWith(".nuspec"));
        var nuspec = nuspecEntry.Open();
        nuspec.CopyTo(nuspecStream);
        nuspecStream.Position = 0;
        package.Nuspec = NugetSpecification.FromStream(nuspecStream);
        if (package.Nuspec.Icon != null)
        {
            var iconEntry = archive.Entries.First(e => e.Name == package.Nuspec.Icon);
            MemoryStream memory = new();
            iconEntry.Open().CopyTo(memory);
            package.Icon = memory.ToArray();
        }
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;
            var firstPart = name.Split("/")[0];
            switch (firstPart)
            {
                case "_rels":
                    //package._rels = ReadStream(entry.Open());
                    continue;
                case "package":
                    //package._coreProperties = ReadStream(entry.Open());
                    continue;
            }
            
            if (name == "[Content_Types].xml")
            {
                //package._contentTypes = ReadStream(entry.Open());
                continue;
            }

            if (name.Contains(".nuspec"))
                continue;

            package._files[name] = entry.Open();
        }

        if (stream.CanSeek)
            stream.Position = 0;
        return package;
    }
    
    /*public void Unpack(string path)
    {
        foreach (var file in _files)
        {
            var filePath = Path.Join(path, file.Key);
            var dirName = Path.GetDirectoryName(filePath);
            if (dirName is null)
                throw new ArgumentException("path must end with /", nameof(path));
            Directory.CreateDirectory(dirName);
            var stream = new FileStream(filePath, FileMode.Create);
            file.Value.CopyTo(stream);
            stream.Flush();
            stream.Close();
        }
        
    }*/
}