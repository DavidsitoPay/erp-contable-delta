using DeltaERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Infrastructure.Data;

public class DeltaErpDbContext : DbContext
{
    public DeltaErpDbContext(DbContextOptions<DeltaErpDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<AsientoContable> AsientosContables => Set<AsientoContable>();
    public DbSet<LineaAsiento> LineasAsiento => Set<LineaAsiento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Nombres de tabla en snake_case, igual al diccionario de datos entregado.
        modelBuilder.Entity<Usuario>().ToTable("usuario");
        modelBuilder.Entity<AsientoContable>().ToTable("asientocontable");
        modelBuilder.Entity<LineaAsiento>().ToTable("lineaasiento");

        base.OnModelCreating(modelBuilder);
    }
}
