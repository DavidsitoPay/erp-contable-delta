using DeltaERP.Tests.Support;
using Npgsql;

namespace DeltaERP.Tests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class BalanceJerarquicoEsquemaTests
{
    private readonly PostgresFixture _pg;

    public BalanceJerarquicoEsquemaTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    [Theory]
    [InlineData("Confirmado")]
    [InlineData("Borrador")]
    public async Task InsertarSubcuenta_BajoPadreConSaldoOBorrador_Rechaza(string estado)
    {
        var padre = await _pg.Data.CrearCuentaConSaldoPropioAsync(estado);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.CrearSubcuentaAsync(padre, "Activo", "Deudora"));

        Assert.Equal("55000", ex.SqlState);
        Assert.Contains("saldo propio", ex.MessageText);
    }

    [Fact]
    public async Task ActualizarPadre_HaciaCuentaConSaldo_Rechaza()
    {
        var padre = await _pg.Data.CrearCuentaConSaldoPropioAsync();
        var cuenta = await _pg.Data.CrearCuentaAsync("Activo", "Deudora");

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _pg.Data.EjecutarAsync("UPDATE cuentacontable SET cuenta_padre_id = $1 WHERE id = $2", padre, cuenta));

        Assert.Equal("55000", ex.SqlState);
    }
}
