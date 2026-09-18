using DeltaERP.Api.Auth;
using DeltaERP.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Api.Controllers;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, UsuarioAutenticado Usuario);

public record UsuarioAutenticado(int Id, string Nombre, string Email, string Perfil);

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly DeltaErpDbContext _db;
    private readonly TokenService _tokenService;
    private readonly PasswordHasher<object> _passwordHasher = new();

    public AuthController(DeltaErpDbContext db, TokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    /// <summary>
    /// M8 Seguridad. Valida credenciales y emite un JWT; el resto de la API
    /// (ver AsientosController) lo usa para resolver el usuario autenticado
    /// en vez de confiar en un usuarioId enviado por el cliente.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.Activo);

        if (usuario is null)
        {
            return Unauthorized(new { error = "Credenciales inválidas." });
        }

        var resultado = _passwordHasher.VerifyHashedPassword(new object(), usuario.PasswordHash, request.Password);
        if (resultado == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { error = "Credenciales inválidas." });
        }

        var perfilNombre = await _db.Perfiles
            .Where(p => p.Id == usuario.PerfilId)
            .Select(p => p.Nombre)
            .FirstAsync();

        var token = _tokenService.GenerarToken(usuario, perfilNombre);

        return Ok(new LoginResponse(token, new UsuarioAutenticado(usuario.Id, usuario.Nombre, usuario.Email, perfilNombre)));
    }
}
