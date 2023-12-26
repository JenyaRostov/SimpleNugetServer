using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using NugetServer.Auth;
using NugetServer.Controllers;

var builder = WebApplication.CreateBuilder(args);

bool useHttps = File.Exists("key");
Console.WriteLine($"HTTPS Status: {useHttps}");
if(useHttps)
{
    var rsa = RSA.Create();
    rsa.ImportFromPem(File.ReadAllText("key"));
    var cert = new X509Certificate2("cert.pem");
    cert = cert.CopyWithPrivateKey(rsa);
    var httpsConnectionAdapterOptions = new HttpsConnectionAdapterOptions
    {
        SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        ClientCertificateMode = ClientCertificateMode.AllowCertificate,
        ServerCertificate = cert

    };
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ConfigureEndpointDefaults(listenOptions =>
        {
            listenOptions.UseHttps(httpsConnectionAdapterOptions);
        });
    });
}
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicScheme",options => {});

var app = builder.Build();

if(useHttps)
    app.UseHttpsRedirection();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseAuthorization();

app.MapControllers();

var service = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>();
var descriptors = service.ActionDescriptors
    .Items
    .OfType<ControllerActionDescriptor>();

NugetController.InitNuget(descriptors,builder.Configuration);
app.Run();