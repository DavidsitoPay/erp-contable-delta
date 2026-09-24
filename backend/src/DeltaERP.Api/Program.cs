using System.Text;
using DeltaERP.Api.Auth;
using DeltaERP.Infrastructure.Data;
using EFCore.NamingConventions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Capa de datos: PostgreSQL vía Npgsql / EF Core.
// UseSnakeCaseNamingConvention traduce columnas (PeriodoId -> periodo_id, etc.)
// para que coincidan con database/01_tables.sql sin mapearlas una por una.
// Los nombres de tabla (concatenados sin guion bajo) se mantienen explícitos
// en DeltaErpDbContext.OnModelCreating, que se aplica después y tiene prioridad.
builder.Services.AddDbContext<DeltaErpDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
           .UseSnakeCaseNamingConvention());

// M8 Seguridad: autenticación JWT. Los controladores resuelven el usuario
// autenticado desde el token (ver AsientosController) en vez de confiar en un
// usuarioId enviado por el cliente — esto es lo que hace correcta a RN-08.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<TokenService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Falta la sección Jwt en la configuración.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Orígenes permitidos configurables (Cors:AllowedOrigins, separados por coma) para no
// tener que recompilar al cambiar de entorno. Default: solo el frontend local de Vite.
var corsOrigins = builder.Configuration.GetValue<string>("Cors:AllowedOrigins")
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "Delta ERP Contable API" }));

app.Run();
