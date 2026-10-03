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

    // M3 Libros: balance de saldos. Vista de solo lectura (sin clave primaria),
    // nunca se hace INSERT/UPDATE/DELETE sobre este DbSet.
    public DbSet<BalanceSaldoCuenta> BalanceSaldos => Set<BalanceSaldoCuenta>();

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

        // Sin clave primaria (es una vista, no una tabla) — HasNoKey() es
        // obligatorio para que EF no intente inferir un Id. ToView en vez de
        // ToTable: EF la trata como solo lectura y nunca genera migraciones
        // de escritura sobre ella.
        modelBuilder.Entity<BalanceSaldoCuenta>().HasNoKey().ToView("vw_balance_saldos");

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
