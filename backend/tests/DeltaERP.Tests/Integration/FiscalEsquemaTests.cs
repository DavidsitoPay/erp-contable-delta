using DeltaERP.Domain.Entities;
using DeltaERP.Infrastructure.Data;
using DeltaERP.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class FiscalEsquemaTests
{
    private const string SqlDocumentoCxC =
        "INSERT INTO documentocxc (numero, tipo_documento, cliente_id, fecha, fecha_vencimiento, monto_total, monto_base, calculo_legado, estado) " +
        "VALUES ($1, 'Factura', $2, '2025-03-15', '2025-03-15', 100, $3, $4, 'Vigente')";

    private readonly PostgresFixture _pg;

    public FiscalEsquemaTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    [Theory]
    [InlineData(typeof(Moneda))]
    [InlineData(typeof(ConfiguracionFiscal))]
    [InlineData(typeof(Impuesto))]
    [InlineData(typeof(Contraparte))]
    [InlineData(typeof(DocumentoCxC))]
    [InlineData(typeof(LineaDocumentoCxC))]
    [InlineData(typeof(DocumentoCxP))]
    [InlineData(typeof(LineaDocumentoCxP))]
    public async Task ColumnasMapeadas_ExistenEnLaBaseReal(Type tipo)
    {
        using var scope = _pg.Factory.Services.CreateScope();
        var entidad = scope.ServiceProvider.GetRequiredService<DeltaErpDbContext>().Model.FindEntityType(tipo)!;
        var tabla = entidad.GetTableName()!;
        var objeto = StoreObjectIdentifier.Table(tabla, entidad.GetSchema());
        var faltantes = new List<string>();

        foreach (var propiedad in entidad.GetProperties())
        {
            var columna = propiedad.GetColumnName(objeto)!;
            var existe = await _pg.Data.ScalarAsync<long>(
                "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = $1 AND column_name = $2",
                tabla, columna);
            if (existe != 1)
            {
                faltantes.Add($"{tabla}.{columna}");
            }
        }

        Assert.Empty(faltantes);
    }

    [Fact]
    public async Task Migracion_SiembraImpuestosMonedaFuncionalYConfiguracionUnica()
    {
        var impuestos = await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM impuesto WHERE codigo IN ('IVA_GENERAL', 'IVA_GENERAL_SIN_CREDITO', 'EXENTO_EXPORTACION', 'NO_AFECTO', 'PEQUENO_CONTRIBUYENTE')");
        var funcional = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM moneda WHERE codigo = 'GTQ' AND es_funcional");
        var configuraciones = await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM configuracionfiscal");

        Assert.Equal((5L, 1L, 1L), (impuestos, funcional, configuraciones));
    }

    [Fact]
    public async Task ConfiguracionFiscal_NoAdmiteOtraFilaNiEliminacion()
    {
        var segundaFila = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("INSERT INTO configuracionfiscal (id, moneda_funcional_id) SELECT 2, moneda_funcional_id FROM configuracionfiscal WHERE id = 1"));
        var eliminacion = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("DELETE FROM configuracionfiscal WHERE id = 1"));

        Assert.Equal("ck_configuracionfiscal_unica", segundaFila.ConstraintName);
        Assert.Equal("55000", eliminacion.SqlState);
    }

    [Fact]
    public async Task Impuesto_NoSeEliminaFisicamente()
    {
        var id = await _pg.Data.CrearImpuestoAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync("DELETE FROM impuesto WHERE id = $1", id));

        Assert.Contains("no se elimina físicamente", ex.MessageText);
        Assert.Equal(1L, await _pg.Data.ScalarAsync<long>("SELECT COUNT(*) FROM impuesto WHERE id = $1", id));
    }

    [Theory]
    [InlineData(100, true, null)]
    [InlineData(100, false, "ck_documentocxc_dte")]
    [InlineData(90, true, "ck_documentocxc_montos")]
    public async Task DocumentoCxC_AplicaLosCheckDeMontoYDte(int montoBase, bool legado, string? restriccion)
    {
        var cliente = await _pg.Data.CrearContraparteAsync("Cliente");
        var insertar = () => _pg.Data.EjecutarAsync(SqlDocumentoCxC, $"F-{TestData.Sufijo()}", cliente, montoBase, legado);

        if (restriccion is null)
        {
            await insertar();
            return;
        }
        var ex = await Assert.ThrowsAsync<PostgresException>(insertar);
        Assert.Equal(restriccion, ex.ConstraintName);
    }

    [Fact]
    public async Task DocumentoCxP_ExigeElTrioDteCompletoOVacio()
    {
        var proveedor = await _pg.Data.CrearContraparteAsync("Proveedor");
        const string sql =
            "INSERT INTO documentocxp (numero, tipo_documento, proveedor_id, fecha, fecha_vencimiento, monto_total, monto_base, calculo_legado, dte_serie, estado) " +
            "VALUES ($1, 'Factura', $2, '2025-03-15', '2025-03-15', 100, 100, FALSE, $3::varchar, 'Vigente')";

        await _pg.Data.EjecutarAsync(sql, $"P-{TestData.Sufijo()}", proveedor, DBNull.Value);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync(sql, $"P-{TestData.Sufijo()}", proveedor, "SOLO-SERIE"));

        Assert.Equal("ck_documentocxp_dte_completo", ex.ConstraintName);
    }

    [Fact]
    public async Task LineaDeDocumento_ConImpuestoNoVigenteALaFecha_LaRechazaElTrigger()
    {
        var cliente = await _pg.Data.CrearContraparteAsync("Cliente");
        var cuenta = await _pg.Data.CrearCuentaAsync("Ingreso", "Acreedora");
        var futuro = await _pg.Data.CrearImpuestoAsync(new OpcionesImpuesto(Desde: new DateOnly(2030, 1, 1)));
        var documento = await _pg.Data.ScalarAsync<int>(
            "INSERT INTO documentocxc (numero, tipo_documento, cliente_id, fecha, fecha_vencimiento, monto_total, monto_base, calculo_legado, dte_uuid, dte_serie, dte_numero, dte_fecha_certificacion, estado) " +
            "VALUES ($1, 'Factura', $2, '2025-03-15', '2025-03-15', 100, 100, FALSE, $3, $4, $5, $6, 'Vigente') RETURNING id",
            $"F-{TestData.Sufijo()}", cliente, Guid.NewGuid(), $"S{TestData.Sufijo()}", "1", DateTimeOffset.UtcNow);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync(
            "INSERT INTO lineadocumentocxc (documento_id, cantidad, precio_unitario, tasa_aplicada, impuesto_id, monto_linea, monto_base, monto_iva, tipo_bien_servicio, cuenta_contable_id) " +
            "VALUES ($1, 1, 100, 12, $2, 100, 89.29, 10.71, 'SERVICIO', $3)",
            documento, futuro, cuenta));

        Assert.Equal("23514", ex.SqlState);
        Assert.Contains("no es aplicable", ex.MessageText);
    }
}
