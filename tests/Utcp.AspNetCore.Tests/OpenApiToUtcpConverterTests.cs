using System.Text.Json;
using Microsoft.OpenApi.Models;
using Utcp.AspNetCore.Models;

namespace Utcp.AspNetCore.Tests;

public class OpenApiToUtcpConverterTests
{
    private static readonly OpenApiSecurityScheme Bearer = new() { Type = SecuritySchemeType.Http, Scheme = "bearer" };

    private static OpenApiDocument Doc(string? server, string id, OpenApiSecurityScheme scheme)
    {
        var doc = new OpenApiDocument
        {
            Components = new OpenApiComponents(),
            Paths = new OpenApiPaths
            {
                ["/x"] = new OpenApiPathItem
                {
                    Operations =
                    {
                        [OperationType.Get] = new OpenApiOperation
                        {
                            OperationId = "GetX",
                            Security =
                            {
                                new OpenApiSecurityRequirement
                                {
                                    [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = id } }] =
                                        []
                                }
                            },
                            Responses = new OpenApiResponses()
                        }
                    }
                }
            }
        };
        doc.Components.SecuritySchemes[id] = scheme;
        if (server is not null) doc.Servers.Add(new OpenApiServer { Url = server });
        return doc;
    }

    private static HttpCallTemplate Template(OpenApiDocument d, string fallback = "http://fallback")
    {
        return (HttpCallTemplate)OpenApiToUtcpConverter.Convert(d, fallback, new UtcpOptions()).Tools.Single()
            .ToolCallTemplate;
    }

    [Fact]
    public void Server_url_preferred_over_fallback()
    {
        var t = Template(Doc("https://api.example.com/", "Bearer", Bearer));
        Assert.Equal("https://api.example.com/x", t.Url);
    }

    [Fact]
    public void Relative_or_missing_server_uses_fallback()
    {
        Assert.Equal("http://fallback/x", Template(Doc(null, "Bearer", Bearer)).Url);
        Assert.Equal("http://fallback/x", Template(Doc("/api", "Bearer", Bearer)).Url);
    }

    [Fact]
    public void Bearer_var_derived_from_scheme_id()
    {
        var a = Assert.IsType<ApiKeyAuth>(Template(Doc(null, "my-jwt", Bearer)).Auth);
        Assert.Equal("Bearer ${MY_JWT_TOKEN}", a.ApiKey);
        Assert.Equal("Authorization", a.VarName);
    }

    [Fact]
    public void ApiKey_scheme_mapped()
    {
        var s = new OpenApiSecurityScheme
            { Type = SecuritySchemeType.ApiKey, Name = "X-Key", In = ParameterLocation.Query };
        var a = Assert.IsType<ApiKeyAuth>(Template(Doc(null, "ApiKey", s)).Auth);
        Assert.Equal("${APIKEY_API_KEY}", a.ApiKey);
        Assert.Equal("X-Key", a.VarName);
        Assert.Equal("query", a.Location);
    }

    [Fact]
    public void Basic_scheme_mapped()
    {
        var s = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "basic" };
        var b = Assert.IsType<BasicAuth>(Template(Doc(null, "Basic", s)).Auth);
        Assert.Equal("basic", b.AuthType);
        Assert.Equal("${BASIC_USERNAME}", b.Username);
        Assert.Equal("${BASIC_PASSWORD}", b.Password);
        var json = JsonSerializer.Serialize(b, typeof(object), UtcpJson.SerializerOptions);
        Assert.Contains("\"auth_type\": \"basic\"", json);
    }
}
