using DeltaERP.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DeltaERP.Tests.Support;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string JwtIssuer = "DeltaERP.Tests";
    public const string JwtAudience = "DeltaERP.Tests";
    public const string JwtKey = "clave_solo_para_pruebas_0123456789_abcdefghijklmnopqrstuvwxyz";

    public FakeBanguatClient Banguat { get; } = new();

    // Program.cs lee Jwt y ConnectionStrings de forma temprana; las variables de
    // entorno son la única fuente que ve en ese momento.
    public ApiFactory(string connectionString)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", connectionString);
        Environment.SetEnvironmentVariable("Jwt__Issuer", JwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", JwtAudience);
        Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);
        Environment.SetEnvironmentVariable("Banguat__Endpoint", "http://127.0.0.1:1/");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBanguatClient>();
            services.AddSingleton<IBanguatClient>(Banguat);
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
