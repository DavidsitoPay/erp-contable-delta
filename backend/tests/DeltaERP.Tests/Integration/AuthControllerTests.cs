using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DeltaERP.Api.Auth;
using DeltaERP.Tests.Support;
using Microsoft.IdentityModel.Tokens;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class AuthControllerTests
{
    private readonly PostgresFixture _pg;

    public AuthControllerTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    private static TokenValidationParameters ParametrosDeValidacion() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = ApiFactory.JwtIssuer,
        ValidateAudience = true,
        ValidAudience = ApiFactory.JwtAudience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.JwtKey)),
    };

    private static async Task<JsonElement> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Login_ConCredencialesValidas_DevuelveTokenFirmadoConLosClaimsDelUsuario()
    {
        var (usuarioId, email, password) = await _pg.Data.CrearUsuarioConCredencialesAsync(Roles.Contador, "Secreta#123");
        var client = _pg.Factory.CreateClient();

        var body = await LoginAsync(client, email, password);

        var token = body.GetProperty("token").GetString();
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        var usuario = body.GetProperty("usuario");
        Assert.Equal(usuarioId, usuario.GetProperty("id").GetInt32());
        Assert.Equal(email, usuario.GetProperty("email").GetString());
        Assert.Equal(Roles.Contador, usuario.GetProperty("perfil").GetString());
        Assert.NotEmpty(usuario.GetProperty("nombre").GetString()!);

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, ParametrosDeValidacion(), out _);
        Assert.Equal(Roles.Contador, principal.FindFirst(ClaimTypes.Role)?.Value);
        Assert.Equal(email, principal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal(usuarioId.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.NotEmpty(principal.FindFirst(ClaimTypes.Name)?.Value ?? "");

        var body2 = await LoginAsync(client, email, password);
        var token2 = body2.GetProperty("token").GetString();
        var jti1 = new JwtSecurityTokenHandler().ReadJwtToken(token).Id;
        var jti2 = new JwtSecurityTokenHandler().ReadJwtToken(token2).Id;
        Assert.NotEqual(jti1, jti2);
    }

    [Fact]
    public async Task Login_ConCredencialesInvalidas_Responde401()
    {
        var activo = await _pg.Data.CrearUsuarioConCredencialesAsync(Roles.Contador, "Secreta#123");
        var inactivo = await _pg.Data.CrearUsuarioConCredencialesAsync(Roles.Contador, "Secreta#123", activo: false);
        var client = _pg.Factory.CreateClient();

        var casos = new[]
        {
            new { Email = $"{TestData.Sufijo()}@test.local", Password = "Secreta#123" },
            new { Email = activo.Email, Password = "otra-clave" },
            new { Email = inactivo.Email, Password = "Secreta#123" },
        };

        foreach (var caso in casos)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email = caso.Email, password = caso.Password });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var error = await response.LeerErrorAsync();
            Assert.Contains("inválidas", error);
        }
    }
}
