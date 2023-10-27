using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace NugetServer.Auth;

public class AuthOptions : AuthenticationSchemeOptions
{}

//https://stackoverflow.com/questions/73999266/asp-net-core-web-api-how-to-authorize-my-web-api-request-with-basic-auth
public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;

    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IConfiguration configuration) : base(options, logger, encoder, clock)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var host = new Uri(_configuration["urls"]!).Host;
        var authHeader = Request.Headers["Authorization"].ToString();

        if (authHeader != null && authHeader.StartsWith("basic", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring("Basic ".Length).Trim();
            //Console.WriteLine(token);
            var credentialstring = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var credentials = credentialstring.Split(':');

            var username = _configuration["BasicCredentials:username"];
            var password = _configuration["BasicCredentials:password"];

            if (credentials[0] == username && credentials[1] == password)
            {
                var claims = new[] { new Claim("name", credentials[0]), new Claim(ClaimTypes.Role, "Admin") };
                var identity = new ClaimsIdentity(claims, "Basic");
                var claimsPrincipal = new ClaimsPrincipal(identity);

                return Task.FromResult(
                    AuthenticateResult.Success(new AuthenticationTicket(claimsPrincipal, Scheme.Name)));
            }

            Response.StatusCode = 401;
            Response.Headers.Add("WWW-Authenticate", $"Basic realm=\"{host}\"");

            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization Header"));
        }
        else
        {
            Response.StatusCode = 401;
            Response.Headers.Add("WWW-Authenticate", $"Basic realm=\"{host}\"");

            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization Header"));
        }
    }
}