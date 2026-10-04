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

    public DbSet<BalanceSaldoCuenta> BalanceSaldos => Set<BalanceSaldoCuenta>();

    public DbSet<Contraparte> Contrapartes => Set<Contraparte>();

    public DbSet<DocumentoCxC> DocumentosCxC => Set<DocumentoCxC>();
    public DbSet<LineaDocumentoCxC> LineasDocumentoCxC => Set<LineaDocumentoCxC>();
    public DbSet<ReciboPagoCliente> RecibosPagoCliente => Set<ReciboPagoCliente>();
    public DbSet<AplicacionPagoCliente> AplicacionesPagoCliente => Set<AplicacionPagoCliente>();
    public DbSet<SaldoDocumentoCxC> SaldosDocumentoCxC => Set<SaldoDocumentoCxC>();

    public DbSet<DocumentoCxP> DocumentosCxP => Set<DocumentoCxP>();
    public DbSet<LineaDocumentoCxP> LineasDocumentoCxP => Set<LineaDocumentoCxP>();
    public DbSet<PagoProveedorCabecera> PagosProveedor => Set<PagoProveedorCabecera>();
    public DbSet<AplicacionPagoProveedor> AplicacionesPagoProveedor => Set<AplicacionPagoProveedor>();
    public DbSet<SaldoDocumentoCxP> SaldosDocumentoCxP => Set<SaldoDocumentoCxP>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // snake_case: debe coincidir con los nombres de tabla reales en la base de datos.
        modelBuilder.Entity<Usuario>().ToTable("usuario");
        modelBuilder.Entity<Perfil>().ToTable("perfil");
        modelBuilder.Entity<AsientoContable>().ToTable("asientocontable");
        modelBuilder.Entity<LineaAsiento>().ToTable("lineaasiento");
        modelBuilder.Entity<CuentaContable>().ToTable("cuentacontable");
        modelBuilder.Entity<CentroCosto>().ToTable("centrocosto");
        modelBuilder.Entity<PeriodoContable>().ToTable("periodocontable");
        modelBuilder.Entity<Contraparte>().ToTable("contraparte");
        modelBuilder.Entity<DocumentoCxC>().ToTable("documentocxc");
        modelBuilder.Entity<LineaDocumentoCxC>().ToTable("lineadocumentocxc");
        modelBuilder.Entity<ReciboPagoCliente>().ToTable("recibopagocliente");
        modelBuilder.Entity<AplicacionPagoCliente>().ToTable("aplicacionpagocliente");
        modelBuilder.Entity<DocumentoCxP>().ToTable("documentocxp");
        modelBuilder.Entity<LineaDocumentoCxP>().ToTable("lineadocumentocxp");
        modelBuilder.Entity<PagoProveedorCabecera>().ToTable("pagoproveedorcabecera");
        modelBuilder.Entity<AplicacionPagoProveedor>().ToTable("aplicacionpagoproveedor");

        // Vistas sin clave primaria: HasNoKey() evita que EF infiera un Id, y ToView
        // (no ToTable) las deja de solo lectura, sin migraciones de escritura.
        modelBuilder.Entity<BalanceSaldoCuenta>().HasNoKey().ToView("vw_balance_saldos");
        modelBuilder.Entity<SaldoDocumentoCxC>().HasNoKey().ToView("vw_saldodocumentocxc");
        modelBuilder.Entity<SaldoDocumentoCxP>().HasNoKey().ToView("vw_saldodocumentocxp");

        // Sin navegación inversa, la convención de EF no reconoce AsientoId como FK
        // y crea una columna sombra (AsientoContableId) inexistente en la base de datos.
        modelBuilder.Entity<AsientoContable>()
            .HasMany(a => a.Lineas)
            .WithOne()
            .HasForeignKey(l => l.AsientoId);

        // Mismo motivo: evitar columnas sombra en las colecciones de CxC y CxP.
        modelBuilder.Entity<DocumentoCxC>()
            .HasMany(d => d.Lineas)
            .WithOne()
            .HasForeignKey(l => l.DocumentoId);
        modelBuilder.Entity<ReciboPagoCliente>()
            .HasMany(r => r.Aplicaciones)
            .WithOne()
            .HasForeignKey(a => a.ReciboPagoId);
        modelBuilder.Entity<DocumentoCxP>()
            .HasMany(d => d.Lineas)
            .WithOne()
            .HasForeignKey(l => l.DocumentoId);
        modelBuilder.Entity<PagoProveedorCabecera>()
            .HasMany(p => p.Aplicaciones)
            .WithOne()
            .HasForeignKey(a => a.PagoCabeceraId);

        base.OnModelCreating(modelBuilder);
    }
}
