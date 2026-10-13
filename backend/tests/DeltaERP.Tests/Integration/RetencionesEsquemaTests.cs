using DeltaERP.Tests.Support;
using Npgsql;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class RetencionesEsquemaTests
{
    private readonly PostgresFixture _pg;

    public RetencionesEsquemaTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    [Fact]
    public async Task Migracion_SiembraLosCuatroParametrosReferenciales()
    {
        var sembrados = await _pg.Data.ScalarAsync<long>(
            "SELECT COUNT(*) FROM parametroretencion WHERE nota = 'Referencial, validar con asesor' " +
            "AND codigo IN ('ISR_PROVEEDOR_ROS', 'IVA_CLIENTE_SECTOR_PUBLICO', 'IVA_CLIENTE_AGENTE_PRIVADO', 'PLAZO_CONSTANCIA_ISR')");

        Assert.Equal(4L, sembrados);
    }

    [Fact]
    public async Task Contraparte_NuevaSinRegimenDeIsr_QuedaNoDefinida()
    {
        var id = await _pg.Data.CrearContraparteAsync("Proveedor");

        Assert.Equal("NO_DEFINIDO", await _pg.Data.ScalarAsync<string>("SELECT regimen_isr FROM contraparte WHERE id = $1", id));
    }

    [Theory]
    [InlineData("INSERT INTO contraparte (tipo, nombre, regimen_isr) VALUES ('Proveedor', 'Sin fundamento', 'EXENTO')", "ck_contraparte_isr_fundamento")]
    [InlineData("INSERT INTO contraparte (tipo, nombre, es_agente_retencion_iva) VALUES ('Cliente', 'Agente sin tipo', TRUE)", "ck_contraparte_agente_iva")]
    [InlineData("INSERT INTO contraparte (tipo, nombre, regimen_isr) VALUES ('Proveedor', 'Régimen ajeno', 'OTRO')", "ck_contraparte_regimen_isr")]
    [InlineData("INSERT INTO parametroretencion (codigo, concepto, nombre, tasa_base, tramo_limite, articulo_legal, vigente_desde) VALUES ('T1', 'ISR_PROVEEDOR_ROS', 'x', 5, 30000, 'x', '2040-01-01')", "ck_parametroretencion_tramo")]
    [InlineData("INSERT INTO parametroretencion (codigo, concepto, nombre, tasa_base, articulo_legal, vigente_desde) VALUES ('T2', 'PLAZO_CONSTANCIA', 'x', 5, 'x', '2040-01-01')", "ck_parametroretencion_forma")]
    [InlineData("INSERT INTO parametroretencion (codigo, concepto, nombre, tasa_base, umbral_sobre, articulo_legal, vigente_desde) VALUES ('T3', 'IVA_RETENIDO_CLIENTE', 'x', 5, 'OTRO', 'x', '2040-01-01')", "ck_parametroretencion_umbral_sobre")]
    public async Task Tabla_ConValoresInconsistentes_ViolaElCheck(string sql, string restriccion)
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.EjecutarAsync(sql));

        Assert.Equal(restriccion, ex.ConstraintName);
    }

    [Fact]
    public async Task ConfiguracionFiscal_NoAdmiteLaMismaCuentaEnIvaEIsr()
    {
        var debito = await _pg.Data.CuentaPorCodigoAsync(TestData.CodigoCuentaIvaDebito);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => _pg.Data.ConfigurarCuentasRetencionAsync(debito, null));

        Assert.Equal("ck_configuracionfiscal_cuentas_retencion", ex.ConstraintName);
    }
}
