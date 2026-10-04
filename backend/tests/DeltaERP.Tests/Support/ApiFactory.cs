using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaERP.Tests.Support;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // Program.cs lee Jwt y ConnectionStrings de forma temprana; las variables de
    // entorno son la única fuente que ve en ese momento.
    public ApiFactory(string connectionString)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", connectionString);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "DeltaERP.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "DeltaERP.Tests");
        Environment.SetEnvironmentVariable("Jwt__Key", "clave_solo_para_pruebas_0123456789_abcdefghijklmnopqrstuvwxyz");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.Scheme;
                options.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                options.DefaultChallengeScheme = TestAuthHandler.Scheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
        });
    }
}
