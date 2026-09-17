using DeltaERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Capa de datos: PostgreSQL vía Npgsql / EF Core
builder.Services.AddDbContext<DeltaErpDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// TODO (Etapa 1 DevOps -> Etapa 3 Backend): configurar autenticación JWT (M8 Seguridad).
// La API es responsable de extraer el usuario_id del token para RN-08 (BitacoraAuditoria).
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
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
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "Delta ERP Contable API" }));

app.Run();
