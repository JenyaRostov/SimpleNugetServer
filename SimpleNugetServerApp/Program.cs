

using System.Security.Cryptography;
using System.Text.Json;
using SimpleNugetServer;

string? GetValueFromUser(string valueName,Func<string,bool>? validation,string errorMessage,bool returnEnter)
{
    while(true)
    {
        Console.WriteLine($"Please enter {valueName}:");
        var val = Console.ReadLine();
        
        if (val is null && returnEnter)
            return null;
        
        if (validation is null || validation(val)) return val;
        
        Console.WriteLine(errorMessage);

    }
}

HashSet<string> GetAllowedPasswords()
{
    HashSet<string> passwords = new();
    while (true)
    {
        var value = GetValueFromUser(
            "an allowed password. Type \"done\" to finish. If empty a random 16 characters hash will be generated",
            null,
            "",true);
        if (value is "done" or "DONE")
        {
            if (passwords.Count is not 0) return passwords;
            Console.WriteLine("There must be at least 1 allowed password");
            continue;
        }

        var isEmpty = string.IsNullOrWhiteSpace(value);
        var pass = isEmpty
            ? BitConverter.ToString(RandomNumberGenerator.GetBytes(16)).Replace("-", "")
            : value!;
        
        Console.WriteLine($"Adding password {pass}");
        passwords.Add(pass);

    }
    
}

void StorePasswords(HashSet<string> passwords) => File.WriteAllText("passwords.txt", JsonSerializer.Serialize(passwords));

HashSet<string>? LoadPasswords() => JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText("passwords.txt"));

void StoreConfig(NugetServerOptions options) => File.WriteAllText("options.json", JsonSerializer.Serialize(options));

NugetServerOptions? LoadConfig() => JsonSerializer.Deserialize<NugetServerOptions>(File.ReadAllText("options.json"));
//This implementation of nuget server doesn't require httppath, so we create a list of allowed hashes instead.
(NugetServerOptions options,HashSet<string> passwords) CreateAndStoreConfig()
{
    var packagesPath = GetValueFromUser("packages path", null, "",false)!;
    Console.WriteLine("Now type strings (passwords) which grant access to this nuget server");
    var passwords = GetAllowedPasswords();
    var host = GetValueFromUser("host (* for all ips)", null, "",false)!;
    var hostName = GetValueFromUser("hostname", null, "",false)!;
    var port = GetValueFromUser("port",
        (s) => int.TryParse(s, out var p) && (p is >= 1 and <= 65535),
        "Port must be a number and be in range [1,65535]", false)!;
    
    var options = new NugetServerOptions(packagesPath, "", host, hostName, int.Parse(port));
    StoreConfig(options);
    StorePasswords(passwords);
    return (options,passwords);
}

(NugetServerOptions? options,HashSet<string>? passwords) GetConfig()
{
    if (!File.Exists("options.json") || !File.Exists("passwords.txt"))
        return (null, null);

    return (LoadConfig(), LoadPasswords());
}

var config = GetConfig();
if(config.options is null)
{
    Console.WriteLine("Couldn't find configuration, prompting user to create one");
    config = CreateAndStoreConfig();
}

var passwords = config.passwords!;
config.options!.CustomPathValidationCallback = s => passwords.Contains(s);

var server = new NugetServer(config.options);
server.Start();
Console.WriteLine("To change configuration change config.json or passwords.txt");
Console.WriteLine($"Server started on {config.options.Host}:{config.options.Port}. Press enter to stop");
Console.ReadKey();


