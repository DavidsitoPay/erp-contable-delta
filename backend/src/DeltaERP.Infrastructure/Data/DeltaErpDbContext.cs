using DeltaERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeltaERP.Infrastructure.Data;

public class DeltaErpDbContext : DbContext
{
    public DeltaErpDbContext(DbContextOptions<DeltaErpDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Perfil> Perfiles => Set<Perfil>();
    public DbSet<AsientoContable> AsientosContables => Set<AsientoContable>();
    public DbSet<LineaAsiento> LineasAsiento => Set<LineaAsiento>();
    public DbSet<CuentaContable> CuentasContables => Set<CuentaContable>();
    public DbSet<CentroCosto> CentrosCosto => Set<CentroCosto>();
    public DbSet<PeriodoContable> PeriodosContables => Set<PeriodoContable>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Nombres de tabla en snake_case, igual al diccionario de datos entregado.
        modelBuilder.Entity<Usuario>().ToTable("usuario");
        modelBuilder.Entity<Perfil>().ToTable("perfil");
        modelBuilder.Entity<AsientoContable>().ToTable("asientocontable");
        modelBuilder.Entity<LineaAsiento>().ToTable("lineaasiento");
        modelBuilder.Entity<CuentaContable>().ToTable("cuentacontable");
        modelBuilder.Entity<CentroCosto>().ToTable("centrocosto");
        modelBuilder.Entity<PeriodoContable>().ToTable("periodocontable");

        // LineaAsiento no tiene navegación de vuelta hacia AsientoContable, así que
        // la convención de EF no reconoce AsientoId como la FK de Lineas y crea una
        // columna sombra (AsientoContableId) que no existe en la base de datos.
        modelBuilder.Entity<AsientoContable>()
            .HasMany(a => a.Lineas)
            .WithOne()
            .HasForeignKey(l => l.AsientoId);

        base.OnModelCreating(modelBuilder);
    }
}
