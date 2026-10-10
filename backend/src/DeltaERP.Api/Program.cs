using System.Text;
using DeltaERP.Api.Auth;
using DeltaERP.Api.Services;
using DeltaERP.Infrastructure.Data;
using EFCore.NamingConventions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// UseSnakeCaseNamingConvention traduce columnas (PeriodoId -> periodo_id) para
// igualar el esquema SQL; los nombres de tabla explícitos en OnModelCreating
// se aplican después y tienen prioridad.
builder.Services.AddDbContext<DeltaErpDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
           .UseSnakeCaseNamingConvention());

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<ValidacionContable>();
builder.Services.AddScoped<AuditoriaService>();
builder.Services.AddScoped<ConfiguracionFiscalService>();
builder.Services.AddScoped<FacturaFiscalService>();
builder.Services.AddScoped<FacturaService>();
builder.Services.AddScoped<LibroFiscalService>();
builder.Services.AddScoped<TesoreriaService>();

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

// Cors:AllowedOrigins es configurable (coma-separado); default: frontend local de Vite.
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

await app.RunAsync();

// Expone Program a WebApplicationFactory<Program> en las pruebas de integración.
public partial class Program
{
    protected Program() { }
}
