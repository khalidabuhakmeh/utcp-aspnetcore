using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Utcp.Sample.Api;

public static class DemoSchemes
{
    public const string ApiKeyHeader = "ApiKeyHeader";
    public const string ApiKeyQuery = "ApiKeyQuery";
    public const string Basic = "Basic";
}

public sealed class DemoAuthOptions : AuthenticationSchemeOptions
{
    // No working defaults: credentials must be supplied via configuration
    // (appsettings.Development.json, user-secrets, or environment variables).
    public string? ApiKey { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

internal static class DemoCredential
{
    /// <summary>Constant-time comparison; fails closed when the expected value is unconfigured.</summary>
    public static bool Matches(string? expected, string provided)
    {
        return !string.IsNullOrEmpty(expected) &&
               CryptographicOperations.FixedTimeEquals(
                   Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));
    }
}

/// <summary>Demo-only api-key handler. Reads X-Api-Key header or api_key query depending on scheme.</summary>
public sealed class ApiKeyHandler(IOptionsMonitor<DemoAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<DemoAuthOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var key = Scheme.Name == DemoSchemes.ApiKeyQuery
            ? Request.Query["api_key"].FirstOrDefault()
            : Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(key)) return Task.FromResult(AuthenticateResult.NoResult());
        if (!DemoCredential.Matches(Options.ApiKey, key))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        var id = new ClaimsIdentity([new Claim(ClaimTypes.Name, "api-key-client")], Scheme.Name);
        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(id), Scheme.Name)));
    }
}

/// <summary>Demo-only HTTP Basic handler.</summary>
public sealed class BasicHandler(IOptionsMonitor<DemoAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<DemoAuthOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..])).Split(':', 2);
            if (parts.Length != 2
                || !DemoCredential.Matches(Options.Username, parts[0])
                || !DemoCredential.Matches(Options.Password, parts[1]))
                return Task.FromResult(AuthenticateResult.Fail("Invalid credentials"));
            var id = new ClaimsIdentity([new Claim(ClaimTypes.Name, parts[0])], Scheme.Name);
            return Task.FromResult(
                AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(id), Scheme.Name)));
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Malformed header"));
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"demo\"";
        return base.HandleChallengeAsync(properties);
    }
}
